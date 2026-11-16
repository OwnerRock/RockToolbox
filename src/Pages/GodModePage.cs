using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RockToolbox.Native
{
    internal sealed class GodEntry
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int Index { get; set; }
    }

    // C# port of src\pages\GodMode.ps1: lists every entry of the GodMode shell folder, filter + open.
    internal sealed class GodModePage : IPage
    {
        private const string Guid = "{ED7BA470-8E54-465E-825C-99712043E01C}";
        private const string AllCats = "All categories";

        private static readonly Dictionary<int, string> CatMap = new Dictionary<int, string>
        {
            { 1, "Appearance and Personalization" }, { 2, "Hardware and Sound" }, { 3, "Network and Internet" },
            { 4, "Sounds, Speech and Audio Devices" }, { 5, "System and Security" }, { 6, "Clock, Language and Region" },
            { 7, "Ease of Access" }, { 8, "Programs" }, { 9, "User Accounts" }, { 10, "Security Center" }, { 11, "Mobile PC" }
        };

        private Logger _log;
        private ComboBox _cmbCat;
        private TextBox _txtQuery;
        private ListBox _list;
        private TextBlock _info;
        private DispatcherTimer _debounce;
        private bool _loaded;
        private bool _loading;

        private readonly List<GodEntry> _all = new List<GodEntry>();
        private readonly List<object> _items = new List<object>();   // original shell items, same order as _all

        public string Key { get { return "God Mode"; } }

        public void Init(Window w, Logger log)
        {
            _log = log;
            _cmbCat = (ComboBox)w.FindName("cmbGodCat");
            _txtQuery = (TextBox)w.FindName("txtGodQuery");
            _list = (ListBox)w.FindName("lvGod");
            _info = (TextBlock)w.FindName("txtGodInfo");

            _debounce = new DispatcherTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(250);
            _debounce.Tick += delegate { _debounce.Stop(); UpdateList(); };
            _txtQuery.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };
            _cmbCat.SelectionChanged += delegate { if (!_loading) UpdateList(); };
            _list.MouseDoubleClick += delegate { OpenSelected(); };

            ((Button)w.FindName("btnGodOpen")).Click += delegate { OpenSelected(); };
            ((Button)w.FindName("btnGodRefresh")).Click += delegate { Load(); };
            ((Button)w.FindName("btnGodFolder")).Click += delegate { OpenFolder(); };
            ((Button)w.FindName("btnGodDesktop")).Click += delegate { CreateOnDesktop(); };
        }

        public void OnShown()
        {
            if (!_loaded) Load();
        }

        private void Load()
        {
            _all.Clear();
            _items.Clear();
            try
            {
                Type t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) throw new InvalidOperationException("Shell.Application is not available.");
                dynamic shell = Activator.CreateInstance(t);
                dynamic folder = shell.NameSpace("shell:::" + Guid);
                if (folder == null) folder = shell.NameSpace("::" + Guid);
                if (folder == null) throw new InvalidOperationException("Windows could not open the GodMode shell folder.");

                // find the Category / Comments columns by header name (English Windows)
                int catCol = -1, descCol = -1;
                for (int c = 0; c < 40; c++)
                {
                    string h = Convert.ToString(folder.GetDetailsOf(null, c));
                    if (string.IsNullOrEmpty(h)) continue;
                    if (catCol < 0 && h == "Category") catCol = c;
                    if (descCol < 0 && (h == "Comments" || h == "Description")) descCol = c;
                }

                dynamic col = folder.Items();
                int count = col.Count;
                List<KeyValuePair<GodEntry, object>> rows = new List<KeyValuePair<GodEntry, object>>();
                for (int i = 0; i < count; i++)
                {
                    dynamic it = col.Item(i);
                    string name = Convert.ToString(it.Name);
                    if (string.IsNullOrEmpty(name)) continue;

                    string cat = "";
                    if (catCol >= 0) { try { cat = Convert.ToString(folder.GetDetailsOf(it, catCol)); } catch { } }
                    if (string.IsNullOrEmpty(cat))
                    {
                        try
                        {
                            object v = it.ExtendedProperty("System.ControlPanel.Category");
                            Array arr = v as Array;
                            if (arr != null && arr.Length > 0) v = arr.GetValue(0);
                            int n;
                            if (v != null && int.TryParse(Convert.ToString(v), out n) && CatMap.ContainsKey(n)) cat = CatMap[n];
                        }
                        catch { }
                    }
                    if (string.IsNullOrEmpty(cat)) cat = "Other";

                    string desc = "";
                    if (descCol >= 0) { try { desc = Convert.ToString(folder.GetDetailsOf(it, descCol)); } catch { } }

                    GodEntry e = new GodEntry { Name = name, Category = cat, Description = desc };
                    rows.Add(new KeyValuePair<GodEntry, object>(e, (object)it));
                }

                rows.Sort(delegate(KeyValuePair<GodEntry, object> a, KeyValuePair<GodEntry, object> b)
                {
                    int r = string.Compare(a.Key.Category, b.Key.Category, StringComparison.OrdinalIgnoreCase);
                    return r != 0 ? r : string.Compare(a.Key.Name, b.Key.Name, StringComparison.OrdinalIgnoreCase);
                });
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].Key.Index = i;
                    _all.Add(rows[i].Key);
                    _items.Add(rows[i].Value);
                }
                _loaded = true;
            }
            catch (Exception ex)
            {
                _log.Error("God Mode: " + ex.Message, "GodMode");
            }

            string sel = _cmbCat.SelectedItem as string;
            _loading = true;
            _cmbCat.Items.Clear();
            _cmbCat.Items.Add(AllCats);
            List<string> seen = new List<string>();
            foreach (GodEntry g in _all)
                if (!seen.Contains(g.Category)) { seen.Add(g.Category); _cmbCat.Items.Add(g.Category); }
            if (sel != null && _cmbCat.Items.Contains(sel)) _cmbCat.SelectedItem = sel; else _cmbCat.SelectedIndex = 0;
            _loading = false;
            UpdateList();
        }

        private void UpdateList()
        {
            string q = (_txtQuery.Text ?? "").Trim();
            string cat = _cmbCat.SelectedItem as string;
            List<GodEntry> view = new List<GodEntry>();
            foreach (GodEntry g in _all)
            {
                if (!string.IsNullOrEmpty(cat) && cat != AllCats && g.Category != cat) continue;
                if (q.Length > 0 &&
                    g.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (g.Description ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                view.Add(g);
            }
            _list.ItemsSource = view;
            _info.Text = view.Count + " of " + _all.Count + " settings. Select a row and press Open selected (or double-click it).";
        }

        private void OpenSelected()
        {
            GodEntry g = _list.SelectedItem as GodEntry;
            if (g == null) { _log.Warn("Select a setting first.", "GodMode"); return; }
            try
            {
                dynamic item = _items[g.Index];
                item.InvokeVerb();
                _log.Info("Opened: " + g.Name, "GodMode");
            }
            catch (Exception ex)
            {
                _log.Error("Could not open \"" + g.Name + "\": " + ex.Message, "GodMode");
            }
        }

        private void OpenFolder()
        {
            try { Process.Start("explorer.exe", "shell:::" + Guid); }
            catch (Exception ex) { _log.Error("Could not open the GodMode folder: " + ex.Message, "GodMode"); }
        }

        private void CreateOnDesktop()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GodMode." + Guid);
                if (Directory.Exists(path)) _log.Info("GodMode folder already exists on the Desktop.", "GodMode");
                else { Directory.CreateDirectory(path); _log.Info("GodMode folder created on the Desktop.", "GodMode"); }
            }
            catch (Exception ex) { _log.Error("Could not create the folder: " + ex.Message, "GodMode"); }
        }
    }
}
