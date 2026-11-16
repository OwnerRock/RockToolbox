using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    internal sealed class InstApp : Notify
    {
        private bool _selected;
        public string Name { get; set; }
        public string Version { get; set; }
        public string Publisher { get; set; }
        public string Source { get; set; }
        public string Size { get; set; }
        public string Key { get; set; }
        public string Cmd { get; set; }
        public string Quiet { get; set; }
        public bool Selected { get { return _selected; } set { _selected = value; Raise("Selected"); } }
    }

    // C# port of src\pages\UninstallApps.ps1: desktop programs (registry) + Store apps (WinRT PackageManager).
    internal sealed class UninstallAppsPage : IPage
    {
        private ListView _lv;
        private ComboBox _type;
        private TextBox _query;
        private TextBlock _count, _info;
        private DispatcherTimer _debounce;
        private readonly List<InstApp> _all = new List<InstApp>();
        private bool _loaded;

        public string Key { get { return "Uninstall Apps"; } }

        public void Init(Window w, Logger log)
        {
            _lv = Ui.Find<ListView>("lvUnApps");
            _type = Ui.Find<ComboBox>("cmbUnType");
            _query = Ui.Find<TextBox>("txtUnQuery");
            _count = Ui.Find<TextBlock>("txtUnCount");
            _info = Ui.Find<TextBlock>("txtUnInfo");
            foreach (string t in new string[] { "All apps", "Desktop apps", "Store apps" }) _type.Items.Add(t);
            _type.SelectedIndex = 0;
            _type.SelectionChanged += delegate { UpdateView(); };
            _debounce = new DispatcherTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(250);
            _debounce.Tick += delegate { _debounce.Stop(); UpdateView(); };
            _query.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };
            Ui.Find<Button>("btnUnRefresh").Click += delegate { Scan(); };
            Ui.Find<Button>("btnUnSelNone").Click += delegate { foreach (InstApp a in _all) a.Selected = false; UpdateCount(); };
            Ui.Find<Button>("btnUnGo").Click += delegate { Uninstall(); };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateCount(); }));
            _lv.MouseDoubleClick += delegate
            {
                InstApp it = _lv.SelectedItem as InstApp;
                if (it != null) { it.Selected = !it.Selected; UpdateCount(); }
            };
        }

        public void OnShown() { if (!_loaded) Scan(); }

        private void UpdateCount()
        {
            int n = 0;
            foreach (InstApp a in _all) if (a.Selected) n++;
            _count.Text = "Selected: " + n;
        }

        private void UpdateView()
        {
            string q = (_query.Text ?? "").Trim();
            string t = _type.SelectedItem as string;
            List<InstApp> view = new List<InstApp>();
            foreach (InstApp a in _all)
            {
                if (t == "Desktop apps" && a.Source != "Desktop") continue;
                if (t == "Store apps" && a.Source != "Store") continue;
                if (q.Length > 0 && a.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (a.Publisher ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                view.Add(a);
            }
            _lv.ItemsSource = view;
            UpdateCount();
            _info.Text = view.Count + " of " + _all.Count + " apps shown. Tick what you want to remove, then press Uninstall selected.";
        }

        private static string FormatSize(object est)
        {
            if (est == null) return "";
            double kb;
            try { kb = Convert.ToDouble(est); } catch { return ""; }
            if (kb <= 0) return "";
            double mb = kb / 1024.0;
            if (mb >= 1024) return (mb / 1024).ToString("0.0") + " GB";
            if (mb >= 1) return mb.ToString("0") + " MB";
            return kb.ToString("0") + " KB";
        }

        private static List<InstApp> ReadAll()
        {
            List<InstApp> rows = new List<InstApp>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            object[][] roots = new object[][]
            {
                new object[] { hklm, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" },
                new object[] { hklm, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" },
                new object[] { hkcu, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" }
            };
            foreach (object[] r in roots)
            {
                try
                {
                    using (RegistryKey parent = ((RegistryKey)r[0]).OpenSubKey((string)r[1]))
                    {
                        if (parent == null) continue;
                        foreach (string sk in parent.GetSubKeyNames())
                        {
                            try
                            {
                                using (RegistryKey k = parent.OpenSubKey(sk))
                                {
                                    string n = Convert.ToString(k.GetValue("DisplayName"));
                                    if (string.IsNullOrEmpty(n)) continue;
                                    if (Convert.ToString(k.GetValue("SystemComponent")) == "1") continue;
                                    if (k.GetValue("ParentKeyName") != null) continue;
                                    if (Regex.IsMatch(n, @"\(KB\d+\)")) continue;
                                    string cmd = Convert.ToString(k.GetValue("UninstallString"));
                                    string q = Convert.ToString(k.GetValue("QuietUninstallString"));
                                    if (string.IsNullOrEmpty(cmd) && string.IsNullOrEmpty(q)) continue;
                                    string ver = Convert.ToString(k.GetValue("DisplayVersion"));
                                    string id = n + "|" + ver;
                                    if (seen.ContainsKey(id)) continue;
                                    seen[id] = true;
                                    InstApp a = new InstApp();
                                    a.Name = n; a.Version = ver; a.Publisher = Convert.ToString(k.GetValue("Publisher"));
                                    a.Source = "Desktop"; a.Size = FormatSize(k.GetValue("EstimatedSize"));
                                    a.Key = sk; a.Cmd = cmd; a.Quiet = q;
                                    rows.Add(a);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            try
            {
                foreach (AppxInfo p in Appx.List())
                {
                    if (p.IsFramework || p.NonRemovable || p.IsSystem) continue;
                    InstApp a = new InstApp();
                    a.Name = p.Name; a.Version = p.Version; a.Publisher = p.Publisher; a.Source = "Store";
                    a.Size = ""; a.Key = p.FullName; a.Cmd = ""; a.Quiet = "";
                    rows.Add(a);
                }
            }
            catch (Exception ex) { Ui.Log.Warn("Store apps could not be listed: " + ex.Message, "Uninstall"); }
            rows.Sort(delegate(InstApp x, InstApp y) { return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase); });
            return rows;
        }

        private void Scan()
        {
            Work.Run("Reading installed apps...", delegate(WorkCtx c)
            {
                c.Info("Listing installed apps...", "Uninstall");
                List<InstApp> rows = ReadAll();
                c.UI(delegate { _all.Clear(); _all.AddRange(rows); _loaded = true; UpdateView(); });
                c.Info(rows.Count + " apps found.", "Uninstall");
            });
        }

        private void Uninstall()
        {
            List<InstApp> sel = new List<InstApp>();
            foreach (InstApp a in _all) if (a.Selected) sel.Add(a);
            if (sel.Count == 0) { Ui.Log.Warn("Tick the apps you want to remove first.", "Uninstall"); return; }
            StringBuilder names = new StringBuilder();
            for (int i = 0; i < sel.Count && i < 8; i++) names.Append("- " + sel[i].Name + "\n");
            if (sel.Count > 8) names.Append("... and " + (sel.Count - 8) + " more");
            if (!Ui.Confirm("Uninstall " + sel.Count + " app(s)?\n\n" + names)) return;

            Work.Run("Uninstalling apps...", delegate(WorkCtx c)
            {
                int ok = 0, fail = 0, n = sel.Count, i = 0;
                foreach (InstApp it in sel)
                {
                    if (c.Cancelled) { c.Warn("Stopped - the remaining apps were skipped.", "Uninstall"); break; }
                    i++;
                    c.Progress((i - 1) * 100.0 / n, "Uninstalling (" + i + "/" + n + "): " + it.Name);
                    c.Info("Uninstalling (" + i + "/" + n + "): " + it.Name, "Uninstall");
                    try
                    {
                        if (it.Source == "Store")
                        {
                            Appx.Remove(it.Key);
                            ok++;
                            c.Info("  removed", "Uninstall");
                        }
                        else
                        {
                            string cmd = it.Quiet;
                            bool hidden = true;
                            if (string.IsNullOrEmpty(cmd)) { cmd = it.Cmd; }
                            Match m = Regex.Match(cmd, @"msiexec(\.exe)?\W*\s+.*?/[IiXx]\s*(\{[0-9A-Fa-f-]{36}\})");
                            if (m.Success) { cmd = "msiexec.exe /x " + m.Groups[2].Value + " /qn /norestart"; hidden = true; }
                            int code = Tool.RunLong(Environment.GetEnvironmentVariable("ComSpec"), "/c \"" + cmd + "\"", hidden, 600000);
                            if (code == 0 || code == 3010 || code == 1641) { ok++; c.Info("  done", "Uninstall"); }
                            else { fail++; c.Warn("  uninstaller ended with code " + code + " (it may have been cancelled)", "Uninstall"); }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { fail++; c.Warn("  failed: " + ex.Message, "Uninstall"); }
                    c.Progress(i * 100.0 / n, null);
                }
                c.Info("Finished: " + ok + " removed, " + fail + " failed.", "Uninstall");
                List<InstApp> rows = ReadAll();
                c.UI(delegate { _all.Clear(); _all.AddRange(rows); UpdateView(); });
            });
        }
    }
}
