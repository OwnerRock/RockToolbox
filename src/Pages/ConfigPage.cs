using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    // C# port of src\pages\Config.ps1: Windows features, fixes, legacy panels, activation check, OpenSSH, cursors, shortcut.
    internal sealed class ConfigPage : IPage
    {
        private sealed class Feat
        {
            public string Name; public string[] Features; public string File, Arg;
            public Feat(string name, params string[] features) { Name = name; Features = features; }
            public Feat(string name, string file, string arg, bool isCmd) { Name = name; File = file; Arg = arg; Features = null; }
            public bool IsCmd { get { return File != null; } }
        }

        private static readonly Feat[] Feats = {
            new Feat(".NET Framework 3.5 (2, 3) - Enable", "NetFx3"),
            new Feat("Hyper-V - Enable", "Microsoft-Hyper-V-All"),
            new Feat("Legacy F8 Boot Recovery - Enable", "bcdedit.exe", "/set {current} bootmenupolicy legacy", true),
            new Feat("Legacy F8 Boot Recovery - Disable", "bcdedit.exe", "/set {current} bootmenupolicy standard", true),
            new Feat("Legacy Media Components (WMP, DirectPlay) - Enable", "MediaPlayback", "WindowsMediaPlayer", "LegacyComponents", "DirectPlay"),
            new Feat("Network File System (NFS) - Enable", "ServicesForNFS-ClientOnly", "ClientForNFS-Infrastructure", "NFS-Administration"),
            new Feat("Windows Sandbox - Enable", "Containers-DisposableClientVM"),
            new Feat("Windows Subsystem for Linux (WSL) - Enable", "Microsoft-Windows-Subsystem-Linux", "VirtualMachinePlatform")
        };

        private static readonly string[][] Panels = {
            new string[] { "Computer Management", "compmgmt.msc", "" }, new string[] { "Control Panel", "control.exe", "" },
            new string[] { "Mouse Properties", "control.exe", "main.cpl" }, new string[] { "Network Connections", "control.exe", "ncpa.cpl" },
            new string[] { "Power Panel", "control.exe", "powercfg.cpl" }, new string[] { "Printer Panel", "control.exe", "printers" },
            new string[] { "Programs and Features", "control.exe", "appwiz.cpl" }, new string[] { "Region", "control.exe", "intl.cpl" },
            new string[] { "Security and Maintenance", "control.exe", "wscui.cpl" }, new string[] { "Sound Settings", "control.exe", "mmsys.cpl" },
            new string[] { "System Properties", "control.exe", "sysdm.cpl" }, new string[] { "Time and Date", "control.exe", "timedate.cpl" },
            new string[] { "Windows Defender Firewall", "control.exe", "firewall.cpl" }, new string[] { "Windows Restore", "rstrui.exe", "" }
        };

        private const string CursorScheme = "Windows 11 Cursors Dark by Jepri Creations";
        private const string CursorDir = "Windows_11_dark";
        private static readonly string[][] CursorMap = {
            new string[] { "Arrow", "pointer.cur" }, new string[] { "Help", "help.cur" }, new string[] { "AppStarting", "working.ani" }, new string[] { "Wait", "busy.ani" },
            new string[] { "crosshair", "precision.cur" }, new string[] { "precisionhair", "precision.cur" }, new string[] { "IBeam", "beam.cur" }, new string[] { "NWPen", "handwriting.cur" },
            new string[] { "No", "unavailable.cur" }, new string[] { "SizeNS", "vert.cur" }, new string[] { "SizeWE", "horz.cur" }, new string[] { "SizeNWSE", "dgn1.cur" }, new string[] { "SizeNESW", "dgn2.cur" },
            new string[] { "SizeAll", "move.cur" }, new string[] { "UpArrow", "alternate.cur" }, new string[] { "Hand", "link.cur" }, new string[] { "Person", "person.cur" }, new string[] { "Pin", "pin.cur" } };
        private static readonly string[] CursorOrder = { "pointer.cur", "help.cur", "working.ani", "busy.ani", "precision.cur", "beam.cur", "handwriting.cur", "unavailable.cur", "vert.cur", "horz.cur", "dgn1.cur", "dgn2.cur", "move.cur", "alternate.cur", "link.cur", "person.cur", "pin.cur" };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);

        private readonly List<CheckBox> _boxes = new List<CheckBox>();
        private readonly Dictionary<string, bool> _done = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _pending = new Dictionary<string, bool>();
        private bool _stateLoaded;

        public string Key { get { return "Config"; } }

        public void Init(Window w, Logger log)
        {
            StackPanel fp = Ui.Find<StackPanel>("featuresPanel");
            foreach (Feat f in Feats)
            {
                CheckBox cb = new CheckBox(); cb.Content = f.Name; cb.Tag = f;
                fp.Children.Add(cb); _boxes.Add(cb);
            }
            Ui.Find<Button>("btnFeatures").Click += delegate { EnableFeatures(); };

            StackPanel fixes = Ui.Find<StackPanel>("fixesPanel");
            AddFix(fixes, "AutoLogon - Run", delegate(WorkCtx c) { Tool.Start("netplwiz.exe", ""); c.Info("Opened netplwiz (untick \"Users must enter a user name and password\").", "Fix"); });
            AddFix(fixes, "Network - Reset", delegate(WorkCtx c)
            {
                c.Info("Resetting network stack...", "Fix");
                c.Tool(Tool.System32("netsh.exe"), "winsock reset"); c.Tool(Tool.System32("netsh.exe"), "int ip reset"); c.Tool(Tool.System32("ipconfig.exe"), "/flushdns");
                c.Info("Network reset done. Restart Windows to finish.", "Fix");
            });
            AddFix(fixes, "NTP Server - Enable (sync time)", delegate(WorkCtx c)
            {
                SysUtil.SetStartMode("w32time", "Automatic"); SysUtil.StartService("w32time");
                c.Tool(Tool.System32("w32tm.exe"), "/config /syncfromflags:manual /manualpeerlist:\"time.windows.com pool.ntp.org\" /update");
                c.Tool(Tool.System32("w32tm.exe"), "/resync /force");
                c.Info("Time service configured and resynced.", "Fix");
            });
            AddFix(fixes, "System Corruption Scan - Run (DISM + SFC)", delegate(WorkCtx c)
            {
                c.Info("Running DISM RestoreHealth (this can take a while)...", "Fix");
                c.Tool(Tool.System32("dism.exe"), "/Online /Cleanup-Image /RestoreHealth");
                c.Info("Running sfc /scannow...", "Fix");
                c.Tool(Tool.System32("sfc.exe"), "/scannow");
                c.Info("Scan finished.", "Fix");
            });
            AddFix(fixes, "Windows Update - Reset", delegate(WorkCtx c) { SysUtil.ResetWindowsUpdate(c); });
            AddFix(fixes, "WinGet - Reinstall", delegate(WorkCtx c)
            {
                c.Info("Re-registering App Installer (winget)...", "Fix");
                Appx.RegisterByFamily("Microsoft.DesktopAppInstaller_8wekyb3d8bbwe");
                c.Info("Done. Open a new terminal and run: winget --version", "Fix");
            });
            Button spotx = Ui.Row("SPOTX (Spotify adblock + theme)", "\uE713");
            spotx.Click += delegate { InstallSpotX(); };
            fixes.Children.Add(spotx);

            StackPanel panels = Ui.Find<StackPanel>("panelsPanel");
            foreach (string[] p in Panels)
            {
                Button b = Ui.Row(p[0], "\uE8A7"); b.Tag = p;
                b.Click += delegate(object s, RoutedEventArgs e)
                {
                    string[] pp = (string[])((Button)s).Tag;
                    try { Tool.Start(pp[1], pp[2]); }
                    catch (Exception ex) { Ui.Log.Error("Could not open " + pp[0] + ": " + ex.Message, "Config"); }
                };
                panels.Children.Add(b);
            }

            Ui.Find<Button>("btnActCheck").Click += delegate { ActivationDialog(); };
            Ui.Find<Button>("btnSsh").Click += delegate
            {
                Work.Run("Installing OpenSSH...", delegate(WorkCtx c)
                {
                    c.Info("Installing OpenSSH Server (may take a few minutes)...", "SSH");
                    c.Tool(Tool.System32("dism.exe"), "/Online /Add-Capability /CapabilityName:OpenSSH.Server~~~~0.0.1.0 /NoRestart /Quiet");
                    SysUtil.SetStartMode("sshd", "Automatic"); SysUtil.StartService("sshd");
                    c.Tool(Tool.System32("netsh.exe"), "advfirewall firewall add rule name=\"OpenSSH Server (sshd)\" dir=in action=allow protocol=TCP localport=22");
                    c.Info("OpenSSH Server enabled and listening on port 22.", "SSH");
                });
            };
            Ui.Find<Button>("btnCurApply").Click += delegate { ApplyCursors(); };
            Ui.Find<Button>("btnCurReset").Click += delegate { ResetCursors(); };
            Ui.Find<Button>("btnShortcut").Click += delegate { MakeShortcut(); };
            Ui.Find<Button>("btnGameDesk").Click += delegate { Work.Run("Gaming settings (desktop)...", delegate(WorkCtx c) { GameBoost.Apply(c, false); }); };
            Ui.Find<Button>("btnGameLap").Click += delegate { Work.Run("Gaming settings (laptop)...", delegate(WorkCtx c) { GameBoost.Apply(c, true); }); };
            Ui.Find<Button>("btnGameLoop").Click += delegate { Work.Run("Game Loop boost...", delegate(WorkCtx c) { GameBoost.ApplyGameLoop(c); }); };
            Ui.Find<Button>("btnGameRestore").Click += delegate { Work.Run("Restoring gaming settings...", delegate(WorkCtx c) { GameBoost.Restore(c); }); };
        }

        // SpotX: runs directly (no confirmation), the authors' command in a visible PowerShell window that closes by itself when it finishes
        private const string SpotXCommand = "iex \"& { $(iwr -useb 'https://raw.githubusercontent.com/SpotX-Official/SpotX/refs/heads/main/run.ps1') } -new_theme\"; Start-Sleep -Seconds 3; exit";

        private static void InstallSpotX()
        {
            try
            {
                string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(SpotXCommand));
                ProcessStartInfo psi = new ProcessStartInfo(Tool.System32(@"WindowsPowerShell\v1.0\powershell.exe"), "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + enc);
                psi.UseShellExecute = true;
                Ui.Log.Info("SpotX is running in a PowerShell window - it closes by itself when it is done.", "SpotX");
                Process p = Process.Start(psi);
                if (p != null)
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { p.WaitForExit(); } catch (Exception) { } Ui.Post(delegate { Ui.Log.Info("SpotX finished.", "SpotX"); }); });
                }
            }
            catch (Exception ex) { Ui.Log.Error("Could not start PowerShell: " + ex.Message, "SpotX"); Dlg.Error("Could not start PowerShell\n\n" + ex.Message); }
        }

        private static void AddFix(StackPanel panel, string name, Action<WorkCtx> act)
        {
            Button b = Ui.Row(name, "\uE713");
            b.Click += delegate { Work.Run(name + "...", delegate(WorkCtx c) { act(c); }); };
            panel.Children.Add(b);
        }

        public void OnShown() { if (!_stateLoaded) LoadFeatureState(); }

        // ---------------- features ----------------
        private void LoadFeatureState()
        {
            _stateLoaded = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<string, int> map = new Dictionary<string, int>();
                try
                {
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, InstallState FROM Win32_OptionalFeature"))
                    using (ManagementObjectCollection col = s.Get())
                        foreach (ManagementObject o in col) map[Convert.ToString(o["Name"])] = Convert.ToInt32(o["InstallState"]);
                }
                catch { }
                try
                {
                    int code; string bc = Tool.Capture(Tool.System32("bcdedit.exe"), "/enum {current}", out code);
                    map["__f8legacy"] = Regex.IsMatch(bc, @"bootmenupolicy\s+Legacy", RegexOptions.IgnoreCase) ? 1 : 0;
                }
                catch { }
                Ui.Post(delegate { ApplyFeatureState(map); });
            });
        }

        private void ApplyFeatureState(Dictionary<string, int> map)
        {
            foreach (CheckBox cb in _boxes)
            {
                Feat f = (Feat)cb.Tag; bool on = false;
                if (f.IsCmd)
                {
                    if (map.ContainsKey("__f8legacy")) { bool leg = map["__f8legacy"] == 1; on = f.Name.EndsWith("- Enable") ? leg : !leg; }
                }
                else
                {
                    int found = 0; bool allOn = true;
                    foreach (string n in f.Features) if (map.ContainsKey(n)) { found++; if (map[n] != 1) allOn = false; }
                    if (found > 0) on = allOn;
                }
                bool pend = !on && _pending.ContainsKey(f.Name);
                if (on || pend)
                {
                    _done[f.Name] = true;
                    cb.IsChecked = true; cb.IsHitTestVisible = false; cb.Focusable = false;
                    if (pend) { cb.Content = f.Name + "   - enabled, restart Windows to finish"; cb.Foreground = Ui.Brush("#FFB454"); }
                    else if (f.IsCmd) { cb.Content = f.Name + "   - current setting"; cb.Foreground = Ui.Brush("#3DDC84"); }
                    else { cb.Content = f.Name + "   - already enabled"; cb.Foreground = Ui.Brush("#3DDC84"); }
                }
                else
                {
                    _done.Remove(f.Name);
                    cb.IsHitTestVisible = true; cb.Focusable = true; cb.Content = f.Name;
                    cb.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private void EnableFeatures()
        {
            List<Feat> sel = new List<Feat>();
            foreach (CheckBox cb in _boxes) if (cb.IsChecked == true && !_done.ContainsKey(((Feat)cb.Tag).Name)) sel.Add((Feat)cb.Tag);
            if (sel.Count == 0) { Ui.Log.Warn("No features selected (the ticked ones are already enabled).", "Features"); return; }
            Work.Run("Enabling features...", delegate(WorkCtx c)
            {
                int n = sel.Count, i = 0;
                foreach (Feat f in sel)
                {
                    c.ThrowIfCancelled();
                    c.Progress(100.0 * i / n, "Enabling: " + f.Name + "  (" + (i + 1) + "/" + n + ")");
                    c.Info("Enabling: " + f.Name, "Features");
                    if (f.IsCmd) { int code = c.Tool(Tool.System32(f.File), f.Arg); c.Info("  exit code " + code, "Features"); }
                    else
                        foreach (string nm in f.Features)
                        {
                            c.ThrowIfCancelled();
                            int code = c.Tool(Tool.System32("dism.exe"), "/Online /Enable-Feature /FeatureName:" + nm + " /All /NoRestart /Quiet");
                            c.Info("  " + nm + " -> exit code " + code + " (3010 = restart required)", "Features");
                            if (code == 3010) _pending[f.Name] = true;
                        }
                    i++;
                }
                c.Info("Done. Restart Windows to finish enabling features.", "Features");
                _stateLoaded = false;
                c.UI(delegate { LoadFeatureState(); });
            });
        }

        // ---------------- cursors ----------------
        private static void SetCursorRegistry(Dictionary<string, string> vals, string scheme, int source, string schemeList)
        {
            string key = @"HKCU:\Control Panel\Cursors";
            Reg.SetString(key, "", scheme);
            foreach (KeyValuePair<string, string> kv in vals) Reg.Set(key, kv.Key, kv.Value, RegistryValueKind.ExpandString);
            Reg.SetDword(key, "Scheme Source", source);
            if (!string.IsNullOrEmpty(schemeList)) Reg.Set(key + @"\Schemes", scheme, schemeList, RegistryValueKind.ExpandString);
        }

        private static void ApplyCursors()
        {
            Work.Run("Applying cursors...", delegate(WorkCtx c)
            {
                c.Info("Applying cursor scheme: " + CursorScheme, "Cursor");
                string pack = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"assets\CursorPack.zip");
                if (!File.Exists(pack)) throw new FileNotFoundException("Cursor pack not found: " + pack);
                string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Cursors\" + CursorDir);
                Directory.CreateDirectory(dest);
                int cnt = 0;
                using (ZipArchive zip = ZipFile.OpenRead(pack))
                    foreach (ZipArchiveEntry e in zip.Entries)
                    {
                        cnt++;
                        string target = Path.Combine(dest, e.Name);
                        if (File.Exists(target) && new FileInfo(target).Length == e.Length) continue;
                        e.ExtractToFile(target, true);
                    }
                c.Info("  " + cnt + " cursor files written to " + dest, "Cursor");
                Dictionary<string, string> vals = new Dictionary<string, string>();
                foreach (string[] m in CursorMap) vals[m[0]] = @"%SystemRoot%\Cursors\" + CursorDir + @"\" + m[1];
                List<string> list = new List<string>();
                foreach (string f in CursorOrder) list.Add(@"%SystemRoot%\Cursors\" + CursorDir + @"\" + f);
                SetCursorRegistry(vals, CursorScheme, 1, string.Join(",", list.ToArray()));
                SystemParametersInfo(0x57, 0, IntPtr.Zero, 3);
                c.Info("Cursor scheme applied. Open Mouse Properties > Pointers to see it.", "Cursor");
            });
        }

        private static void ResetCursors()
        {
            Work.Run("Restoring cursors...", delegate(WorkCtx c)
            {
                c.Info("Restoring default Windows cursors...", "Cursor");
                string[][] def = {
                    new string[] { "Arrow", "aero_arrow.cur" }, new string[] { "Help", "aero_helpsel.cur" }, new string[] { "AppStarting", "aero_working.ani" }, new string[] { "Wait", "aero_busy.ani" },
                    new string[] { "crosshair", "" }, new string[] { "precisionhair", "" }, new string[] { "IBeam", "" }, new string[] { "NWPen", "aero_pen.cur" }, new string[] { "No", "aero_unavail.cur" },
                    new string[] { "SizeNS", "aero_ns.cur" }, new string[] { "SizeWE", "aero_ew.cur" }, new string[] { "SizeNWSE", "aero_nwse.cur" }, new string[] { "SizeNESW", "aero_nesw.cur" },
                    new string[] { "SizeAll", "aero_move.cur" }, new string[] { "UpArrow", "aero_up.cur" }, new string[] { "Hand", "aero_link.cur" }, new string[] { "Person", "aero_person.cur" }, new string[] { "Pin", "aero_pin.cur" } };
                Dictionary<string, string> vals = new Dictionary<string, string>();
                foreach (string[] d in def) vals[d[0]] = d[1].Length > 0 ? @"%SystemRoot%\cursors\" + d[1] : "";
                SetCursorRegistry(vals, "Windows Default", 2, null);
                SystemParametersInfo(0x57, 0, IntPtr.Zero, 3);
                c.Info("Default Windows cursors restored.", "Cursor");
            });
        }

        // ---------------- desktop shortcut ----------------
        private static void MakeShortcut()
        {
            Work.Run("Creating shortcut...", delegate(WorkCtx c)
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dir = Path.GetDirectoryName(exe);
                string ico = Path.Combine(dir, "RockToolbox.ico");
                if (!File.Exists(ico)) ico = exe;
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                int made = 0;
                foreach (string f in new string[] { desktop })
                {
                    try
                    {
                        string lnk = Path.Combine(f, "Rock Toolbox.lnk");
                        dynamic s = sh.CreateShortcut(lnk);
                        s.TargetPath = exe; s.WorkingDirectory = dir; s.IconLocation = ico + ",0"; s.Description = "Rock Toolbox";
                        s.Save();
                        c.Info("  shortcut created: " + lnk, "Shortcut"); made++;
                    }
                    catch (Exception ex) { c.Warn("  could not create shortcut in " + f + " : " + ex.Message, "Shortcut"); }
                }
                if (made == 0) throw new InvalidOperationException("No shortcut could be created.");
                c.Info("Done. If the icon looks old, press F5 on the desktop or restart Explorer (Tweaks tab).", "Shortcut");
            });
        }

        // ---------------- activation dialog ----------------
        private sealed class ActResult
        {
            public bool Ok; public string Edition = "", Name = "", Desc = "", Channel = "", Partial = "", BiosKey = "", RegKey = "", Err = "";
            public int Status = -1; public int Grace;
        }

        private static string DecodeKey(byte[] dpid)
        {
            if (dpid == null || dpid.Length < 67) return "";
            string map = "BCDFGHJKMPQRTVWXY2346789";
            byte[] v = (byte[])dpid.Clone();
            int isWin8 = (v[66] / 6) & 1;
            v[66] = (byte)((v[66] & 0xF7) | ((isWin8 & 2) * 4));
            string key = ""; int last = 0;
            for (int i = 24; i >= 0; i--)
            {
                int cur = 0;
                for (int j = 14; j >= 0; j--) { cur = cur * 256 + v[j + 52]; v[j + 52] = (byte)(cur / 24); cur = cur % 24; }
                key = map[cur] + key; last = cur;
            }
            if (isWin8 == 1) { string r = key.Substring(1); key = r.Substring(0, last) + "N" + r.Substring(last); }
            return Regex.Replace(key, "(.{5})(?=.)", "$1-");
        }

        private static ActResult ReadActivation()
        {
            ActResult o = new ActResult();
            try
            {
                string cvp = @"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
                string dv = Convert.ToString(Reg.Get(cvp, "DisplayVersion")); if (dv.Length == 0) dv = Convert.ToString(Reg.Get(cvp, "ReleaseId"));
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem"))
                using (ManagementObjectCollection col = s.Get())
                    foreach (ManagementObject os in col)
                        o.Edition = Convert.ToString(os["Caption"]).Trim() + "   (" + (dv.Length > 0 ? dv + ", " : "") + "build " + Convert.ToString(os["BuildNumber"]) + ")";
            }
            catch { }
            try
            {
                List<ManagementObject> list = new List<ManagementObject>();
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT * FROM SoftwareLicensingProduct WHERE ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL"))
                using (ManagementObjectCollection col = s.Get())
                    foreach (ManagementObject m in col) list.Add(m);
                ManagementObject p = list.Find(delegate(ManagementObject m) { return !Regex.IsMatch(Convert.ToString(m["Name"]), "ESU|add-on"); });
                if (p == null && list.Count > 0) p = list[0];
                if (p != null)
                {
                    o.Ok = true; o.Status = Convert.ToInt32(p["LicenseStatus"]); o.Name = Convert.ToString(p["Name"]); o.Desc = Convert.ToString(p["Description"]);
                    o.Channel = Convert.ToString(p["ProductKeyChannel"]); o.Partial = Convert.ToString(p["PartialProductKey"]);
                    try { o.Grace = Convert.ToInt32(p["GracePeriodRemaining"]); } catch { }
                }
                else o.Err = "No Windows license was found on this PC.";
            }
            catch (Exception ex) { o.Err = ex.Message; }
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService"))
                using (ManagementObjectCollection col = s.Get())
                    foreach (ManagementObject m in col) o.BiosKey = Convert.ToString(m["OA3xOriginalProductKey"]).Trim();
            }
            catch { }
            try { o.RegKey = DecodeKey(Reg.Get(@"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DigitalProductId") as byte[]); } catch { }
            return o;
        }

        private static void ActivationDialog()
        {
            Window w = new Window();
            w.Title = "Windows Activation"; w.Owner = Ui.Win; w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            w.SizeToContent = SizeToContent.WidthAndHeight; w.ResizeMode = ResizeMode.NoResize; w.ShowInTaskbar = false;
            w.Background = Ui.Brush("#1F2329"); w.FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); w.FontSize = 13; w.MinWidth = 600; w.MaxWidth = 760;
            StackPanel root = new StackPanel(); root.Margin = new Thickness(24, 22, 24, 22);
            TextBlock head = new TextBlock(); head.FontSize = 17; head.FontWeight = FontWeights.Bold; head.Foreground = Ui.Brush("#FFFFFF"); head.TextWrapping = TextWrapping.Wrap;
            TextBlock sub = new TextBlock(); sub.Foreground = Ui.Brush("#9AA0B8"); sub.TextWrapping = TextWrapping.Wrap; sub.Margin = new Thickness(0, 2, 0, 0);
            root.Children.Add(head); root.Children.Add(sub);
            string[] labels = { "Windows edition", "License", "License type", "Active key (last 5)", "Key in BIOS / UEFI", "Installed key (registry)" };
            Grid grid = new Grid(); grid.Margin = new Thickness(0, 16, 0, 0);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            List<TextBlock> vals = new List<TextBlock>();
            for (int i = 0; i < labels.Length; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                TextBlock l = new TextBlock(); l.Text = labels[i]; l.Margin = new Thickness(0, 5, 26, 5); l.Foreground = Ui.Brush("#9AA0B8");
                Grid.SetRow(l, i); Grid.SetColumn(l, 0);
                TextBlock v = new TextBlock(); v.Margin = new Thickness(0, 5, 0, 5); v.Foreground = Ui.Brush("#E6E8F2"); v.TextWrapping = TextWrapping.Wrap; v.MaxWidth = 430; v.FontWeight = FontWeights.SemiBold;
                Grid.SetRow(v, i); Grid.SetColumn(v, 1);
                grid.Children.Add(l); grid.Children.Add(v); vals.Add(v);
            }
            root.Children.Add(grid);
            TextBlock note = new TextBlock(); note.Foreground = Ui.Brush("#8D93AD"); note.FontSize = 11.5; note.TextWrapping = TextWrapping.Wrap; note.MaxWidth = 640; note.Margin = new Thickness(2, 10, 0, 0);
            note.Visibility = Visibility.Collapsed; root.Children.Add(note);
            StackPanel btns = new StackPanel(); btns.Orientation = Orientation.Horizontal; btns.HorizontalAlignment = HorizontalAlignment.Right; btns.Margin = new Thickness(0, 18, 0, 0);
            Button copy = new Button(); copy.Content = "Copy details"; copy.Padding = new Thickness(14, 5, 14, 5);
            Button again = new Button(); again.Content = "Check again"; again.Padding = new Thickness(14, 5, 14, 5); again.Margin = new Thickness(10, 0, 0, 0);
            Button close = new Button(); close.Content = "Close"; close.Padding = new Thickness(14, 5, 14, 5); close.Margin = new Thickness(10, 0, 0, 0); close.IsDefault = true; close.IsCancel = true;
            btns.Children.Add(copy); btns.Children.Add(again); btns.Children.Add(close); root.Children.Add(btns);
            w.Content = root;
            w.ContentRendered += delegate { try { Fx.FadeUp(root, 0, 14); } catch { } };

            string report = "";
            Action start = null;
            start = delegate
            {
                head.Text = "Checking Windows activation..."; sub.Text = ""; copy.IsEnabled = false; again.IsEnabled = false; note.Visibility = Visibility.Collapsed;
                foreach (TextBlock t in vals) t.Text = "...";
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    ActResult r = ReadActivation();
                    w.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        again.IsEnabled = true;
                        string[] st = { "Unlicensed", "Licensed", "Initial grace period", "Additional grace period", "Non-genuine grace period", "Notification mode", "Extended grace period" };
                        if (r.Ok && r.Status == 1)
                        {
                            head.Text = "Windows is activated"; sub.Text = "License status: Licensed";
                            if (Regex.IsMatch(r.Channel + r.Desc, "Volume|KMS|GVLK") && r.Grace > 0)
                                sub.Text = "Volume (KMS) activation - about " + (r.Grace / 1440) + " day(s) left until the next automatic renewal.";
                        }
                        else if (r.Ok) { head.Text = "Windows is NOT activated"; sub.Text = "License status: " + (r.Status >= 0 && r.Status < st.Length ? st[r.Status] : "Unknown"); }
                        else { head.Text = "Could not read the license status"; sub.Text = r.Err; }
                        string type = r.Channel.Length > 0 ? r.Channel : r.Desc;
                        string last5 = r.Partial.Length > 0 ? "XXXXX-XXXXX-XXXXX-XXXXX-" + r.Partial : "";
                        string bios = r.BiosKey.Length > 0 ? r.BiosKey : "None stored in the BIOS / UEFI";
                        string reg = r.RegKey; bool mismatch = false;
                        if (reg.Length == 0) reg = "Not available";
                        else if (r.Partial.Length > 0 && !reg.EndsWith(r.Partial)) { reg += "   (generic - not the active key)"; mismatch = true; }
                        string[] vv = { r.Edition, r.Name, type, last5, bios, reg };
                        StringBuilder sb = new StringBuilder("Windows activation\r\n" + head.Text + "\r\n" + sub.Text + "\r\n");
                        for (int i = 0; i < vals.Count; i++) { if (vv[i].Length == 0) vv[i] = "-"; vals[i].Text = vv[i]; sb.Append(labels[i] + ": " + vv[i] + "\r\n"); }
                        report = sb.ToString(); copy.IsEnabled = true;
                        if (r.Ok && r.BiosKey.Length == 0 && (mismatch || r.RegKey.Length == 0))
                        {
                            note.Text = "Windows 10 / 11 usually uses a digital license linked to this PC or your Microsoft account, so the full key often cannot be read back. The last 5 characters above identify the active key.";
                            note.Visibility = Visibility.Visible;
                        }
                        Ui.Log.Info(head.Text, "Activation");
                    }));
                });
            };
            copy.Click += delegate { try { Clipboard.SetText(report); Ui.Log.Info("Activation details copied to the clipboard.", "Activation"); } catch { } };
            again.Click += delegate { start(); };
            close.Click += delegate { w.Close(); };
            start();
            w.ShowDialog();
        }
    }
}
