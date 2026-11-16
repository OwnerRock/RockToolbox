using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RockToolbox.Native
{
    internal sealed class SiRow
    {
        public string Section { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public int Pct { get; set; }          // -1 = no bar
        public bool Warn { get; set; }
        public string Live { get; set; }
    }

    // C# port of src\pages\SystemInfo.ps1 + SystemInfoLive.ps1 (report + live 1-second refresh).
    internal sealed class SystemInfoPage : IPage, IHidable
    {
        private sealed class LiveEntry { public SiRow Row; public TextBlock Text; public ProgressBar Bar; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }
        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

        private StackPanel _panel;
        private TextBlock _info;
        private List<SiRow> _rows = new List<SiRow>();
        private readonly Dictionary<string, LiveEntry> _live = new Dictionary<string, LiveEntry>();
        private readonly Dictionary<string, double[]> _netPrev = new Dictionary<string, double[]>();   // rx, tx, ticks
        private DispatcherTimer _timer;
        private bool _loaded, _active, _initStarted;
        private int _tick, _gpuEvery = 3;
        private PerformanceCounter _cpu, _diskT, _diskR, _diskW;
        private DateTime _boot = DateTime.MinValue;
        private string CachePath { get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox"), "sysinfo.json"); } }

        public string Key { get { return "System Info"; } }

        public void Init(Window w, Logger log)
        {
            _panel = Ui.Find<StackPanel>("siPanel");
            _info = Ui.Find<TextBlock>("txtSiInfo");
            Ui.Find<Button>("btnSiRefresh").Click += delegate { Load(); };
            Ui.Find<Button>("btnSiCopy").Click += delegate
            {
                if (_rows.Count == 0) { Ui.Log.Warn("Nothing to copy yet - wait for the report to load.", "SysInfo"); return; }
                try { Clipboard.SetText(ReportText()); Ui.Log.Info("System report copied to the clipboard.", "SysInfo"); }
                catch { Ui.Log.Warn("Could not copy to the clipboard.", "SysInfo"); }
            };
            Ui.Find<Button>("btnSiSave").Click += delegate
            {
                if (_rows.Count == 0) { Ui.Log.Warn("Nothing to save yet - wait for the report to load.", "SysInfo"); return; }
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.Filter = "Text file (*.txt)|*.txt";
                dlg.FileName = "System-report.txt";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dlg.ShowDialog() == true)
                {
                    try { File.WriteAllText(dlg.FileName, ReportText(), new UTF8Encoding(true)); Ui.Log.Info("Report saved: " + dlg.FileName, "SysInfo"); }
                    catch (Exception ex) { Ui.Log.Error("Could not save: " + ex.Message, "SysInfo"); }
                }
            };
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += delegate { UpdateLive(); };
        }

        public void OnShown()
        {
            _active = true;
            StartLive();
            if (!_loaded) Load();
            // stop the live timer when another tab opens
            Ui.Win.Dispatcher.BeginInvoke(new Action(delegate { }));
        }

        public void OnHidden() { _active = false; _timer.Stop(); }

        // ---------------- report ----------------
        private string ReportText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Rock Toolbox - System report  (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            string last = "";
            foreach (SiRow r in _rows)
            {
                if (r.Section != last) { sb.AppendLine(); sb.AppendLine("== " + r.Section + " =="); last = r.Section; }
                sb.AppendLine((r.Label ?? "").Trim().PadRight(28) + " " + r.Value);
            }
            return sb.ToString();
        }

        private void Load()
        {
            if (_rows.Count == 0) ShowCached();
            Work.Run("Reading system information...", delegate(WorkCtx c)
            {
                c.Info("Reading system information...", "SysInfo");
                List<SiRow> rows = Collect(c);
                _rows = rows;
                c.UI(delegate
                {
                    Show(rows);
                    _loaded = true;
                    _info.Text = "Live while this tab is open: processor, memory, uptime, network traffic, disk activity, drive space, battery and GPU load. Press Refresh to read everything again.";
                });
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                    File.WriteAllText(CachePath, new JavaScriptSerializer().Serialize(rows), new UTF8Encoding(false));
                }
                catch { }
                c.Info("System information ready.", "SysInfo");
            });
        }

        private void ShowCached()
        {
            try
            {
                if (!File.Exists(CachePath)) return;
                List<SiRow> rows = new JavaScriptSerializer().Deserialize<List<SiRow>>(File.ReadAllText(CachePath, Encoding.UTF8));
                if (rows != null && rows.Count > 0) { _rows = rows; Show(rows); }
            }
            catch { }
        }

        private static List<ManagementObject> Q(string scope, string query)
        {
            List<ManagementObject> l = new List<ManagementObject>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, query))
                using (ManagementObjectCollection col = s.Get())
                    foreach (ManagementObject o in col) l.Add(o);
            }
            catch { }
            return l;
        }

        private static string S(object o) { return o == null ? "" : Convert.ToString(o); }
        private static double D(object o) { try { return o == null ? 0 : Convert.ToDouble(o); } catch { return 0; } }
        private static DateTime Dt(object o) { try { return ManagementDateTimeConverter.ToDateTime(S(o)); } catch { return DateTime.MinValue; } }

        private static void Add(List<SiRow> rows, string sec, string lbl, string val, int pct, bool warn, string live)
        {
            if (val == null || val.Trim().Length == 0) return;
            SiRow r = new SiRow(); r.Section = sec; r.Label = lbl; r.Value = val; r.Pct = pct; r.Warn = warn; r.Live = live ?? "";
            rows.Add(r);
        }
        private static void Add(List<SiRow> rows, string sec, string lbl, string val) { Add(rows, sec, lbl, val, -1, false, ""); }

        private List<SiRow> Collect(WorkCtx c)
        {
            List<SiRow> rows = new List<SiRow>();
            c.Progress(-1, "Reading Windows and computer info...");
            ManagementObject os = null, cs = null, bios = null, bb = null;
            List<ManagementObject> t;
            t = Q("root\\cimv2", "SELECT * FROM Win32_OperatingSystem"); if (t.Count > 0) os = t[0];
            t = Q("root\\cimv2", "SELECT * FROM Win32_ComputerSystem"); if (t.Count > 0) cs = t[0];
            t = Q("root\\cimv2", "SELECT * FROM Win32_BIOS"); if (t.Count > 0) bios = t[0];
            t = Q("root\\cimv2", "SELECT * FROM Win32_BaseBoard"); if (t.Count > 0) bb = t[0];
            string cvp = @"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string ver = S(Reg.Get(cvp, "DisplayVersion"));
            if (ver.Length == 0) ver = S(Reg.Get(cvp, "ReleaseId"));
            string ubr = S(Reg.Get(cvp, "UBR"));
            if (os != null)
            {
                Add(rows, "Windows", "Edition", S(os["Caption"]));
                Add(rows, "Windows", "Version", ver + "  (build " + S(os["BuildNumber"]) + "." + ubr + ")");
                Add(rows, "Windows", "Architecture", S(os["OSArchitecture"]));
                DateTime inst = Dt(os["InstallDate"]);
                if (inst != DateTime.MinValue) Add(rows, "Windows", "Installed on", inst.ToString("yyyy-MM-dd"));
                DateTime boot = Dt(os["LastBootUpTime"]);
                if (boot != DateTime.MinValue)
                {
                    TimeSpan up = DateTime.Now - boot;
                    Add(rows, "Windows", "Uptime", up.Days + " d " + up.Hours + " h " + up.Minutes + " min " + up.Seconds + " s", -1, false, "up");
                }
            }
            Add(rows, "Windows", "Computer name", Environment.MachineName);
            object sb = Reg.Get(@"HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
            if (sb != null) Add(rows, "Windows", "Secure Boot", Convert.ToInt32(sb) == 1 ? "On" : "Off");
            else Add(rows, "Windows", "Secure Boot", "Not supported (legacy BIOS)");
            List<ManagementObject> tpm = Q("root\\cimv2\\Security\\MicrosoftTpm", "SELECT * FROM Win32_Tpm");
            if (tpm.Count > 0)
                Add(rows, "Windows", "TPM", "Present, version " + S(tpm[0]["SpecVersion"]).Split(',')[0].Trim() + ", enabled: " + S(tpm[0]["IsEnabled_InitialValue"]));
            else Add(rows, "Windows", "TPM", "Not found");

            if (cs != null) { Add(rows, "Computer", "Manufacturer", S(cs["Manufacturer"])); Add(rows, "Computer", "Model", S(cs["Model"])); }
            if (bb != null) Add(rows, "Computer", "Motherboard", (S(bb["Manufacturer"]) + " " + S(bb["Product"])).Trim());
            if (bios != null)
            {
                DateTime rd = Dt(bios["ReleaseDate"]);
                Add(rows, "Computer", "BIOS", (S(bios["SMBIOSBIOSVersion"]) + "  " + (rd != DateTime.MinValue ? "(" + rd.ToString("yyyy-MM-dd") + ")" : "")).Trim());
                Add(rows, "Computer", "Serial number", S(bios["SerialNumber"]));
            }

            c.Progress(-1, "Reading processor and memory...");
            foreach (ManagementObject p in Q("root\\cimv2", "SELECT * FROM Win32_Processor"))
            {
                Add(rows, "Processor", "Name", Regex.Replace(S(p["Name"]), @"\s+", " ").Trim());
                Add(rows, "Processor", "Cores / threads", S(p["NumberOfCores"]) + " cores / " + S(p["NumberOfLogicalProcessors"]) + " threads");
                Add(rows, "Processor", "Max speed", (D(p["MaxClockSpeed"]) / 1000.0).ToString("0.00") + " GHz");
                int load = (int)D(p["LoadPercentage"]);
                Add(rows, "Processor", "Current load", load + " %", load, false, "cpu");
            }
            if (os != null)
            {
                double total = D(os["TotalVisibleMemorySize"]) * 1024, free = D(os["FreePhysicalMemory"]) * 1024;
                if (total > 0)
                {
                    int usedPct = (int)(100 * (total - free) / total);
                    Add(rows, "Memory", "RAM in use", Fmt.Bytes(total - free) + " of " + Fmt.Bytes(total) + "  (" + usedPct + " %)", usedPct, usedPct >= 90, "ram");
                }
            }
            Dictionary<int, string> types = new Dictionary<int, string> { { 20, "DDR" }, { 21, "DDR2" }, { 24, "DDR3" }, { 26, "DDR4" }, { 34, "DDR5" }, { 30, "LPDDR4" }, { 35, "LPDDR5" } };
            int mi = 1;
            foreach (ManagementObject m in Q("root\\cimv2", "SELECT * FROM Win32_PhysicalMemory"))
            {
                string tp; if (!types.TryGetValue((int)D(m["SMBIOSMemoryType"]), out tp)) tp = "";
                int sp = (int)D(m["ConfiguredClockSpeed"]); if (sp == 0) sp = (int)D(m["Speed"]);
                Add(rows, "Memory", "Module " + mi, Regex.Replace(Fmt.Bytes(D(m["Capacity"])) + "  " + tp + "  " + sp + " MHz  " + S(m["Manufacturer"]) + "  " + S(m["PartNumber"]), @"\s+", " ").Trim());
                mi++;
            }

            c.Progress(-1, "Reading graphics...");
            Dictionary<string, double> vram = new Dictionary<string, double>();
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                    if (k != null)
                        foreach (string sk in k.GetSubKeyNames())
                            using (Microsoft.Win32.RegistryKey g = k.OpenSubKey(sk))
                            {
                                if (g == null) continue;
                                string dd = S(g.GetValue("DriverDesc"));
                                object q = g.GetValue("HardwareInformation.qwMemorySize");
                                if (dd.Length > 0 && q != null) vram[dd] = D(q);
                            }
            }
            catch { }
            List<ManagementObject> vcs = Q("root\\cimv2", "SELECT * FROM Win32_VideoController");
            foreach (ManagementObject g in vcs)
            {
                string gn = S(g["Name"]);
                Add(rows, "Graphics", "Card", gn);
                double vr; if (!vram.TryGetValue(gn, out vr) || vr <= 0) vr = D(g["AdapterRAM"]);
                if (vr > 0) Add(rows, "Graphics", "  Video memory", Fmt.Bytes(vr));
                DateTime gd = Dt(g["DriverDate"]);
                Add(rows, "Graphics", "  Driver", S(g["DriverVersion"]) + (gd != DateTime.MinValue ? "  (" + gd.ToString("yyyy-MM-dd") + ")" : ""));
                if (D(g["CurrentHorizontalResolution"]) > 0)
                    Add(rows, "Graphics", "  Resolution", S(g["CurrentHorizontalResolution"]) + " x " + S(g["CurrentVerticalResolution"]) + " @ " + S(g["CurrentRefreshRate"]) + " Hz");
            }
            if (vcs.Count > 0) Add(rows, "Graphics", "GPU load now (3D engine)", "0 %", 0, false, "gpu");

            c.Progress(-1, "Reading storage...");
            Dictionary<int, string> media = new Dictionary<int, string> { { 3, "HDD" }, { 4, "SSD" }, { 5, "SCM" } };
            Dictionary<int, string> bus = new Dictionary<int, string> { { 1, "SCSI" }, { 3, "ATA" }, { 7, "USB" }, { 8, "RAID" }, { 11, "SATA" }, { 17, "NVMe" } };
            foreach (ManagementObject d in Q("root\\Microsoft\\Windows\\Storage", "SELECT * FROM MSFT_PhysicalDisk"))
            {
                int h = (int)D(d["HealthStatus"]);
                string hs = h == 0 ? "Healthy" : (h == 1 ? "Warning" : (h == 2 ? "Unhealthy" : "Unknown"));
                string mt; if (!media.TryGetValue((int)D(d["MediaType"]), out mt)) mt = "";
                string bt; if (!bus.TryGetValue((int)D(d["BusType"]), out bt)) bt = "";
                Add(rows, "Storage", "Disk: " + S(d["FriendlyName"]), Fmt.Bytes(D(d["Size"])) + "  " + mt + "  " + bt + "  -  health: " + hs, -1, hs != "Healthy", "");
            }
            foreach (ManagementObject v in Q("root\\cimv2", "SELECT * FROM Win32_LogicalDisk WHERE DriveType=3"))
            {
                double size = D(v["Size"]), fr = D(v["FreeSpace"]);
                if (size <= 0) continue;
                int usedPct = (int)(100 * (size - fr) / size);
                Add(rows, "Storage", "Drive " + S(v["DeviceID"]) + "  " + S(v["VolumeName"]),
                    Fmt.Bytes(fr) + " free of " + Fmt.Bytes(size) + "  (" + usedPct + " % used)", usedPct, usedPct >= 90, "vol:" + S(v["DeviceID"]));
            }
            Add(rows, "Storage", "Disk activity now", "Read 0 B/s    Write 0 B/s    (0 %)", 0, false, "disk");

            c.Progress(-1, "Reading network...");
            foreach (NetworkInterface a in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (a.OperationalStatus != OperationalStatus.Up) continue;
                if (a.NetworkInterfaceType == NetworkInterfaceType.Loopback || a.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                if (Regex.IsMatch(a.Description, "Virtual|Pseudo|Bluetooth|VPN|TAP|Hyper-V|VMware|VirtualBox")) continue;
                List<string> ips = new List<string>();
                foreach (UnicastIPAddressInformation u in a.GetIPProperties().UnicastAddresses)
                    if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ips.Add(u.Address.ToString());
                byte[] mb = a.GetPhysicalAddress().GetAddressBytes();
                string[] mac = new string[mb.Length]; for (int i = 0; i < mb.Length; i++) mac[i] = mb[i].ToString("X2");
                string spd = a.Speed > 0 ? (a.Speed / 1000000.0).ToString("0") + " Mbps" : "";
                Add(rows, "Network", a.Name, a.Description + "  -  " + spd + "  -  " + string.Join(", ", ips.ToArray()) + "  -  " + string.Join("-", mac));
                Add(rows, "Network", "    Traffic now", "Down 0 B/s    Up 0 B/s", -1, false, "net:" + a.Name);
            }

            List<ManagementObject> bat = Q("root\\cimv2", "SELECT * FROM Win32_Battery");
            if (bat.Count > 0)
            {
                c.Progress(-1, "Reading battery...");
                Dictionary<int, string> bs = new Dictionary<int, string> { { 1, "Discharging" }, { 2, "On AC power" }, { 3, "Fully charged" }, { 4, "Low" }, { 5, "Critical" }, { 6, "Charging" }, { 7, "Charging (high)" }, { 8, "Charging (low)" }, { 9, "Charging (critical)" } };
                foreach (ManagementObject b in bat)
                {
                    string st; if (!bs.TryGetValue((int)D(b["BatteryStatus"]), out st)) st = "";
                    int ch = (int)D(b["EstimatedChargeRemaining"]);
                    Add(rows, "Battery", "Charge", ch + " %  " + st, ch, false, "bat");
                }
                List<ManagementObject> sd = Q("root\\wmi", "SELECT * FROM BatteryStaticData");
                List<ManagementObject> fc = Q("root\\wmi", "SELECT * FROM BatteryFullChargedCapacity");
                double des = sd.Count > 0 ? D(sd[0]["DesignedCapacity"]) : 0, ful = fc.Count > 0 ? D(fc[0]["FullChargedCapacity"]) : 0;
                if (des > 0 && ful > 0)
                {
                    int h = (int)(100 * ful / des);
                    Add(rows, "Battery", "Health", h + " %   (full charge " + (int)ful + " mWh of " + (int)des + " mWh designed)", h, h < 60, "");
                }
                List<ManagementObject> cy = Q("root\\wmi", "SELECT * FROM BatteryCycleCount");
                if (cy.Count > 0 && D(cy[0]["CycleCount"]) > 0) Add(rows, "Battery", "Charge cycles", S(cy[0]["CycleCount"]));
            }
            return rows;
        }

        // ---------------- UI ----------------
        private static void Fg(FrameworkElement el, string hex)
        {
            TextBlock tb = el as TextBlock;
            if (tb != null) tb.Foreground = Ui.Brush(hex);
            ProgressBar pb = el as ProgressBar;
            if (pb != null) pb.Foreground = Ui.Brush(hex);
        }

        private void Show(List<SiRow> rows)
        {
            _panel.Children.Clear();
            _live.Clear();
            List<string> order = new List<string>();
            Dictionary<string, List<SiRow>> secs = new Dictionary<string, List<SiRow>>();
            foreach (SiRow r in rows)
            {
                if (!secs.ContainsKey(r.Section)) { secs[r.Section] = new List<SiRow>(); order.Add(r.Section); }
                secs[r.Section].Add(r);
            }
            foreach (string s in order)
            {
                TextBlock h = new TextBlock(); h.Text = Lang.L(s);
                try { h.Style = (Style)Ui.Win.FindResource("H"); } catch { }
                _panel.Children.Add(h);
                Grid g = new Grid(); g.Margin = new Thickness(6, 0, 0, 4);
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                int ri = 0;
                foreach (SiRow r in secs[s])
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    TextBlock lbl = new TextBlock(); lbl.Text = r.Label; lbl.Margin = new Thickness(0, 3, 12, 3); lbl.TextWrapping = TextWrapping.Wrap;
                    lbl.Foreground = Ui.Brush("#9AA0B8");
                    Grid.SetRow(lbl, ri); Grid.SetColumn(lbl, 0); g.Children.Add(lbl);
                    StackPanel stack = new StackPanel();
                    TextBlock val = new TextBlock(); val.Text = r.Value; val.Margin = new Thickness(0, 3, 0, 3); val.TextWrapping = TextWrapping.Wrap;
                    val.Foreground = Ui.Brush(r.Warn ? "#FFB454" : "#E6E8F2");
                    stack.Children.Add(val);
                    LiveEntry le = null;
                    if (!string.IsNullOrEmpty(r.Live)) { le = new LiveEntry(); le.Row = r; le.Text = val; _live[r.Live] = le; }
                    if (r.Pct >= 0)
                    {
                        ProgressBar pb = new ProgressBar();
                        try { pb.Style = (Style)Ui.Win.FindResource("StripeBar"); } catch { }
                        pb.Height = 6; pb.Minimum = 0; pb.Maximum = 100; pb.Value = r.Pct; pb.Margin = new Thickness(0, 0, 40, 4);
                        pb.Foreground = Ui.Brush(r.Warn ? "#FF6B6B" : "#4FD1FF");
                        pb.Background = Ui.Brush("#0F1116"); pb.BorderBrush = Ui.Brush("#2B3038");
                        stack.Children.Add(pb);
                        if (le != null) le.Bar = pb;
                    }
                    Grid.SetRow(stack, ri); Grid.SetColumn(stack, 1); g.Children.Add(stack);
                    ri++;
                }
                _panel.Children.Add(g);
            }
        }

        // ---------------- live refresh ----------------
        private void StartLive()
        {
            _tick = 0; _netPrev.Clear();
            if (!_initStarted)
            {
                _initStarted = true;
                // counters are created on another thread - creating them on the UI thread froze the window for seconds
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try { PerformanceCounter c = new PerformanceCounter("Processor", "% Processor Time", "_Total"); c.NextValue(); _cpu = c; } catch { }
                    try
                    {
                        PerformanceCounter t = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                        PerformanceCounter r = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                        PerformanceCounter wr = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                        t.NextValue(); r.NextValue(); wr.NextValue();
                        _diskT = t; _diskR = r; _diskW = wr;
                    }
                    catch { }
                    try { List<ManagementObject> o = Q("root\\cimv2", "SELECT LastBootUpTime FROM Win32_OperatingSystem"); if (o.Count > 0) _boot = Dt(o[0]["LastBootUpTime"]); } catch { }
                });
            }
            if (!_timer.IsEnabled) _timer.Start();
        }

        private static void Smooth(ProgressBar bar, double v)
        {
            if (bar == null) return;
            DoubleAnimation a = new DoubleAnimation(v, new Duration(TimeSpan.FromMilliseconds(600)));
            bar.BeginAnimation(RangeBase.ValueProperty, a);
        }

        private static void Look(LiveEntry e, bool warn)
        {
            e.Text.Foreground = Ui.Brush(warn ? "#FFB454" : "#E6E8F2");
            if (e.Bar != null) e.Bar.Foreground = Ui.Brush(warn ? "#FF6B6B" : "#4FD1FF");
        }

        private void UpdateLive()
        {
            if (!_active || _live.Count == 0) return;
            _tick++;
            try
            {
                LiveEntry e;
                if (_live.TryGetValue("cpu", out e) && _cpu != null)
                {
                    int v = (int)Math.Min(100, Math.Round(_cpu.NextValue()));
                    e.Text.Text = v + " %"; e.Row.Value = v + " %"; Smooth(e.Bar, v); Look(e, v >= 90);
                }
                if (_live.TryGetValue("ram", out e))
                {
                    MEMORYSTATUSEX m = new MEMORYSTATUSEX(); m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                    if (GlobalMemoryStatusEx(ref m) && m.ullTotalPhys > 0)
                    {
                        double tot = m.ullTotalPhys, free = m.ullAvailPhys;
                        int pct = (int)Math.Round(100 * (tot - free) / tot);
                        string txt = Fmt.Bytes(tot - free) + " of " + Fmt.Bytes(tot) + "  (" + pct + " %)";
                        e.Text.Text = txt; e.Row.Value = txt; Smooth(e.Bar, pct); Look(e, pct >= 90);
                    }
                }
                if (_live.TryGetValue("up", out e) && _boot != DateTime.MinValue)
                {
                    TimeSpan up = DateTime.Now - _boot;
                    string txt = up.Days + " d " + up.Hours + " h " + up.Minutes + " min " + up.Seconds + " s";
                    e.Text.Text = txt; e.Row.Value = txt;
                }
                List<string> keys = new List<string>(_live.Keys);
                List<string> netKeys = keys.FindAll(delegate(string k) { return k.StartsWith("net:"); });
                if (netKeys.Count > 0)
                {
                    double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                    Dictionary<string, NetworkInterface> nics = new Dictionary<string, NetworkInterface>();
                    foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces()) nics[n.Name] = n;
                    foreach (string k in netKeys)
                    {
                        string name = k.Substring(4);
                        NetworkInterface n; if (!nics.TryGetValue(name, out n)) continue;
                        IPv4InterfaceStatistics st = n.GetIPv4Statistics();
                        double rx = st.BytesReceived, tx = st.BytesSent;
                        double[] prev;
                        if (_netPrev.TryGetValue(name, out prev))
                        {
                            double dt = now - prev[2];
                            if (dt > 0.2)
                            {
                                string txt = "Down " + Fmt.Bytes(Math.Max(0, (rx - prev[0]) / dt)) + "/s    Up " + Fmt.Bytes(Math.Max(0, (tx - prev[1]) / dt)) + "/s";
                                _live[k].Text.Text = txt; _live[k].Row.Value = txt;
                            }
                        }
                        _netPrev[name] = new double[] { rx, tx, now };
                    }
                }
                if (_live.TryGetValue("disk", out e) && _diskT != null)
                {
                    double busy = _diskT.NextValue(), rd = _diskR.NextValue(), wr = _diskW.NextValue();
                    int bp = (int)Math.Min(100, Math.Round(busy));
                    string txt = "Read " + Fmt.Bytes(rd) + "/s    Write " + Fmt.Bytes(wr) + "/s    (" + bp + " %)";
                    e.Text.Text = txt; e.Row.Value = txt; Smooth(e.Bar, bp); Look(e, bp >= 95);
                }
                if (_live.TryGetValue("bat", out e) && _tick % 2 == 0)
                {
                    SYSTEM_POWER_STATUS ps;
                    if (GetSystemPowerStatus(out ps) && ps.BatteryLifePercent <= 100)
                    {
                        int lvl = ps.BatteryLifePercent;
                        string state = "Discharging";
                        if (ps.ACLineStatus == 1) state = (ps.BatteryFlag & 8) != 0 ? "Charging" : "On AC power";
                        string txt = lvl + " %  " + state;
                        e.Text.Text = txt; e.Row.Value = txt; Smooth(e.Bar, lvl); Look(e, lvl <= 15 && state == "Discharging");
                    }
                }
                if (_tick % 5 == 0)
                {
                    foreach (string k in keys.FindAll(delegate(string x) { return x.StartsWith("vol:"); }))
                    {
                        LiveEntry ve = _live[k];
                        DriveInfo di = new DriveInfo(k.Substring(4));
                        if (di.IsReady && di.TotalSize > 0)
                        {
                            int pct = (int)Math.Round(100.0 * (di.TotalSize - di.TotalFreeSpace) / di.TotalSize);
                            string txt = Fmt.Bytes(di.TotalFreeSpace) + " free of " + Fmt.Bytes(di.TotalSize) + "  (" + pct + " % used)";
                            ve.Text.Text = txt; ve.Row.Value = txt; Smooth(ve.Bar, pct); Look(ve, pct >= 90);
                        }
                    }
                }
                if (_live.TryGetValue("gpu", out e) && _tick % _gpuEvery == 0)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    double sum = 0;
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\cimv2", "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%engtype_3D%'"))
                    using (ManagementObjectCollection col = s.Get())
                        foreach (ManagementObject o in col) sum += D(o["UtilizationPercentage"]);
                    sw.Stop();
                    _gpuEvery = sw.ElapsedMilliseconds > 300 ? 10 : 3;
                    int gp = (int)Math.Min(100, Math.Round(sum));
                    e.Text.Text = gp + " %"; e.Row.Value = gp + " %"; Smooth(e.Bar, gp); Look(e, gp >= 95);
                }
            }
            catch (ManagementException) { _live.Remove("gpu"); }
            catch { }
        }
    }
}
