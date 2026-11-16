<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    internal sealed class StartupItem : Notify
    {
        private bool _selected;
        private string _status;
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Group { get; set; }
        public string Command { get; set; }
        public string Source { get; set; }
        public string Ref { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
        public string Status { get { return _status; } set { _status = value; Raise("Status"); } }
    }

    // C# port of src\pages\Startup.ps1: startup apps (registry + folders), logon scheduled tasks and auto services.
    internal sealed class StartupPage : IPage
    {
        private const string ApprBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

        private ListView _lv;
        private ComboBox _kind;
        private TextBox _search;
        private TextBlock _count;
        private DispatcherTimer _debounce;
        private List<StartupItem> _all;
        private bool _loaded;

        public string Key { get { return "Startup"; } }

        public void Init(Window w, Logger log)
        {
            _lv = Ui.Find<ListView>("lvStartup");
            _kind = Ui.Find<ComboBox>("cmbStKind");
            _search = Ui.Find<TextBox>("txtStSearch");
            _count = Ui.Find<TextBlock>("txtStCount");

            foreach (string t in new string[] { "All", "Startup apps", "Scheduled tasks", "Services" }) _kind.Items.Add(t);
            _kind.SelectedIndex = 0;
            _kind.SelectionChanged += delegate { ApplyFilter(); };

            _debounce = new DispatcherTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(250);
            _debounce.Tick += delegate { _debounce.Stop(); ApplyFilter(); };
            _search.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };

            Ui.Find<Button>("btnStRefresh").Click += delegate { Scan(); };
            Ui.Find<Button>("btnStNone").Click += delegate
            {
                if (_all != null) foreach (StartupItem x in _all) x.Selected = false;
                UpdateCount();
            };
            Ui.Find<Button>("btnStDisable").Click += delegate { Toggle(false); };
            Ui.Find<Button>("btnStEnable").Click += delegate { Toggle(true); };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateCount(); }));
            _lv.MouseDoubleClick += delegate
            {
                StartupItem it = _lv.SelectedItem as StartupItem;
                if (it != null) { it.Selected = !it.Selected; UpdateCount(); }
            };
            Ui.Find<Button>("btnStOpen").Click += delegate { OpenLocation(); };
        }

        public void OnShown() { if (!_loaded) Scan(); }

        private void UpdateCount()
        {
            List<StartupItem> src = _lv.ItemsSource as List<StartupItem>;
            int total = src == null ? 0 : src.Count, n = 0;
            if (src != null) foreach (StartupItem x in src) if (x.Selected) n++;
            _count.Text = "Selected: " + n + " of " + total;
        }

        private void ApplyFilter()
        {
            if (_all == null) return;
            string kind = _kind.SelectedItem as string;
            string q = (_search.Text ?? "").Trim();
            List<StartupItem> list = new List<StartupItem>();
            foreach (StartupItem it in _all)
            {
                if (kind == "Startup apps" && it.Group != "apps") continue;
                if (kind == "Scheduled tasks" && it.Group != "tasks") continue;
                if (kind == "Services" && it.Group != "services") continue;
                if (q.Length > 0 && it.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    it.Command.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(it);
            }
            _lv.ItemsSource = list;
            UpdateCount();
        }

        // ---------------- scanning ----------------
        private static bool IsEnabled(string hive, string sub, string name)
        {
            object o = Reg.Get(hive + @":\" + ApprBase + @"\" + sub, name);
            byte[] b = o as byte[];
            if (b != null && b.Length > 0) return (b[0] & 1) == 0;
            return true;
        }

        private static void AddRow(List<StartupItem> rows, string name, string kind, string group, string cmd, string src, string rf, bool en)
        {
            StartupItem it = new StartupItem();
            it.Name = name ?? ""; it.Kind = kind; it.Group = group; it.Command = cmd ?? ""; it.Source = src; it.Ref = rf;
            it.Status = en ? "Enabled" : "Disabled";
            rows.Add(it);
        }

        private static List<StartupItem> ReadAll(WorkCtx c)
        {
            List<StartupItem> rows = new List<StartupItem>();

            c.Progress(-1, "Reading startup programs...");
            string[][] runKeys = new string[][]
            {
                new string[] { "HKCU", @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", "Run", "Registry (current user)" },
                new string[] { "HKLM", @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Run", "Run", "Registry (all users)" },
                new string[] { "HKLM", @"HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", "Registry (all users, 32-bit)" }
            };
            foreach (string[] rk in runKeys)
            {
                try
                {
                    string path = rk[1];
                    RegistryKey root = rk[0] == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.OpenSubKey(path.Substring(path.IndexOf('\\') + 1)))
                    {
                        if (k == null) continue;
                        foreach (string vn in k.GetValueNames())
                        {
                            if (vn.Length == 0) continue;
                            AddRow(rows, vn, "Startup app", "apps", Convert.ToString(k.GetValue(vn)), rk[3],
                                "reg|" + rk[0] + "|" + rk[2] + "|" + vn, IsEnabled(rk[0], rk[2], vn));
                        }
                    }
                }
                catch { }
            }

            string[][] folders = new string[][]
            {
                new string[] { "HKCU", Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder (current user)" },
                new string[] { "HKLM", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)" }
            };
            dynamic wsh = null;
            try { wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); } catch { }
            foreach (string[] f in folders)
            {
                try
                {
                    if (!Directory.Exists(f[1])) continue;
                    foreach (string file in Directory.GetFiles(f[1]))
                    {
                        string fn = Path.GetFileName(file);
                        if (string.Equals(fn, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        string cmd = file;
                        if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && wsh != null)
                        {
                            try { dynamic lk = wsh.CreateShortcut(file); cmd = (Convert.ToString(lk.TargetPath) + " " + Convert.ToString(lk.Arguments)).Trim(); } catch { }
                        }
                        AddRow(rows, Path.GetFileNameWithoutExtension(file), "Startup app", "apps", cmd, f[2],
                            "folder|" + f[0] + "|StartupFolder|" + fn, IsEnabled(f[0], "StartupFolder", fn));
                    }
                }
                catch { }
            }

            c.Progress(-1, "Reading scheduled tasks...");
            try
            {
                dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                svc.Connect();
                WalkTasks(svc.GetFolder("\\"), rows);
            }
            catch { }

            c.Progress(-1, "Reading services...");
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, DisplayName, StartMode, PathName FROM Win32_Service"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject o in col)
                    {
                        string mode = Convert.ToString(o["StartMode"]);
                        if (mode != "Auto" && mode != "Disabled") continue;
                        string pn = Convert.ToString(o["PathName"]);
                        if (string.IsNullOrEmpty(pn)) continue;
                        if (Regex.IsMatch(pn, @"\\Windows\\|\\Windows Defender\\|\\Microsoft Shared\\|WindowsApps|\\Microsoft\.NET\\", RegexOptions.IgnoreCase)) continue;
                        AddRow(rows, Convert.ToString(o["DisplayName"]), "Service", "services", pn,
                            "Service: " + Convert.ToString(o["Name"]), "svc|" + Convert.ToString(o["Name"]), mode != "Disabled");
                    }
                }
            }
            catch { }

            rows.Sort(delegate(StartupItem a, StartupItem b)
            {
                int r = string.Compare(a.Group, b.Group, StringComparison.Ordinal);
                if (r != 0) return r;
                r = (a.Status == "Disabled" ? 1 : 0).CompareTo(b.Status == "Disabled" ? 1 : 0);
                if (r != 0) return r;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return rows;
        }

        private static void WalkTasks(dynamic folder, List<StartupItem> rows)
        {
            try
            {
                foreach (dynamic task in folder.GetTasks(1))
                {
                    try
                    {
                        string full = Convert.ToString(task.Path);
                        int i = full.LastIndexOf('\\');
                        string tpath = full.Substring(0, i + 1);
                        if (tpath.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase)) continue;
                        bool logon = false;
                        foreach (dynamic tr in task.Definition.Triggers)
                        {
                            int type = (int)tr.Type;
                            if (type == 8 || type == 9) logon = true;   // boot / logon
                        }
                        if (!logon) continue;
                        string cmd = "";
                        foreach (dynamic a in task.Definition.Actions)
                        {
                            try { cmd = (Convert.ToString(a.Path) + " " + Convert.ToString(a.Arguments)).Trim(); } catch { }
                            break;
                        }
                        AddRow(rows, Convert.ToString(task.Name), "Scheduled task", "tasks", cmd, "Task: " + tpath,
                            "task|" + tpath + "|" + Convert.ToString(task.Name), (bool)task.Enabled);
                    }
                    catch { }
                }
                foreach (dynamic sub in folder.GetFolders(0)) WalkTasks(sub, rows);
            }
            catch { }
        }

        private void Scan()
        {
            Work.Run("Reading startup items...", delegate(WorkCtx c)
            {
                c.Info("Reading startup items (programs, tasks, services)...", "Startup");
                List<StartupItem> rows = ReadAll(c);
                _all = rows;
                _loaded = true;
                c.UI(delegate { ApplyFilter(); });
                c.Info(rows.Count + " startup items found.", "Startup");
            });
        }

        // ---------------- enable / disable ----------------
        private static void SetItem(StartupItem item, bool enable)
        {
            string[] p = item.Ref.Split(new char[] { '|' }, 4);
            switch (p[0])
            {
                case "reg":
                case "folder":
                    {
                        byte[] bytes;
                        if (enable) bytes = new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                        else
                        {
                            bytes = new byte[12];
                            bytes[0] = 3;
                            byte[] ft = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                            Array.Copy(ft, 0, bytes, 4, 8);
                        }
                        Reg.Set(p[1] + @":\" + ApprBase + @"\" + p[2], p[3], bytes, RegistryValueKind.Binary);
                        break;
                    }
                case "task":
                    {
                        string[] tn = item.Ref.Split(new char[] { '|' }, 3);
                        dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                        svc.Connect();
                        dynamic task = svc.GetFolder(tn[1]).GetTask(tn[2]);
                        task.Enabled = enable;
                        break;
                    }
                case "svc":
                    {
                        using (ManagementObject o = new ManagementObject("Win32_Service.Name='" + p[1] + "'"))
                        {
                            object r = o.InvokeMethod("ChangeStartMode", new object[] { enable ? "Automatic" : "Disabled" });
                            if (Convert.ToInt32(r) != 0) throw new InvalidOperationException("ChangeStartMode returned " + r);
                        }
                        break;
                    }
            }
            item.Status = enable ? "Enabled" : "Disabled";
        }

        private void Toggle(bool enable)
        {
            List<StartupItem> src = _lv.ItemsSource as List<StartupItem>;
            List<StartupItem> sel = new List<StartupItem>();
            if (src != null) foreach (StartupItem x in src) if (x.Selected) sel.Add(x);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the items you want to change first.", "Startup"); return; }
            if (!enable)
            {
                int svc = 0;
                foreach (StartupItem x in sel) if (x.Group == "services") svc++;
                if (svc > 0 && !Ui.Confirm(svc + " service(s) are selected. Disabling a service can break the program that needs it.\n\nDisable the selected items?")) return;
            }
            int ok = 0, bad = 0;
            foreach (StartupItem it in sel)
            {
                try { SetItem(it, enable); ok++; }
                catch (Exception ex) { bad++; Ui.Log.Warn("  failed: " + it.Name + " - " + ex.Message, "Startup"); }
                it.Selected = false;
            }
            Ui.Log.Info((enable ? "Enabled " : "Disabled ") + ok + " item(s)" + (bad > 0 ? ", " + bad + " failed" : "") +
                ". Changes apply at the next sign-in or restart.", "Startup");
            UpdateCount();
        }

        private void OpenLocation()
        {
            StartupItem it = _lv.SelectedItem as StartupItem;
            if (it == null) { Ui.Log.Warn("Click a row first, then press Open location.", "Startup"); return; }
            string cmd = it.Command ?? "";
            string exe = null;
            Match m = Regex.Match(cmd, "^\\s*\"([^\"]+)\"");
            if (m.Success) exe = m.Groups[1].Value;
            else
            {
                m = Regex.Match(cmd, @"^\s*(.+?\.(exe|lnk|bat|cmd|vbs|ps1))(\s|$)", RegexOptions.IgnoreCase);
                if (m.Success) exe = m.Groups[1].Value;
            }
            if (exe != null) exe = Environment.ExpandEnvironmentVariables(exe);
            try
            {
                if (exe != null && File.Exists(exe)) Tool.Start("explorer.exe", "/select,\"" + exe + "\"");
                else if (exe != null && Directory.Exists(Path.GetDirectoryName(exe))) Tool.Start("explorer.exe", "\"" + Path.GetDirectoryName(exe) + "\"");
                else Ui.Log.Warn("Could not find the file of this item.", "Startup");
            }
            catch (Exception ex) { Ui.Log.Warn("Could not open the location: " + ex.Message, "Startup"); }
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
using System.Windows.Threading;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    internal sealed class StartupItem : Notify
    {
        private bool _selected;
        private string _status;
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Group { get; set; }
        public string Command { get; set; }
        public string Source { get; set; }
        public string Ref { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
        public string Status { get { return _status; } set { _status = value; Raise("Status"); } }
    }

    // C# port of src\pages\Startup.ps1: startup apps (registry + folders), logon scheduled tasks and auto services.
    internal sealed class StartupPage : IPage
    {
        private const string ApprBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

        private ListView _lv;
        private ComboBox _kind;
        private TextBox _search;
        private TextBlock _count;
        private DispatcherTimer _debounce;
        private List<StartupItem> _all;
        private bool _loaded;

        public string Key { get { return "Startup"; } }

        public void Init(Window w, Logger log)
        {
            _lv = Ui.Find<ListView>("lvStartup");
            _kind = Ui.Find<ComboBox>("cmbStKind");
            _search = Ui.Find<TextBox>("txtStSearch");
            _count = Ui.Find<TextBlock>("txtStCount");

            foreach (string t in new string[] { "All", "Startup apps", "Scheduled tasks", "Services" }) _kind.Items.Add(t);
            _kind.SelectedIndex = 0;
            _kind.SelectionChanged += delegate { ApplyFilter(); };

            _debounce = new DispatcherTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(250);
            _debounce.Tick += delegate { _debounce.Stop(); ApplyFilter(); };
            _search.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };

            Ui.Find<Button>("btnStRefresh").Click += delegate { Scan(); };
            Ui.Find<Button>("btnStNone").Click += delegate
            {
                if (_all != null) foreach (StartupItem x in _all) x.Selected = false;
                UpdateCount();
            };
            Ui.Find<Button>("btnStDisable").Click += delegate { Toggle(false); };
            Ui.Find<Button>("btnStEnable").Click += delegate { Toggle(true); };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateCount(); }));
            _lv.MouseDoubleClick += delegate
            {
                StartupItem it = _lv.SelectedItem as StartupItem;
                if (it != null) { it.Selected = !it.Selected; UpdateCount(); }
            };
            Ui.Find<Button>("btnStOpen").Click += delegate { OpenLocation(); };
        }

        public void OnShown() { if (!_loaded) Scan(); }

        private void UpdateCount()
        {
            List<StartupItem> src = _lv.ItemsSource as List<StartupItem>;
            int total = src == null ? 0 : src.Count, n = 0;
            if (src != null) foreach (StartupItem x in src) if (x.Selected) n++;
            _count.Text = "Selected: " + n + " of " + total;
        }

        private void ApplyFilter()
        {
            if (_all == null) return;
            string kind = _kind.SelectedItem as string;
            string q = (_search.Text ?? "").Trim();
            List<StartupItem> list = new List<StartupItem>();
            foreach (StartupItem it in _all)
            {
                if (kind == "Startup apps" && it.Group != "apps") continue;
                if (kind == "Scheduled tasks" && it.Group != "tasks") continue;
                if (kind == "Services" && it.Group != "services") continue;
                if (q.Length > 0 && it.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    it.Command.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(it);
            }
            _lv.ItemsSource = list;
            UpdateCount();
        }

        // ---------------- scanning ----------------
        private static bool IsEnabled(string hive, string sub, string name)
        {
            object o = Reg.Get(hive + @":\" + ApprBase + @"\" + sub, name);
            byte[] b = o as byte[];
            if (b != null && b.Length > 0) return (b[0] & 1) == 0;
            return true;
        }

        private static void AddRow(List<StartupItem> rows, string name, string kind, string group, string cmd, string src, string rf, bool en)
        {
            StartupItem it = new StartupItem();
            it.Name = name ?? ""; it.Kind = kind; it.Group = group; it.Command = cmd ?? ""; it.Source = src; it.Ref = rf;
            it.Status = en ? "Enabled" : "Disabled";
            rows.Add(it);
        }

        private static List<StartupItem> ReadAll(WorkCtx c)
        {
            List<StartupItem> rows = new List<StartupItem>();

            c.Progress(-1, "Reading startup programs...");
            string[][] runKeys = new string[][]
            {
                new string[] { "HKCU", @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", "Run", "Registry (current user)" },
                new string[] { "HKLM", @"HKLM:\Software\Microsoft\Windows\CurrentVersion\Run", "Run", "Registry (all users)" },
                new string[] { "HKLM", @"HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", "Registry (all users, 32-bit)" }
            };
            foreach (string[] rk in runKeys)
            {
                try
                {
                    string path = rk[1];
                    RegistryKey root = rk[0] == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.OpenSubKey(path.Substring(path.IndexOf('\\') + 1)))
                    {
                        if (k == null) continue;
                        foreach (string vn in k.GetValueNames())
                        {
                            if (vn.Length == 0) continue;
                            AddRow(rows, vn, "Startup app", "apps", Convert.ToString(k.GetValue(vn)), rk[3],
                                "reg|" + rk[0] + "|" + rk[2] + "|" + vn, IsEnabled(rk[0], rk[2], vn));
                        }
                    }
                }
                catch { }
            }

            string[][] folders = new string[][]
            {
                new string[] { "HKCU", Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder (current user)" },
                new string[] { "HKLM", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)" }
            };
            dynamic wsh = null;
            try { wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); } catch { }
            foreach (string[] f in folders)
            {
                try
                {
                    if (!Directory.Exists(f[1])) continue;
                    foreach (string file in Directory.GetFiles(f[1]))
                    {
                        string fn = Path.GetFileName(file);
                        if (string.Equals(fn, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        string cmd = file;
                        if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && wsh != null)
                        {
                            try { dynamic lk = wsh.CreateShortcut(file); cmd = (Convert.ToString(lk.TargetPath) + " " + Convert.ToString(lk.Arguments)).Trim(); } catch { }
                        }
                        AddRow(rows, Path.GetFileNameWithoutExtension(file), "Startup app", "apps", cmd, f[2],
                            "folder|" + f[0] + "|StartupFolder|" + fn, IsEnabled(f[0], "StartupFolder", fn));
                    }
                }
                catch { }
            }

            c.Progress(-1, "Reading scheduled tasks...");
            try
            {
                dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                svc.Connect();
                WalkTasks(svc.GetFolder("\\"), rows);
            }
            catch { }

            c.Progress(-1, "Reading services...");
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, DisplayName, StartMode, PathName FROM Win32_Service"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject o in col)
                    {
                        string mode = Convert.ToString(o["StartMode"]);
                        if (mode != "Auto" && mode != "Disabled") continue;
                        string pn = Convert.ToString(o["PathName"]);
                        if (string.IsNullOrEmpty(pn)) continue;
                        if (Regex.IsMatch(pn, @"\\Windows\\|\\Windows Defender\\|\\Microsoft Shared\\|WindowsApps|\\Microsoft\.NET\\", RegexOptions.IgnoreCase)) continue;
                        AddRow(rows, Convert.ToString(o["DisplayName"]), "Service", "services", pn,
                            "Service: " + Convert.ToString(o["Name"]), "svc|" + Convert.ToString(o["Name"]), mode != "Disabled");
                    }
                }
            }
            catch { }

            rows.Sort(delegate(StartupItem a, StartupItem b)
            {
                int r = string.Compare(a.Group, b.Group, StringComparison.Ordinal);
                if (r != 0) return r;
                r = (a.Status == "Disabled" ? 1 : 0).CompareTo(b.Status == "Disabled" ? 1 : 0);
                if (r != 0) return r;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return rows;
        }

        private static void WalkTasks(dynamic folder, List<StartupItem> rows)
        {
            try
            {
                foreach (dynamic task in folder.GetTasks(1))
                {
                    try
                    {
                        string full = Convert.ToString(task.Path);
                        int i = full.LastIndexOf('\\');
                        string tpath = full.Substring(0, i + 1);
                        if (tpath.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase)) continue;
                        bool logon = false;
                        foreach (dynamic tr in task.Definition.Triggers)
                        {
                            int type = (int)tr.Type;
                            if (type == 8 || type == 9) logon = true;   // boot / logon
                        }
                        if (!logon) continue;
                        string cmd = "";
                        foreach (dynamic a in task.Definition.Actions)
                        {
                            try { cmd = (Convert.ToString(a.Path) + " " + Convert.ToString(a.Arguments)).Trim(); } catch { }
                            break;
                        }
                        AddRow(rows, Convert.ToString(task.Name), "Scheduled task", "tasks", cmd, "Task: " + tpath,
                            "task|" + tpath + "|" + Convert.ToString(task.Name), (bool)task.Enabled);
                    }
                    catch { }
                }
                foreach (dynamic sub in folder.GetFolders(0)) WalkTasks(sub, rows);
            }
            catch { }
        }

        private void Scan()
        {
            Work.Run("Reading startup items...", delegate(WorkCtx c)
            {
                c.Info("Reading startup items (programs, tasks, services)...", "Startup");
                List<StartupItem> rows = ReadAll(c);
                _all = rows;
                _loaded = true;
                c.UI(delegate { ApplyFilter(); });
                c.Info(rows.Count + " startup items found.", "Startup");
            });
        }

        // ---------------- enable / disable ----------------
        private static void SetItem(StartupItem item, bool enable)
        {
            string[] p = item.Ref.Split(new char[] { '|' }, 4);
            switch (p[0])
            {
                case "reg":
                case "folder":
                    {
                        byte[] bytes;
                        if (enable) bytes = new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                        else
                        {
                            bytes = new byte[12];
                            bytes[0] = 3;
                            byte[] ft = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                            Array.Copy(ft, 0, bytes, 4, 8);
                        }
                        Reg.Set(p[1] + @":\" + ApprBase + @"\" + p[2], p[3], bytes, RegistryValueKind.Binary);
                        break;
                    }
                case "task":
                    {
                        string[] tn = item.Ref.Split(new char[] { '|' }, 3);
                        dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                        svc.Connect();
                        dynamic task = svc.GetFolder(tn[1]).GetTask(tn[2]);
                        task.Enabled = enable;
                        break;
                    }
                case "svc":
                    {
                        using (ManagementObject o = new ManagementObject("Win32_Service.Name='" + p[1] + "'"))
                        {
                            object r = o.InvokeMethod("ChangeStartMode", new object[] { enable ? "Automatic" : "Disabled" });
                            if (Convert.ToInt32(r) != 0) throw new InvalidOperationException("ChangeStartMode returned " + r);
                        }
                        break;
                    }
            }
            item.Status = enable ? "Enabled" : "Disabled";
        }

        private void Toggle(bool enable)
        {
            List<StartupItem> src = _lv.ItemsSource as List<StartupItem>;
            List<StartupItem> sel = new List<StartupItem>();
            if (src != null) foreach (StartupItem x in src) if (x.Selected) sel.Add(x);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the items you want to change first.", "Startup"); return; }
            if (!enable)
            {
                int svc = 0;
                foreach (StartupItem x in sel) if (x.Group == "services") svc++;
                if (svc > 0 && !Ui.Confirm(svc + " service(s) are selected. Disabling a service can break the program that needs it.\n\nDisable the selected items?")) return;
            }
            int ok = 0, bad = 0;
            foreach (StartupItem it in sel)
            {
                try { SetItem(it, enable); ok++; }
                catch (Exception ex) { bad++; Ui.Log.Warn("  failed: " + it.Name + " - " + ex.Message, "Startup"); }
                it.Selected = false;
            }
            Ui.Log.Info((enable ? "Enabled " : "Disabled ") + ok + " item(s)" + (bad > 0 ? ", " + bad + " failed" : "") +
                ". Changes apply at the next sign-in or restart.", "Startup");
            UpdateCount();
        }

        private void OpenLocation()
        {
            StartupItem it = _lv.SelectedItem as StartupItem;
            if (it == null) { Ui.Log.Warn("Click a row first, then press Open location.", "Startup"); return; }
            string cmd = it.Command ?? "";
            string exe = null;
            Match m = Regex.Match(cmd, "^\\s*\"([^\"]+)\"");
            if (m.Success) exe = m.Groups[1].Value;
            else
            {
                m = Regex.Match(cmd, @"^\s*(.+?\.(exe|lnk|bat|cmd|vbs|ps1))(\s|$)", RegexOptions.IgnoreCase);
                if (m.Success) exe = m.Groups[1].Value;
            }
            if (exe != null) exe = Environment.ExpandEnvironmentVariables(exe);
            try
            {
                if (exe != null && File.Exists(exe)) Tool.Start("explorer.exe", "/select,\"" + exe + "\"");
                else if (exe != null && Directory.Exists(Path.GetDirectoryName(exe))) Tool.Start("explorer.exe", "\"" + Path.GetDirectoryName(exe) + "\"");
                else Ui.Log.Warn("Could not find the file of this item.", "Startup");
            }
            catch (Exception ex) { Ui.Log.Warn("Could not open the location: " + ex.Message, "Startup"); }
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
