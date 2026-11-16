<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RockToolbox.Native
{
    internal sealed class DrvItem : Notify
    {
        private bool _selected;
        public string Title { get; set; }
        public string Kind { get; set; }
        public string DrvClass { get; set; }
        public string Provider { get; set; }
        public string DrvDate { get; set; }
        public string Size { get; set; }
        public string Version { get; set; }
        public string Id { get; set; }
        public string Installed { get; set; }
        public string Latest { get; set; }
        public CatalogHit Hit;
        public HwDriver Hw;
        public string FilePath { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
    }

    // C# port of src\pages\Drivers.ps1: install missing drivers from Windows Update, or remove third-party driver packages.
    internal sealed class DriversPage : IPage
    {
        private Panel _welcome, _install, _uninstall;
        private ListView _lvI, _lvU;
        private TextBlock _countI, _infoI, _countU, _infoU;
        private Button _modeI, _modeU;
        private bool _instLoaded, _unLoaded;

        public string Key { get { return "Drivers"; } }

        public static string KindOf(string cls, string title)
        {
            cls = (cls ?? "").ToLowerInvariant();
            switch (cls)
            {
                case "display": return "Screen / Graphics card";
                case "media": case "audioendpoint": return "Sound / Audio";
                case "net": return "Network (Wi-Fi / Ethernet)";
                case "bluetooth": return "Bluetooth";
                case "camera": return "Camera";
                case "image": return "Camera / Scanner";
                case "hidclass": return "Keyboard / Mouse / Touch";
                case "keyboard": return "Keyboard";
                case "mouse": return "Mouse / Touchpad";
                case "usb": return "USB ports";
                case "system": return "System / Chipset";
                case "scsiadapter": return "Storage (SATA / NVMe)";
                case "hdc": return "Storage controller";
                case "diskdrive": return "Disk drive";
                case "printer": case "printqueue": return "Printer";
                case "monitor": return "Monitor";
                case "battery": return "Battery / Power";
                case "firmware": return "Firmware / BIOS";
                case "processor": return "CPU";
                case "biometric": return "Fingerprint / Face login";
                case "sensor": return "Sensors";
                case "ports": return "COM / LPT ports";
                case "modem": return "Modem";
                case "wpd": return "Phone / Portable device";
                case "softwarecomponent": return "Software component";
                case "extension": return "Driver extension";
            }
            string t = (title ?? "").ToLowerInvariant();
            if (Regex.IsMatch(t, "audio|sound|speaker|microphone|realtek.*hd")) return "Sound / Audio";
            if (Regex.IsMatch(t, "graphics|display|geforce|radeon|nvidia|uhd|iris")) return "Screen / Graphics card";
            if (Regex.IsMatch(t, "wi-?fi|wireless|wlan|ethernet|network|gigabit")) return "Network (Wi-Fi / Ethernet)";
            if (Regex.IsMatch(t, "bluetooth")) return "Bluetooth";
            if (Regex.IsMatch(t, "camera|webcam")) return "Camera";
            if (Regex.IsMatch(t, "touchpad|mouse|keyboard")) return "Keyboard / Mouse / Touch";
            if (Regex.IsMatch(t, "chipset|management engine|serial io|gpio|thermal|platform")) return "System / Chipset";
            if (Regex.IsMatch(t, "nvme|sata|raid|storage")) return "Storage (SATA / NVMe)";
            if (Regex.IsMatch(t, "usb")) return "USB ports";
            if (!string.IsNullOrEmpty(cls)) return cls;
            return "Other";
        }

        public void Init(Window w, Logger log)
        {
            _welcome = Ui.Find<Panel>("pnlDrvWelcome");
            _install = Ui.Find<Panel>("pnlDrvInstall");
            _uninstall = Ui.Find<Panel>("pnlDrvUninstall");
            _lvI = Ui.Find<ListView>("lvDrvInstall");
            _lvU = Ui.Find<ListView>("lvDrvUn");
            _countI = Ui.Find<TextBlock>("txtDrvCount");
            _infoI = Ui.Find<TextBlock>("txtDrvInfo");
            _countU = Ui.Find<TextBlock>("txtDrvUCount");
            _infoU = Ui.Find<TextBlock>("txtDrvUInfo");
            _modeI = Ui.Find<Button>("btnDrvModeInstall");
            _modeU = Ui.Find<Button>("btnDrvModeUninstall");

            _modeI.Click += delegate { ShowMode("Install"); };
            _modeU.Click += delegate { ShowMode("Uninstall"); };
            Ui.Find<Button>("btnDrvPickInstall").Click += delegate { ShowMode("Install"); };
            Ui.Find<Button>("btnDrvPickUninstall").Click += delegate { ShowMode("Uninstall"); };
            Ui.Find<Button>("btnDrvScan").Click += delegate { ScanInstall(); };
            Ui.Find<Button>("btnDrvSelAll").Click += delegate { foreach (DrvItem d in Items(_lvI)) d.Selected = true; CountI(); };
            Ui.Find<Button>("btnDrvSelNone").Click += delegate { foreach (DrvItem d in Items(_lvI)) d.Selected = false; CountI(); };
            Ui.Find<Button>("btnDrvInstall").Click += delegate { InstallSelected(); };
            Ui.Find<Button>("btnDrvUScan").Click += delegate { ScanUninstall(); };
            Ui.Find<Button>("btnDrvUSelNone").Click += delegate { foreach (DrvItem d in Items(_lvU)) d.Selected = false; CountU(); };
            Ui.Find<Button>("btnDrvUninstall").Click += delegate { UninstallSelected(); };

            _lvI.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { CountI(); }));
            _lvU.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate(object s, RoutedEventArgs e)
            {
                Button b = e.OriginalSource as Button;
                if (b != null)
                {
                    e.Handled = true;
                    string fp = Convert.ToString(b.Tag);
                    try
                    {
                        if (fp.Length > 0 && File.Exists(fp)) Tool.Start("explorer.exe", "/select,\"" + fp + "\"");
                        else if (fp.Length > 0 && Directory.Exists(Path.GetDirectoryName(fp))) Tool.Start("explorer.exe", "\"" + Path.GetDirectoryName(fp) + "\"");
                        else Ui.Log.Warn("Could not find the folder of this driver.", "Drivers");
                    }
                    catch (Exception ex) { Ui.Log.Warn("Could not open the folder: " + ex.Message, "Drivers"); }
                    return;
                }
                CountU();
            }));
            _lvI.MouseDoubleClick += delegate { DrvItem it = _lvI.SelectedItem as DrvItem; if (it != null) { it.Selected = !it.Selected; CountI(); } };
            _lvU.MouseDoubleClick += delegate { DrvItem it = _lvU.SelectedItem as DrvItem; if (it != null) { it.Selected = !it.Selected; CountU(); } };
            ShowMode("");
        }

        public void OnShown() { }

        private static List<DrvItem> Items(ListView lv) { return (lv.ItemsSource as List<DrvItem>) ?? new List<DrvItem>(); }

        private void CountI() { int n = 0; List<DrvItem> a = Items(_lvI); foreach (DrvItem d in a) if (d.Selected) n++; _countI.Text = "Selected: " + n + " of " + a.Count; }
        private void CountU() { int n = 0; List<DrvItem> a = Items(_lvU); foreach (DrvItem d in a) if (d.Selected) n++; _countU.Text = "Selected: " + n + " of " + a.Count; }

        private void ShowMode(string mode)
        {
            _welcome.Visibility = Visibility.Collapsed; _install.Visibility = Visibility.Collapsed; _uninstall.Visibility = Visibility.Collapsed;
            if (mode == "Install") { _install.Visibility = Visibility.Visible; if (!_instLoaded) ScanInstall(); }
            else if (mode == "Uninstall") { _uninstall.Visibility = Visibility.Visible; if (!_unLoaded) ScanUninstall(); }
            else _welcome.Visibility = Visibility.Visible;
            _modeI.Background = Ui.Brush(mode == "Install" ? "#4A78B0" : "#1E3547");
            _modeI.Foreground = Ui.Brush(mode == "Install" ? "#FFFFFF" : "#F0F4FF");
            _modeU.Background = Ui.Brush(mode == "Uninstall" ? "#4A78B0" : "#1E3547");
            _modeU.Foreground = Ui.Brush(mode == "Uninstall" ? "#FFFFFF" : "#F0F4FF");
        }

        // ---------------- install ----------------
        private void ScanInstall()
        {
            Work.Run("Looking for drivers...", delegate(WorkCtx c) { DoScanInstall(c); });
        }

        private static string VerDate(string ver, DateTime d)
        {
            string t = (ver ?? "").Trim();
            string ds = DriverCatalog.DateText(d);
            if (ds.Length > 0) t += (t.Length > 0 ? "  (" : "(") + ds + ")";
            return t.Length > 0 ? t : "unknown";
        }

        private static bool IsNewer(HwDriver d, CatalogHit h)
        {
            if (h == null) return false;
            if (string.IsNullOrEmpty(d.VersionText) && d.Ver == null && d.Date == DateTime.MinValue) return true;
            if (d.Ver != null && h.Ver != null) return h.Ver > d.Ver || (h.Ver == d.Ver && h.Date > d.Date.AddDays(1));
            if (h.Date != DateTime.MinValue && d.Date != DateTime.MinValue) return h.Date > d.Date.AddDays(1);
            return false;
        }

        // devices Windows lists with a problem (code 28 = no driver, 10/43 = failed) - they are not in Win32_PnPSignedDriver with a real driver
        private static List<HwDriver> ScanMissing()
        {
            List<HwDriver> res = new List<HwDriver>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, PNPClass, Manufacturer, DeviceID, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject d in col)
                    {
                        string[] ids = d["HardwareID"] as string[];
                        if (ids == null || ids.Length == 0) continue;
                        string did = Convert.ToString(d["DeviceID"]);
                        if (did.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase) || did.StartsWith("SWD\\", StringComparison.OrdinalIgnoreCase)) continue;
                        HwDriver h = new HwDriver();
                        h.Name = Convert.ToString(d["Name"]); if (string.IsNullOrEmpty(h.Name)) h.Name = "Unknown device";
                        h.Cls = Convert.ToString(d["PNPClass"]); h.Category = string.IsNullOrEmpty(h.Cls) ? "Other" : h.Cls;
                        h.Maker = Convert.ToString(d["Manufacturer"]); h.DeviceId = did;
                        List<string> l = new List<string>(ids);
                        l.Sort(delegate(string a, string b) { return a.Length.CompareTo(b.Length); });
                        h.Ids = l; h.Key = l[0];
                        res.Add(h);
                    }
                }
            }
            catch { }
            return res;
        }

        private void DoScanInstall(WorkCtx c)
        {
            c.Info("Step 1/3: reading the hardware and the installed drivers...", "Drivers");
            c.Progress(-1, "Reading the hardware of this PC...");
            List<HwDriver> devs = DriverCatalog.ScanInstalled();
            c.Info(devs.Count + " devices with a driver found.", "Drivers");
            List<HwDriver> missing = ScanMissing();
            if (missing.Count > 0) { c.Info(missing.Count + " device(s) have no working driver.", "Drivers"); devs.AddRange(missing); }
            c.ThrowIfCancelled();
            c.Info("Step 2/3: checking the Microsoft Update Catalog for newer versions (not Windows Update)...", "Drivers");
            Exception net;
            Dictionary<string, CatalogHit> latest = DriverCatalog.LookupAll(c, devs, out net);
            c.ThrowIfCancelled();
            c.Info("Step 3/3: comparing versions...", "Drivers");
            List<DrvItem> list = new List<DrvItem>();
            int upToDate = 0, unknown = 0;
            foreach (HwDriver d in devs)
            {
                if (d.Key.Length == 0) { unknown++; continue; }
                CatalogHit h;
                if (!latest.TryGetValue(d.Key, out h)) { unknown++; continue; }
                if (!IsNewer(d, h)) { upToDate++; continue; }
                DrvItem o = new DrvItem();
                o.Hw = d; o.Hit = h; o.Id = h.UpdateId;
                o.Installed = VerDate(d.VersionText, d.Date);
                o.Latest = VerDate(h.VersionText, h.Date);
                o.Title = "Update for " + d.Name + ": from " + (string.IsNullOrEmpty(d.VersionText) ? "unknown" : d.VersionText) + " to " + (string.IsNullOrEmpty(h.VersionText) ? "newer" : h.VersionText);
                o.Kind = d.Category; o.Provider = d.Maker; o.DrvClass = d.Cls; o.DrvDate = DriverCatalog.DateText(h.Date);
                list.Add(o);
                c.Info("  " + o.Title + "   [" + o.Installed + " -> " + o.Latest + "]", "Drivers");
            }
            foreach (DrvItem m in list) if (m.Hw != null && m.Hw.Ver == null && m.Hw.VersionText.Length == 0) m.Title = "Missing driver: " + m.Hw.Name;
            int wuCount = 0;
            try
            {
                c.Info("Also checking Windows Update for drivers...", "Drivers");
                foreach (WuRow r in WuApi.ScanDrivers(c))
                {
                    DrvItem o = new DrvItem();
                    o.Id = r.Id; o.Title = "Windows Update: " + r.Title; o.Kind = string.IsNullOrEmpty(r.Type) ? "Windows Update" : r.Type;
                    o.Provider = r.Category; o.DrvClass = r.Type; o.DrvDate = r.KB; o.Size = r.Size; o.Installed = "-"; o.Latest = r.KB;
                    list.Add(o); wuCount++;
                    c.Info("  " + o.Title, "Drivers");
                }
            }
            catch (Exception wex) { c.Warn("Windows Update drivers could not be checked: " + wex.Message, "Drivers"); }
            list.Sort(delegate(DrvItem a, DrvItem b) { return string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase); });
            string msg;
            if (net != null) msg = "Could not reach the Microsoft Update Catalog: " + Lang.L(Net.FriendlyError(net)) + "  (" + list.Count + " updates found before the connection failed)";
            else if (list.Count == 0) msg = "No driver is missing or outdated (" + upToDate + " devices are up to date). Windows already uses its built-in drivers for " + unknown + " devices.";
            else msg = list.Count + " driver update(s) found.  " + upToDate + " devices are up to date, " + unknown + " have no catalog driver.";
            c.UI(delegate { _lvI.ItemsSource = list; _instLoaded = true; CountI(); _infoI.Text = msg; });
            c.Info(msg, "Drivers");
            c.Progress(100, null);
        }

        private void InstallSelected()
        {
            List<DrvItem> sel = new List<DrvItem>();
            foreach (DrvItem d in Items(_lvI)) if (d.Selected) sel.Add(d);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the drivers you want to install first.", "Drivers"); return; }
            Work.Run("Installing drivers...", delegate(WorkCtx c)
            {
                if (!Safety.NewRestorePoint(c, "installing drivers")) throw new OperationCanceledException();
                int ok = 0, fail = 0, n = sel.Count, i = 0; bool reboot = false;
                List<string> wuIds = new List<string>();
                foreach (DrvItem w in sel) if (w.Hit == null && !string.IsNullOrEmpty(w.Id)) wuIds.Add(w.Id);
                if (wuIds.Count > 0)
                {
                    c.Info("Installing " + wuIds.Count + " driver(s) from Windows Update...", "Drivers");
                    try { WuResult wr = WuApi.Install(c, wuIds); ok += wr.Ok; fail += wr.Fail; if (wr.Reboot) reboot = true; }
                    catch (Exception wex) { fail += wuIds.Count; c.Warn("  Windows Update failed: " + wex.Message, "Drivers"); }
                }
                foreach (DrvItem d in sel)
                {
                    if (d.Hit == null) continue;
                    c.ThrowIfCancelled();
                    i++;
                    c.Progress((i - 1) * 100.0 / n, "Installing (" + i + "/" + n + "): " + d.Hw.Name);
                    c.Info("Installing (" + i + "/" + n + "): " + d.Title, "Drivers");
                    bool rb; string err;
                    if (DriverCatalog.InstallUpdate(c, d.Hw.Name, d.Hit, out rb, out err)) { ok++; if (rb) reboot = true; c.Info("  installed.", "Drivers"); }
                    else { fail++; c.Warn("  failed: " + err, "Drivers"); }
                    c.Progress(i * 100.0 / n, null);
                }
                c.Info("Finished: " + ok + " installed, " + fail + " failed.", "Drivers");
                if (reboot) c.Warn("Restart your PC to finish installing.", "Drivers");
                DoScanInstall(c);
            });
        }

        // ---------------- uninstall ----------------
        private void ScanUninstall() { Work.Run("Reading installed drivers...", delegate(WorkCtx c) { DoScanUninstall(c); }); }

        private void DoScanUninstall(WorkCtx c)
        {
            c.Info("Reading the installed drivers (this can take up to a minute)...", "Drivers");
            c.Progress(-1, "Reading installed drivers...");
            Dictionary<string, DrvItem> map = new Dictionary<string, DrvItem>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> extra = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string inf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT DeviceName, InfName, DriverProviderName, DeviceClass, DriverVersion, DriverDate FROM Win32_PnPSignedDriver"))
            using (ManagementObjectCollection col = s.Get())
            {
                foreach (ManagementObject d in col)
                {
                    string name = Convert.ToString(d["InfName"]);
                    if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^oem\d+\.inf$", RegexOptions.IgnoreCase)) continue;
                    if (map.ContainsKey(name)) { extra[name] = (extra.ContainsKey(name) ? extra[name] : 0) + 1; continue; }
                    DrvItem o = new DrvItem();
                    o.Title = Convert.ToString(d["DeviceName"]);
                    if (string.IsNullOrEmpty(o.Title)) o.Title = name;
                    o.Provider = Convert.ToString(d["DriverProviderName"]);
                    o.DrvClass = Convert.ToString(d["DeviceClass"]);
                    o.Version = Convert.ToString(d["DriverVersion"]);
                    try { o.DrvDate = ManagementDateTimeConverter.ToDateTime(Convert.ToString(d["DriverDate"])).ToString("yyyy-MM-dd"); } catch { o.DrvDate = ""; }
                    o.Id = name;
                    o.FilePath = Path.Combine(inf, name);
                    o.Kind = KindOf(o.DrvClass, o.Title);
                    map[name] = o;
                }
            }
            List<DrvItem> list = new List<DrvItem>(map.Values);
            foreach (DrvItem o in list) if (extra.ContainsKey(o.Id)) o.Title += "  (+" + extra[o.Id] + " more)";
            list.Sort(delegate(DrvItem a, DrvItem b)
            {
                int x = string.Compare(a.Provider, b.Provider, StringComparison.OrdinalIgnoreCase);
                return x != 0 ? x : string.Compare(a.DrvClass, b.DrvClass, StringComparison.OrdinalIgnoreCase);
            });
            c.UI(delegate
            {
                _lvU.ItemsSource = list; _unLoaded = true; CountU();
                _infoU.Text = list.Count + " third-party driver packages. Removing a driver can stop a device from working - create a restore point first.";
            });
            c.Info(list.Count + " driver packages found.", "Drivers");
        }

        private void UninstallSelected()
        {
            List<DrvItem> sel = new List<DrvItem>();
            foreach (DrvItem d in Items(_lvU)) if (d.Selected) sel.Add(d);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the drivers you want to remove first.", "Drivers"); return; }
            string names = "";
            for (int i = 0; i < sel.Count && i < 8; i++) names += "- " + sel[i].Provider + " / " + sel[i].Title + "\n";
            if (sel.Count > 8) names += "... and " + (sel.Count - 8) + " more\n";
            if (!Ui.Confirm("Remove " + sel.Count + " driver package(s)?\n\n" + names + "\nA removed driver can stop its device from working until you install it again.")) return;
            List<string> infs = new List<string>();
            foreach (DrvItem d in sel) infs.Add(d.Id);
            Work.Run("Removing drivers...", delegate(WorkCtx c)
            {
                if (!Safety.NewRestorePoint(c, "removing drivers")) throw new OperationCanceledException();
                int ok = 0, fail = 0, n = infs.Count, i = 0;
                foreach (string f in infs)
                {
                    if (c.Cancelled) { c.Warn("Stopped - the remaining drivers were skipped.", "Drivers"); break; }
                    i++;
                    c.Progress((i - 1) * 100.0 / n, "Removing driver (" + i + "/" + n + "): " + f);
                    c.Info("Removing driver (" + i + "/" + n + "): " + f, "Drivers");
                    try
                    {
                        int code;
                        string outp = Tool.Capture(Tool.System32("pnputil.exe"), "/delete-driver " + f + " /uninstall /force", out code);
                        foreach (string l in outp.Split('\n')) { string t = l.Trim(); if (t.Length > 0) c.Info("  " + t, "Drivers"); }
                        if (code == 0 || code == 3010) ok++; else fail++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { fail++; c.Warn("  failed: " + ex.Message, "Drivers"); }
                    c.Progress(i * 100.0 / n, null);
                }
                c.Info("Finished: " + ok + " removed, " + fail + " failed.", "Drivers");
                DoScanUninstall(c);
            });
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RockToolbox.Native
{
    internal sealed class DrvItem : Notify
    {
        private bool _selected;
        public string Title { get; set; }
        public string Kind { get; set; }
        public string DrvClass { get; set; }
        public string Provider { get; set; }
        public string DrvDate { get; set; }
        public string Size { get; set; }
        public string Version { get; set; }
        public string Id { get; set; }
        public string Installed { get; set; }
        public string Latest { get; set; }
        public CatalogHit Hit;
        public HwDriver Hw;
        public string FilePath { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
    }

    // C# port of src\pages\Drivers.ps1: install missing drivers from Windows Update, or remove third-party driver packages.
    internal sealed class DriversPage : IPage
    {
        private Panel _welcome, _install, _uninstall;
        private ListView _lvI, _lvU;
        private TextBlock _countI, _infoI, _countU, _infoU;
        private Button _modeI, _modeU;
        private bool _instLoaded, _unLoaded;

        public string Key { get { return "Drivers"; } }

        public static string KindOf(string cls, string title)
        {
            cls = (cls ?? "").ToLowerInvariant();
            switch (cls)
            {
                case "display": return "Screen / Graphics card";
                case "media": case "audioendpoint": return "Sound / Audio";
                case "net": return "Network (Wi-Fi / Ethernet)";
                case "bluetooth": return "Bluetooth";
                case "camera": return "Camera";
                case "image": return "Camera / Scanner";
                case "hidclass": return "Keyboard / Mouse / Touch";
                case "keyboard": return "Keyboard";
                case "mouse": return "Mouse / Touchpad";
                case "usb": return "USB ports";
                case "system": return "System / Chipset";
                case "scsiadapter": return "Storage (SATA / NVMe)";
                case "hdc": return "Storage controller";
                case "diskdrive": return "Disk drive";
                case "printer": case "printqueue": return "Printer";
                case "monitor": return "Monitor";
                case "battery": return "Battery / Power";
                case "firmware": return "Firmware / BIOS";
                case "processor": return "CPU";
                case "biometric": return "Fingerprint / Face login";
                case "sensor": return "Sensors";
                case "ports": return "COM / LPT ports";
                case "modem": return "Modem";
                case "wpd": return "Phone / Portable device";
                case "softwarecomponent": return "Software component";
                case "extension": return "Driver extension";
            }
            string t = (title ?? "").ToLowerInvariant();
            if (Regex.IsMatch(t, "audio|sound|speaker|microphone|realtek.*hd")) return "Sound / Audio";
            if (Regex.IsMatch(t, "graphics|display|geforce|radeon|nvidia|uhd|iris")) return "Screen / Graphics card";
            if (Regex.IsMatch(t, "wi-?fi|wireless|wlan|ethernet|network|gigabit")) return "Network (Wi-Fi / Ethernet)";
            if (Regex.IsMatch(t, "bluetooth")) return "Bluetooth";
            if (Regex.IsMatch(t, "camera|webcam")) return "Camera";
            if (Regex.IsMatch(t, "touchpad|mouse|keyboard")) return "Keyboard / Mouse / Touch";
            if (Regex.IsMatch(t, "chipset|management engine|serial io|gpio|thermal|platform")) return "System / Chipset";
            if (Regex.IsMatch(t, "nvme|sata|raid|storage")) return "Storage (SATA / NVMe)";
            if (Regex.IsMatch(t, "usb")) return "USB ports";
            if (!string.IsNullOrEmpty(cls)) return cls;
            return "Other";
        }

        public void Init(Window w, Logger log)
        {
            _welcome = Ui.Find<Panel>("pnlDrvWelcome");
            _install = Ui.Find<Panel>("pnlDrvInstall");
            _uninstall = Ui.Find<Panel>("pnlDrvUninstall");
            _lvI = Ui.Find<ListView>("lvDrvInstall");
            _lvU = Ui.Find<ListView>("lvDrvUn");
            _countI = Ui.Find<TextBlock>("txtDrvCount");
            _infoI = Ui.Find<TextBlock>("txtDrvInfo");
            _countU = Ui.Find<TextBlock>("txtDrvUCount");
            _infoU = Ui.Find<TextBlock>("txtDrvUInfo");
            _modeI = Ui.Find<Button>("btnDrvModeInstall");
            _modeU = Ui.Find<Button>("btnDrvModeUninstall");

            _modeI.Click += delegate { ShowMode("Install"); };
            _modeU.Click += delegate { ShowMode("Uninstall"); };
            Ui.Find<Button>("btnDrvPickInstall").Click += delegate { ShowMode("Install"); };
            Ui.Find<Button>("btnDrvPickUninstall").Click += delegate { ShowMode("Uninstall"); };
            Ui.Find<Button>("btnDrvScan").Click += delegate { ScanInstall(); };
            Ui.Find<Button>("btnDrvSelAll").Click += delegate { foreach (DrvItem d in Items(_lvI)) d.Selected = true; CountI(); };
            Ui.Find<Button>("btnDrvSelNone").Click += delegate { foreach (DrvItem d in Items(_lvI)) d.Selected = false; CountI(); };
            Ui.Find<Button>("btnDrvInstall").Click += delegate { InstallSelected(); };
            Ui.Find<Button>("btnDrvUScan").Click += delegate { ScanUninstall(); };
            Ui.Find<Button>("btnDrvUSelNone").Click += delegate { foreach (DrvItem d in Items(_lvU)) d.Selected = false; CountU(); };
            Ui.Find<Button>("btnDrvUninstall").Click += delegate { UninstallSelected(); };

            _lvI.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { CountI(); }));
            _lvU.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate(object s, RoutedEventArgs e)
            {
                Button b = e.OriginalSource as Button;
                if (b != null)
                {
                    e.Handled = true;
                    string fp = Convert.ToString(b.Tag);
                    try
                    {
                        if (fp.Length > 0 && File.Exists(fp)) Tool.Start("explorer.exe", "/select,\"" + fp + "\"");
                        else if (fp.Length > 0 && Directory.Exists(Path.GetDirectoryName(fp))) Tool.Start("explorer.exe", "\"" + Path.GetDirectoryName(fp) + "\"");
                        else Ui.Log.Warn("Could not find the folder of this driver.", "Drivers");
                    }
                    catch (Exception ex) { Ui.Log.Warn("Could not open the folder: " + ex.Message, "Drivers"); }
                    return;
                }
                CountU();
            }));
            _lvI.MouseDoubleClick += delegate { DrvItem it = _lvI.SelectedItem as DrvItem; if (it != null) { it.Selected = !it.Selected; CountI(); } };
            _lvU.MouseDoubleClick += delegate { DrvItem it = _lvU.SelectedItem as DrvItem; if (it != null) { it.Selected = !it.Selected; CountU(); } };
            ShowMode("");
        }

        public void OnShown() { }

        private static List<DrvItem> Items(ListView lv) { return (lv.ItemsSource as List<DrvItem>) ?? new List<DrvItem>(); }

        private void CountI() { int n = 0; List<DrvItem> a = Items(_lvI); foreach (DrvItem d in a) if (d.Selected) n++; _countI.Text = "Selected: " + n + " of " + a.Count; }
        private void CountU() { int n = 0; List<DrvItem> a = Items(_lvU); foreach (DrvItem d in a) if (d.Selected) n++; _countU.Text = "Selected: " + n + " of " + a.Count; }

        private void ShowMode(string mode)
        {
            _welcome.Visibility = Visibility.Collapsed; _install.Visibility = Visibility.Collapsed; _uninstall.Visibility = Visibility.Collapsed;
            if (mode == "Install") { _install.Visibility = Visibility.Visible; if (!_instLoaded) ScanInstall(); }
            else if (mode == "Uninstall") { _uninstall.Visibility = Visibility.Visible; if (!_unLoaded) ScanUninstall(); }
            else _welcome.Visibility = Visibility.Visible;
            _modeI.Background = Ui.Brush(mode == "Install" ? "#4A78B0" : "#1E3547");
            _modeI.Foreground = Ui.Brush(mode == "Install" ? "#FFFFFF" : "#F0F4FF");
            _modeU.Background = Ui.Brush(mode == "Uninstall" ? "#4A78B0" : "#1E3547");
            _modeU.Foreground = Ui.Brush(mode == "Uninstall" ? "#FFFFFF" : "#F0F4FF");
        }

        // ---------------- install ----------------
        private void ScanInstall()
        {
            Work.Run("Looking for drivers...", delegate(WorkCtx c) { DoScanInstall(c); });
        }

        private static string VerDate(string ver, DateTime d)
        {
            string t = (ver ?? "").Trim();
            string ds = DriverCatalog.DateText(d);
            if (ds.Length > 0) t += (t.Length > 0 ? "  (" : "(") + ds + ")";
            return t.Length > 0 ? t : "unknown";
        }

        private static bool IsNewer(HwDriver d, CatalogHit h)
        {
            if (h == null) return false;
            if (string.IsNullOrEmpty(d.VersionText) && d.Ver == null && d.Date == DateTime.MinValue) return true;
            if (d.Ver != null && h.Ver != null) return h.Ver > d.Ver || (h.Ver == d.Ver && h.Date > d.Date.AddDays(1));
            if (h.Date != DateTime.MinValue && d.Date != DateTime.MinValue) return h.Date > d.Date.AddDays(1);
            return false;
        }

        // devices Windows lists with a problem (code 28 = no driver, 10/43 = failed) - they are not in Win32_PnPSignedDriver with a real driver
        private static List<HwDriver> ScanMissing()
        {
            List<HwDriver> res = new List<HwDriver>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, PNPClass, Manufacturer, DeviceID, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject d in col)
                    {
                        string[] ids = d["HardwareID"] as string[];
                        if (ids == null || ids.Length == 0) continue;
                        string did = Convert.ToString(d["DeviceID"]);
                        if (did.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase) || did.StartsWith("SWD\\", StringComparison.OrdinalIgnoreCase)) continue;
                        HwDriver h = new HwDriver();
                        h.Name = Convert.ToString(d["Name"]); if (string.IsNullOrEmpty(h.Name)) h.Name = "Unknown device";
                        h.Cls = Convert.ToString(d["PNPClass"]); h.Category = string.IsNullOrEmpty(h.Cls) ? "Other" : h.Cls;
                        h.Maker = Convert.ToString(d["Manufacturer"]); h.DeviceId = did;
                        List<string> l = new List<string>(ids);
                        l.Sort(delegate(string a, string b) { return a.Length.CompareTo(b.Length); });
                        h.Ids = l; h.Key = l[0];
                        res.Add(h);
                    }
                }
            }
            catch { }
            return res;
        }

        private void DoScanInstall(WorkCtx c)
        {
            c.Info("Step 1/3: reading the hardware and the installed drivers...", "Drivers");
            c.Progress(-1, "Reading the hardware of this PC...");
            List<HwDriver> devs = DriverCatalog.ScanInstalled();
            c.Info(devs.Count + " devices with a driver found.", "Drivers");
            List<HwDriver> missing = ScanMissing();
            if (missing.Count > 0) { c.Info(missing.Count + " device(s) have no working driver.", "Drivers"); devs.AddRange(missing); }
            c.ThrowIfCancelled();
            c.Info("Step 2/3: checking the Microsoft Update Catalog for newer versions (not Windows Update)...", "Drivers");
            Exception net;
            Dictionary<string, CatalogHit> latest = DriverCatalog.LookupAll(c, devs, out net);
            c.ThrowIfCancelled();
            c.Info("Step 3/3: comparing versions...", "Drivers");
            List<DrvItem> list = new List<DrvItem>();
            int upToDate = 0, unknown = 0;
            foreach (HwDriver d in devs)
            {
                if (d.Key.Length == 0) { unknown++; continue; }
                CatalogHit h;
                if (!latest.TryGetValue(d.Key, out h)) { unknown++; continue; }
                if (!IsNewer(d, h)) { upToDate++; continue; }
                DrvItem o = new DrvItem();
                o.Hw = d; o.Hit = h; o.Id = h.UpdateId;
                o.Installed = VerDate(d.VersionText, d.Date);
                o.Latest = VerDate(h.VersionText, h.Date);
                o.Title = "Update for " + d.Name + ": from " + (string.IsNullOrEmpty(d.VersionText) ? "unknown" : d.VersionText) + " to " + (string.IsNullOrEmpty(h.VersionText) ? "newer" : h.VersionText);
                o.Kind = d.Category; o.Provider = d.Maker; o.DrvClass = d.Cls; o.DrvDate = DriverCatalog.DateText(h.Date);
                list.Add(o);
                c.Info("  " + o.Title + "   [" + o.Installed + " -> " + o.Latest + "]", "Drivers");
            }
            foreach (DrvItem m in list) if (m.Hw != null && m.Hw.Ver == null && m.Hw.VersionText.Length == 0) m.Title = "Missing driver: " + m.Hw.Name;
            int wuCount = 0;
            try
            {
                c.Info("Also checking Windows Update for drivers...", "Drivers");
                foreach (WuRow r in WuApi.ScanDrivers(c))
                {
                    DrvItem o = new DrvItem();
                    o.Id = r.Id; o.Title = "Windows Update: " + r.Title; o.Kind = string.IsNullOrEmpty(r.Type) ? "Windows Update" : r.Type;
                    o.Provider = r.Category; o.DrvClass = r.Type; o.DrvDate = r.KB; o.Size = r.Size; o.Installed = "-"; o.Latest = r.KB;
                    list.Add(o); wuCount++;
                    c.Info("  " + o.Title, "Drivers");
                }
            }
            catch (Exception wex) { c.Warn("Windows Update drivers could not be checked: " + wex.Message, "Drivers"); }
            list.Sort(delegate(DrvItem a, DrvItem b) { return string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase); });
            string msg;
            if (net != null) msg = "Could not reach the Microsoft Update Catalog: " + Lang.L(Net.FriendlyError(net)) + "  (" + list.Count + " updates found before the connection failed)";
            else if (list.Count == 0) msg = "No driver is missing or outdated (" + upToDate + " devices are up to date). Windows already uses its built-in drivers for " + unknown + " devices.";
            else msg = list.Count + " driver update(s) found.  " + upToDate + " devices are up to date, " + unknown + " have no catalog driver.";
            c.UI(delegate { _lvI.ItemsSource = list; _instLoaded = true; CountI(); _infoI.Text = msg; });
            c.Info(msg, "Drivers");
            c.Progress(100, null);
        }

        private void InstallSelected()
        {
            List<DrvItem> sel = new List<DrvItem>();
            foreach (DrvItem d in Items(_lvI)) if (d.Selected) sel.Add(d);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the drivers you want to install first.", "Drivers"); return; }
            Work.Run("Installing drivers...", delegate(WorkCtx c)
            {
                if (!Safety.NewRestorePoint(c, "installing drivers")) throw new OperationCanceledException();
                int ok = 0, fail = 0, n = sel.Count, i = 0; bool reboot = false;
                List<string> wuIds = new List<string>();
                foreach (DrvItem w in sel) if (w.Hit == null && !string.IsNullOrEmpty(w.Id)) wuIds.Add(w.Id);
                if (wuIds.Count > 0)
                {
                    c.Info("Installing " + wuIds.Count + " driver(s) from Windows Update...", "Drivers");
                    try { WuResult wr = WuApi.Install(c, wuIds); ok += wr.Ok; fail += wr.Fail; if (wr.Reboot) reboot = true; }
                    catch (Exception wex) { fail += wuIds.Count; c.Warn("  Windows Update failed: " + wex.Message, "Drivers"); }
                }
                foreach (DrvItem d in sel)
                {
                    if (d.Hit == null) continue;
                    c.ThrowIfCancelled();
                    i++;
                    c.Progress((i - 1) * 100.0 / n, "Installing (" + i + "/" + n + "): " + d.Hw.Name);
                    c.Info("Installing (" + i + "/" + n + "): " + d.Title, "Drivers");
                    bool rb; string err;
                    if (DriverCatalog.InstallUpdate(c, d.Hw.Name, d.Hit, out rb, out err)) { ok++; if (rb) reboot = true; c.Info("  installed.", "Drivers"); }
                    else { fail++; c.Warn("  failed: " + err, "Drivers"); }
                    c.Progress(i * 100.0 / n, null);
                }
                c.Info("Finished: " + ok + " installed, " + fail + " failed.", "Drivers");
                if (reboot) c.Warn("Restart your PC to finish installing.", "Drivers");
                DoScanInstall(c);
            });
        }

        // ---------------- uninstall ----------------
        private void ScanUninstall() { Work.Run("Reading installed drivers...", delegate(WorkCtx c) { DoScanUninstall(c); }); }

        private void DoScanUninstall(WorkCtx c)
        {
            c.Info("Reading the installed drivers (this can take up to a minute)...", "Drivers");
            c.Progress(-1, "Reading installed drivers...");
            Dictionary<string, DrvItem> map = new Dictionary<string, DrvItem>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> extra = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string inf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT DeviceName, InfName, DriverProviderName, DeviceClass, DriverVersion, DriverDate FROM Win32_PnPSignedDriver"))
            using (ManagementObjectCollection col = s.Get())
            {
                foreach (ManagementObject d in col)
                {
                    string name = Convert.ToString(d["InfName"]);
                    if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^oem\d+\.inf$", RegexOptions.IgnoreCase)) continue;
                    if (map.ContainsKey(name)) { extra[name] = (extra.ContainsKey(name) ? extra[name] : 0) + 1; continue; }
                    DrvItem o = new DrvItem();
                    o.Title = Convert.ToString(d["DeviceName"]);
                    if (string.IsNullOrEmpty(o.Title)) o.Title = name;
                    o.Provider = Convert.ToString(d["DriverProviderName"]);
                    o.DrvClass = Convert.ToString(d["DeviceClass"]);
                    o.Version = Convert.ToString(d["DriverVersion"]);
                    try { o.DrvDate = ManagementDateTimeConverter.ToDateTime(Convert.ToString(d["DriverDate"])).ToString("yyyy-MM-dd"); } catch { o.DrvDate = ""; }
                    o.Id = name;
                    o.FilePath = Path.Combine(inf, name);
                    o.Kind = KindOf(o.DrvClass, o.Title);
                    map[name] = o;
                }
            }
            List<DrvItem> list = new List<DrvItem>(map.Values);
            foreach (DrvItem o in list) if (extra.ContainsKey(o.Id)) o.Title += "  (+" + extra[o.Id] + " more)";
            list.Sort(delegate(DrvItem a, DrvItem b)
            {
                int x = string.Compare(a.Provider, b.Provider, StringComparison.OrdinalIgnoreCase);
                return x != 0 ? x : string.Compare(a.DrvClass, b.DrvClass, StringComparison.OrdinalIgnoreCase);
            });
            c.UI(delegate
            {
                _lvU.ItemsSource = list; _unLoaded = true; CountU();
                _infoU.Text = list.Count + " third-party driver packages. Removing a driver can stop a device from working - create a restore point first.";
            });
            c.Info(list.Count + " driver packages found.", "Drivers");
        }

        private void UninstallSelected()
        {
            List<DrvItem> sel = new List<DrvItem>();
            foreach (DrvItem d in Items(_lvU)) if (d.Selected) sel.Add(d);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the drivers you want to remove first.", "Drivers"); return; }
            string names = "";
            for (int i = 0; i < sel.Count && i < 8; i++) names += "- " + sel[i].Provider + " / " + sel[i].Title + "\n";
            if (sel.Count > 8) names += "... and " + (sel.Count - 8) + " more\n";
            if (!Ui.Confirm("Remove " + sel.Count + " driver package(s)?\n\n" + names + "\nA removed driver can stop its device from working until you install it again.")) return;
            List<string> infs = new List<string>();
            foreach (DrvItem d in sel) infs.Add(d.Id);
            Work.Run("Removing drivers...", delegate(WorkCtx c)
            {
                if (!Safety.NewRestorePoint(c, "removing drivers")) throw new OperationCanceledException();
                int ok = 0, fail = 0, n = infs.Count, i = 0;
                foreach (string f in infs)
                {
                    if (c.Cancelled) { c.Warn("Stopped - the remaining drivers were skipped.", "Drivers"); break; }
                    i++;
                    c.Progress((i - 1) * 100.0 / n, "Removing driver (" + i + "/" + n + "): " + f);
                    c.Info("Removing driver (" + i + "/" + n + "): " + f, "Drivers");
                    try
                    {
                        int code;
                        string outp = Tool.Capture(Tool.System32("pnputil.exe"), "/delete-driver " + f + " /uninstall /force", out code);
                        foreach (string l in outp.Split('\n')) { string t = l.Trim(); if (t.Length > 0) c.Info("  " + t, "Drivers"); }
                        if (code == 0 || code == 3010) ok++; else fail++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { fail++; c.Warn("  failed: " + ex.Message, "Drivers"); }
                    c.Progress(i * 100.0 / n, null);
                }
                c.Info("Finished: " + ok + " removed, " + fail + " failed.", "Drivers");
                DoScanUninstall(c);
            });
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
