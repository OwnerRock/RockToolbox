using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace RockToolbox.Native
{
    internal sealed class PkgRow
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string ChocoId { get; set; }
        public string Category { get; set; }
    }

    internal sealed class UpgRow : Notify
    {
        private bool _sel;
        public string Name { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string Available { get; set; }
        public string Source { get; set; }
        public bool Selected { get { return _sel; } set { _sel = value; Raise("Selected"); } }
    }

    // The two extra views of the Install page: "Package ID" (ready list of every app with WinGet / Chocolatey IDs)
    // and "Winget Upgrade" (apps that have a newer version). Port of src\pages\WingetIdUpgrade.ps1.
    internal sealed class InstallExtras
    {
        private ListView _lvFind, _lvUpg;
        private ComboBox _mgr, _cat;
        private TextBox _query;
        private TextBlock _findInfo, _upgCount, _upgInfo;
        private Panel _apps, _find, _upg;
        private Button _btnFind, _btnUpg;
        private readonly List<PkgRow> _all = new List<PkgRow>();
        private bool _upgLoaded;
        private DispatcherTimer _deb;

        private static string ChocoId(string id)
        {
            string s = id.Trim();
            if (Regex.IsMatch(s, "^python\\.python\\.", RegexOptions.IgnoreCase)) return "python";
            if (s.Contains(".")) { string[] p = s.Split('.'); s = p[p.Length - 1]; }
            return Regex.Replace(s, "[^A-Za-z0-9._-]", "").ToLowerInvariant();
        }

        private bool Choco { get { return _mgr.SelectedIndex == 1; } }
        private string IdOf(PkgRow r) { return Choco ? r.ChocoId : r.Id; }

        public void Init()
        {
            _apps = Ui.Find<Panel>("pnlInstallApps"); _find = Ui.Find<Panel>("pnlFind"); _upg = Ui.Find<Panel>("pnlUpg");
            _btnFind = Ui.Find<Button>("btnShowFind"); _btnUpg = Ui.Find<Button>("btnShowUpg");
            _lvFind = Ui.Find<ListView>("lvFind"); _lvUpg = Ui.Find<ListView>("lvUpg");
            _mgr = Ui.Find<ComboBox>("cmbFindMgr"); _cat = Ui.Find<ComboBox>("cmbFindSource"); _query = Ui.Find<TextBox>("txtFindQuery");
            _findInfo = Ui.Find<TextBlock>("txtFindInfo"); _upgCount = Ui.Find<TextBlock>("txtUpgCount"); _upgInfo = Ui.Find<TextBlock>("txtUpgInfo");

            List<AppEntry> sorted = new List<AppEntry>(AppData.All);
            sorted.Sort(delegate(AppEntry a, AppEntry b) { int r = string.Compare(a.Cat, b.Cat, StringComparison.OrdinalIgnoreCase); return r != 0 ? r : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            foreach (AppEntry a in sorted) { PkgRow p = new PkgRow(); p.Name = a.Name; p.Id = a.Id; p.Category = a.Cat; p.ChocoId = ChocoId(a.Id); _all.Add(p); }
            _mgr.Items.Add("WinGet ID"); _mgr.Items.Add("Chocolatey ID");
            _mgr.SelectedIndex = InstallPage.Instance.UseChocoPublic ? 1 : 0;
            _cat.Items.Add("All categories"); foreach (string c in AppData.Cats) _cat.Items.Add(c);
            _cat.SelectedIndex = 0;
            UpdateFind();
            _deb = new DispatcherTimer(); _deb.Interval = TimeSpan.FromMilliseconds(250);
            _deb.Tick += delegate { _deb.Stop(); UpdateFind(); };
            _query.TextChanged += delegate { _deb.Stop(); _deb.Start(); };
            _cat.SelectionChanged += delegate { UpdateFind(); };
            _mgr.SelectionChanged += delegate { UpdateFind(); };

            _btnFind.Click += delegate { View(_find.Visibility == Visibility.Visible ? "Apps" : "Find"); };
            _btnUpg.Click += delegate { View(_upg.Visibility == Visibility.Visible ? "Apps" : "Upg"); };
            Ui.Find<Button>("btnFindCopy").Click += delegate { CopyId(); };
            _lvFind.MouseDoubleClick += delegate { CopyId(); };
            Ui.Find<Button>("btnFindSend").Click += delegate
            {
                PkgRow r = _lvFind.SelectedItem as PkgRow;
                if (r == null) { Ui.Log.Warn("Select an app first.", "Find"); return; }
                InstallPage.Instance.CustomBox.Text = IdOf(r);
                if (Choco) InstallPage.Instance.ChocoRadio.IsChecked = true; else InstallPage.Instance.WingetRadio.IsChecked = true;
                View("Apps");
                Ui.Log.Info("ID sent to the Install page: " + IdOf(r), "Find");
            };
            Ui.Find<Button>("btnFindInstall").Click += delegate
            {
                PkgRow r = _lvFind.SelectedItem as PkgRow;
                if (r == null) { Ui.Log.Warn("Select an app first.", "Find"); return; }
                InstallPage.RunQueue("install", new List<string> { IdOf(r) }, Choco);
            };

            Ui.Find<Button>("btnUpgScan").Click += delegate { ScanUpgrades(); };
            Ui.Find<Button>("btnUpgSelAll").Click += delegate { foreach (UpgRow u in Rows()) u.Selected = true; UpdateUpgCount(); };
            Ui.Find<Button>("btnUpgSelNone").Click += delegate { foreach (UpgRow u in Rows()) u.Selected = false; UpdateUpgCount(); };
            Ui.Find<Button>("btnUpgRun").Click += delegate { Upgrade(false); };
            Ui.Find<Button>("btnUpgAll").Click += delegate { Upgrade(true); };
            _lvUpg.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { UpdateUpgCount(); }));
            _lvUpg.MouseDoubleClick += delegate { UpgRow u = _lvUpg.SelectedItem as UpgRow; if (u != null) { u.Selected = !u.Selected; UpdateUpgCount(); } };
        }

        private void View(string v)
        {
            _apps.Visibility = v == "Apps" ? Visibility.Visible : Visibility.Collapsed;
            _find.Visibility = v == "Find" ? Visibility.Visible : Visibility.Collapsed;
            _upg.Visibility = v == "Upg" ? Visibility.Visible : Visibility.Collapsed;
            _btnFind.Background = Ui.Brush(v == "Find" ? "#4A78B0" : "#1E3547"); _btnFind.Foreground = Ui.Brush(v == "Find" ? "#FFFFFF" : "#F0F4FF");
            _btnUpg.Background = Ui.Brush(v == "Upg" ? "#4A78B0" : "#1E3547"); _btnUpg.Foreground = Ui.Brush(v == "Upg" ? "#FFFFFF" : "#F0F4FF");
            if (v == "Upg" && !_upgLoaded) ScanUpgrades();
        }

        private void UpdateFind()
        {
            string q = (_query.Text ?? "").Trim(); string cat = _cat.SelectedItem as string;
            List<PkgRow> view = new List<PkgRow>();
            foreach (PkgRow p in _all)
            {
                if (!string.IsNullOrEmpty(cat) && cat != "All categories" && p.Category != cat) continue;
                if (q.Length > 0 && p.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && p.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && p.ChocoId.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                view.Add(p);
            }
            _lvFind.ItemsSource = view;
            _findInfo.Text = view.Count + " " + Lang.L("of") + " " + _all.Count + " apps. Select a row, then Copy ID / Use in Install tab / Install selected (double-click copies the ID).";
        }

        private void CopyId()
        {
            PkgRow r = _lvFind.SelectedItem as PkgRow;
            if (r == null) { Ui.Log.Warn("Select an app first.", "Find"); return; }
            try { Clipboard.SetText(IdOf(r)); Ui.Log.Info("Copied ID: " + IdOf(r), "Find"); } catch (Exception ex) { Ui.Log.Error("Could not copy: " + ex.Message, "Find"); }
        }

        // ---------------- winget upgrade ----------------
        private List<UpgRow> Rows() { return (_lvUpg.ItemsSource as List<UpgRow>) ?? new List<UpgRow>(); }

        private void UpdateUpgCount()
        {
            List<UpgRow> all = Rows(); int n = 0; foreach (UpgRow u in all) if (u.Selected) n++;
            _upgCount.Text = "Selected: " + n + " of " + all.Count;
        }

        // winget pads every column to its longest value and separates the columns by ONE space,
        // so the columns are cut at the positions of the header words (not at double spaces)
        private static List<UpgRow> ReadUpgrades(string file)
        {
            List<UpgRow> items = new List<UpgRow>();
            if (!File.Exists(file)) return items;
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            bool inTable = false;
            string prev = "";
            List<int> starts = new List<int>();
            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string ln = raw.TrimEnd();
                if (Regex.IsMatch(ln, @"^[\s\-\\|/]*-{8,}\s*$"))
                {
                    inTable = true; starts.Clear();
                    string hdr = Regex.Replace(prev, @"^[\s\-\\|/]+(?=[A-Za-z])", m => new string(' ', m.Length));
                    foreach (Match m in Regex.Matches(hdr, @"\S+")) starts.Add(m.Index);
                    continue;
                }
                prev = ln;
                if (!inTable) continue;
                if (ln.Trim().Length == 0) { inTable = false; continue; }
                if (Regex.IsMatch(ln, "upgrades? available") || Regex.IsMatch(ln, "explicit targeting")) { inTable = false; continue; }
                List<string> parts = new List<string>();
                if (starts.Count >= 4)
                {
                    for (int k = 0; k < starts.Count; k++)
                    {
                        int s = starts[k];
                        if (s >= ln.Length) { parts.Add(""); continue; }
                        int e = k + 1 < starts.Count ? Math.Min(starts[k + 1], ln.Length) : ln.Length;
                        parts.Add(ln.Substring(s, Math.Max(0, e - s)).Trim());
                    }
                }
                else foreach (string x in Regex.Split(ln.Trim(), @"\s{2,}")) if (x.Length > 0) parts.Add(x);
                if (parts.Count < 4) continue;
                string id = parts[1].Trim();
                if (id.Length == 0 || !Regex.IsMatch(id, @"^\S+$") || Regex.IsMatch(id, @"^[\d\.]+$")) continue;
                if (seen.ContainsKey(id.ToLowerInvariant())) continue;
                seen[id.ToLowerInvariant()] = true;
                UpgRow u = new UpgRow(); u.Name = parts[0].Trim(); u.Id = id; u.Version = parts[2].Trim(); u.Available = parts[3].Trim();
                if (parts.Count >= 5) u.Source = parts[4].Trim();
                items.Add(u);
            }
            return items;
        }

        private static void WingetTable(WorkCtx c, string wg, string args, string tmp)
        {
            try { File.Delete(tmp); } catch { }
            c.Tool(Environment.GetEnvironmentVariable("ComSpec"), "/d /c \"mode con: cols=400 lines=9999 >nul 2>&1 & \"" + wg + "\" " + args + " > \"" + tmp + "\"\"");
        }

        private void ScanUpgrades()
        {
            Work.Run("Checking for app updates...", delegate(WorkCtx c)
            {
                string wg = InstallPage.EnsureWinget(c);
                if (wg == null) { c.Warn("winget is not available. Install \"App Installer\" from the Microsoft Store.", "Upgrade"); return; }
                c.Info("Checking which apps have updates (winget upgrade)...", "Upgrade");
                string tmp = Path.Combine(Path.GetTempPath(), "rock_winget_upgrade.txt");
                WingetTable(c, wg, "upgrade --accept-source-agreements", tmp);
                List<UpgRow> items = ReadUpgrades(tmp);
                if (items.Count == 0)
                {
                    c.Info("  nothing listed - refreshing the winget sources and trying again...", "Upgrade");
                    c.Tool(wg, "source update");
                    WingetTable(c, wg, "upgrade --accept-source-agreements", tmp);
                    items = ReadUpgrades(tmp);
                }
                c.UI(delegate
                {
                    _lvUpg.ItemsSource = items; _upgLoaded = true; UpdateUpgCount();
                    _upgInfo.Text = items.Count == 0 ? "Everything is up to date (or winget could not read the list - press Check for updates again)."
                        : items.Count + " app(s) can be updated. Tick the ones you want, then press Update selected.";
                });
                c.Info(items.Count == 0 ? "No app updates found." : items.Count + " app(s) have updates available.", "Upgrade");
            });
        }

        private void Upgrade(bool all)
        {
            List<string> ids = new List<string>();
            foreach (UpgRow u in Rows()) if (all || u.Selected) ids.Add(u.Id);
            if (ids.Count == 0) { Ui.Log.Warn("Tick the apps you want to update first.", "Upgrade"); return; }
            Work.Run("Updating apps...", delegate(WorkCtx c)
            {
                string wg = InstallPage.EnsureWinget(c);
                if (wg == null) { c.Warn("winget is not available.", "Upgrade"); return; }
                int n = ids.Count, i = 0;
                foreach (string id in ids)
                {
                    c.ThrowIfCancelled();
                    c.Progress(100.0 * i / n, "upgrade: " + id + "  (" + (i + 1) + "/" + n + ")");
                    c.Info("winget upgrade : " + id, "Upgrade");
                    int code = c.Tool(wg, "upgrade --id " + id + " -e --silent --accept-source-agreements --accept-package-agreements", null, Encoding.UTF8);
                    if (code == 0 || code == 3010 || code == -1978335189) c.Info("  OK", "Upgrade"); else c.Warn("  exit code " + code, "Upgrade");
                    i++;
                }
                c.Info("Update queue finished. Re-checking the list...", "Upgrade");
                string tmp = Path.Combine(Path.GetTempPath(), "rock_winget_upgrade.txt");
                WingetTable(c, wg, "upgrade --accept-source-agreements", tmp);
                List<UpgRow> items = ReadUpgrades(tmp);
                c.UI(delegate { _lvUpg.ItemsSource = items; UpdateUpgCount(); });
            });
        }
    }
}
