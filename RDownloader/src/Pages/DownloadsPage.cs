using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Threading;

namespace RDownloader.Native
{
    // C# port of the page part of src\pages\Downloads.ps1 (queue, pause / resume, speed limit, saved list). The engine is DlEngine.cs.
    internal sealed partial class DownloadsPage : IPage
    {
        private static readonly string[] LimitNames = { "Unlimited", "256 KB/s", "512 KB/s", "1 MB/s", "2 MB/s", "5 MB/s", "10 MB/s", "20 MB/s" };
        private static readonly long[] LimitKb = { 0, 256, 512, 1024, 2048, 5120, 10240, 20480 };

        private readonly List<RockDownload> _items = new List<RockDownload>();
        private readonly Dictionary<string, IDlJob> _engines = new Dictionary<string, IDlJob>();
        public static DownloadsPage Instance;
        public bool HasActive { get { foreach (IDlJob e in _jobs.Values) if (!e.Finished) return true; return false; } }
        private readonly Dictionary<string, IDlJob> _jobs = new Dictionary<string, IDlJob>();
        private ListView _lv;
        private ComboBox _max, _limit, _conn;
        private TreeView _tree;
        private string _cat = "all";
        private TextBlock _catTitle;
        private bool _queueStopped;
        private TextBox _search, _dir;
        private TextBlock _count, _info;
        private Border _savePanel;
        private DispatcherTimer _timer, _saveTimer, _debounce, _fast;
        private DateTime _smT = DateTime.UtcNow;
        private bool _ticking;
        private int _saveTick;

        public string Key { get { return "Downloads"; } }
        public void OnShown() { ExtGuide.ShowFirstTime(); }

        internal static string FormatSize(double b)
        {
            if (b >= 1073741824.0) return (b / 1073741824.0).ToString("0.0") + " GB";
            if (b >= 1048576.0) return (b / 1048576.0).ToString("0.0") + " MB";
            if (b >= 1024.0) return (b / 1024.0).ToString("0") + " KB";
            return b.ToString("0") + " B";
        }

        // the live counters of the progress window: one more digit, so every step of the count is visible
        internal static string FormatLive(double b)
        {
            if (b >= 1073741824.0) return (b / 1073741824.0).ToString("0.000", CultureInfo.InvariantCulture) + " GB";
            if (b >= 1048576.0) return (b / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
            if (b >= 1024.0) return (b / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return b.ToString("0") + " B";
        }

        internal static string FormatEta(double sec)
        {
            if (sec < 0 || sec > 8640000) return "-";
            TimeSpan t = TimeSpan.FromSeconds(Math.Round(sec));
            if (t.TotalHours >= 1) return (int)Math.Floor(t.TotalHours) + "h " + t.Minutes.ToString("00") + "m";
            if (t.TotalMinutes >= 1) return t.Minutes + "m " + t.Seconds.ToString("00") + "s";
            return t.Seconds + "s";
        }

        private static void SetStatus(RockDownload it, string v)
        {
            it.Status = v; it.StatusText = Lang.L(v);
            if (v == "Downloading" || v == "Completed" || v == "Failed" || v == "Paused")
                it.LastTry = DateTime.Now.ToString("MMM dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        internal string DlDir
        {
            get
            {
                string d = (Ui.Settings.GetString("DlDir") ?? "").Trim();
                if (d.Length == 0) d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Downloads\R-Downloader");
                return d;
            }
        }

        private long LimitBytes { get { int i = _limit.SelectedIndex; return i > 0 ? LimitKb[i] * 1024 : 0; } }
        private int Conns { get { try { return DlOpts.CapConns(Math.Max(1, (int)_conn.SelectedItem)); } catch { return 16; } } }

        private static string SafeName(string name)
        {
            string n = name ?? "";
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c.ToString(), "_");
            n = n.Trim().TrimEnd('.');
            if (n.Length == 0) n = "download";
            if (n.Length > 180) n = n.Substring(0, 180);
            return n;
        }

        private static string NameFromUrl(Uri uri)
        {
            try
            {
                foreach (string pair in uri.Query.TrimStart('?').Split('&'))
                {
                    int i = pair.IndexOf('='); if (i <= 0) continue;
                    string k = pair.Substring(0, i).ToLowerInvariant(), v = pair.Substring(i + 1);
                    if (Array.IndexOf(new string[] { "file", "filename", "fname", "name", "title", "f", "download", "dl", "attachment", "out" }, k) < 0) continue;
                    try { v = Uri.UnescapeDataString(v.Replace('+', ' ')); } catch { }
                    v = v.Replace('\\', '/'); v = v.Substring(v.LastIndexOf('/') + 1).Trim();
                    if (v.Length > 2 && Regex.IsMatch(v, @"\.[A-Za-z0-9]{1,8}$")) return v;
                }
            }
            catch { }
            try
            {
                string[] segs = uri.Segments;
                string last = Uri.UnescapeDataString(segs[segs.Length - 1].Trim('/'));
                if (last.Length > 0) return last;
            }
            catch { }
            return "download";
        }

        internal string UniquePath(string dir, string name)
        {
            string bas = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name);
            string p = Path.Combine(dir, name); int i = 1;
            if (Ui.Settings.GetString("DupMode") == "overwrite" && !_items.Exists(delegate(RockDownload d) { return d.FullPath == p; })) return p;
            while (File.Exists(p) || _items.Exists(delegate(RockDownload d) { return d.FullPath == p; }))
            { p = Path.Combine(dir, bas + " (" + i + ")" + ext); i++; }
            return p;
        }

        public void Init(Window w, Logger log)
        {
            Instance = this;
            _lv = Ui.Find<ListView>("lvDownloads");
            _tree = Ui.Find<TreeView>("tvDlCats"); _catTitle = Ui.Find<TextBlock>("txtDlCat"); _max = Ui.Find<ComboBox>("cmbDlMax");
            _limit = Ui.Find<ComboBox>("cmbDlLimit"); _conn = Ui.Find<ComboBox>("cmbDlConn");
            _search = Ui.Find<TextBox>("txtDlSearch"); _dir = Ui.Find<TextBox>("txtDlDir");
            _count = Ui.Find<TextBlock>("txtDlCount"); _info = Ui.Find<TextBlock>("txtDlInfo");
            _savePanel = Ui.Find<Border>("brDlSave");

            BuildTree();
            for (int i = 1; i <= 8; i++) _max.Items.Add(i);
            int mx = 3; try { mx = Convert.ToInt32(Ui.Settings.GetString("DlMax")); } catch { }
            _max.SelectedItem = (mx >= 1 && mx <= 8) ? mx : 3;
            foreach (string l in LimitNames) _limit.Items.Add(l);
            int li = Array.IndexOf(LimitNames, Ui.Settings.GetString("DlLimit")); _limit.SelectedIndex = li >= 0 ? li : 0;
            foreach (int c in new int[] { 1, 2, 4, 6, 8, 12, 16, 24, 32 }) _conn.Items.Add(c);
            int cv = 16; try { cv = Convert.ToInt32(Ui.Settings.GetString("DlConn")); } catch { }
            _conn.SelectedItem = _conn.Items.Contains(cv) ? cv : 16;
            _dir.Text = DlDir; ApplyViewState();
            _info.Text = "Add a link to start downloading.";

            _lv.SelectionChanged += delegate { UpdateCount(); };
            _debounce = new DispatcherTimer(); _debounce.Interval = TimeSpan.FromMilliseconds(250);
            _debounce.Tick += delegate { _debounce.Stop(); ApplyFilter(); };
            _search.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };
            _max.SelectionChanged += delegate { if (_max.SelectedItem != null) { Ui.Settings.Set("DlMax", (int)_max.SelectedItem); Ui.Settings.SaveSoon(); } Tick(); };
            _limit.SelectionChanged += delegate
            {
                if (_limit.SelectedItem != null) { Ui.Settings.Set("DlLimit", (string)_limit.SelectedItem); Ui.Settings.SaveSoon(); }
                long lim = LimitBytes; foreach (IDlJob e in _jobs.Values) try { e.Limit = lim; } catch { }
            };
            _conn.SelectionChanged += delegate { if (_conn.SelectedItem != null) { Ui.Settings.Set("DlConn", (int)_conn.SelectedItem); Ui.Settings.SaveSoon(); } };
            _dir.LostFocus += delegate { Ui.Settings.Set("DlDir", _dir.Text.Trim()); Ui.Settings.SaveSoon(); };

            Ui.Find<Button>("btnDlBrowse").Click += delegate
            {
                try
                {
                    using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fd.SelectedPath = DlDir;
                        if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK) { _dir.Text = fd.SelectedPath; Ui.Settings.Set("DlDir", fd.SelectedPath); Ui.Settings.SaveSoon(); }
                    }
                }
                catch (Exception ex) { Ui.Log.Warn("Could not open the folder picker: " + ex.Message, "Downloads"); }
            };
            Ui.Find<Button>("btnDlAdd").Click += delegate { OpenInfoFromClipboard(); };
            Ui.Find<Button>("btnDlAddMany").Click += delegate { AddDialog(); };
            Ui.Find<Button>("btnDlGrab").Click += delegate { DlWin.OpenGrabber(); };
            Ui.Find<Button>("btnDlRlist").Click += delegate { OpenRlistPicker(); };
            Ui.Find<Button>("btnDlExt").Click += delegate { ExtGuide.Show(true); };
            Ui.Find<Button>("btnDlPause").Click += delegate { Pause(); };
            Ui.Find<Button>("btnDlResume").Click += delegate { Resume(); };
            Ui.Find<Button>("btnDlCancel").Click += delegate { Cancel(); };
            Ui.Find<Button>("btnDlClear").Click += delegate
            {
                int n = _items.RemoveAll(delegate(RockDownload d) { return d.Status == "Completed" || d.Status == "Failed" || d.Status == "Cancelled"; });
                ApplyFilter(); Ui.Log.Info("Cleared " + n + " finished download(s).", "Downloads"); SaveSoon();
            };
            Ui.Find<Button>("btnDlStopAll").Click += delegate { foreach (RockDownload it in new List<RockDownload>(_items)) PauseItem(it); SaveSoon(); };
            Ui.Find<Button>("btnDlStopQ").Click += delegate { _queueStopped = true; _info.Text = Lang.L("Queue stopped - waiting downloads will not start."); };
            Ui.Find<Button>("btnDlStartQ").Click += delegate
            {
                RSound.Play("QueueStart"); _queueStopped = false; _info.Text = "";
                foreach (RockDownload it in new List<RockDownload>(_items))
                    if (!_jobs.ContainsKey(it.Id) && (it.Status == "Paused" || it.Status == "Failed")) { it.Retries = 0; SetStatus(it, "Queued"); }
                Tick(); SaveSoon();
            };
            _lv.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs ke)
            {
                if (ke.Key == System.Windows.Input.Key.Delete) { Cancel(); ke.Handled = true; }
                else if (ke.Key == System.Windows.Input.Key.C && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    RockDownload cd = _lv.SelectedItem as RockDownload;
                    if (cd != null) { try { Clipboard.SetText(cd.Url); Ui.Log.Info("Link copied: " + cd.Url, "Downloads"); } catch (Exception) { } }
                    ke.Handled = true;
                }
            };
            Ui.Find<Button>("btnDlOpen").Click += delegate { OpenFolder(); };
            Ui.Find<Button>("btnDlSettings").Click += delegate
            { _savePanel.Visibility = _savePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; };
            _lv.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate(object s, RoutedEventArgs e)
            {
                Button b = e.OriginalSource as Button;
                if (b != null && b.Name == "dlCopy")
                {
                    try { Clipboard.SetText(Convert.ToString(b.Tag)); Ui.Log.Info("Link copied: " + b.Tag, "Downloads"); b.Content = "Copied"; } catch { }
                    DispatcherTimer t = new DispatcherTimer(); t.Interval = TimeSpan.FromMilliseconds(1200);
                    t.Tick += delegate { t.Stop(); try { b.Content = "Copy"; } catch { } };
                    t.Start();
                    return;
                }
                UpdateCount();
            }));
            _lv.MouseDoubleClick += delegate
            {
                RockDownload it = _lv.SelectedItem as RockDownload;
                if (it != null && it.Status == "Completed" && File.Exists(it.FullPath))
                    try { Tool.Start(it.FullPath, ""); } catch (Exception ex) { Ui.Log.Warn("Could not open the file: " + ex.Message, "Downloads"); }
            };

            _saveTimer = new DispatcherTimer(); _saveTimer.Interval = TimeSpan.FromMilliseconds(700);
            _saveTimer.Tick += delegate { _saveTimer.Stop(); SaveState(); };
            _timer = new DispatcherTimer(); _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += delegate { try { Tick(); } catch { } };
            _timer.Start();
            _fast = new DispatcherTimer(); _fast.Interval = TimeSpan.FromMilliseconds(40);
            _fast.Tick += delegate { try { Smooth(); } catch { } };
            _fast.Start();
            _clipTimer = new DispatcherTimer(); _clipTimer.Interval = TimeSpan.FromMilliseconds(1200);
            _clipTimer.Tick += delegate { try { WatchClipboard(); } catch { } };
            _clipTimer.Start();
            try { if (Clipboard.ContainsText()) _lastClip = (Clipboard.GetText() ?? "").Trim(); } catch (Exception) { }
            LoadState();
            ApplyFilter();
            AppLife.QuitEvent += delegate
            {
                try
                {
                    foreach (IDlJob e in new List<IDlJob>(_jobs.Values)) try { e.Stop("pause"); } catch { }
                    DateTime end = DateTime.Now.AddMilliseconds(1500);
                    while (DateTime.Now < end)
                    {
                        bool alive = false;
                        foreach (IDlJob e in _jobs.Values) if (!e.Finished) alive = true;
                        if (!alive) break;
                        System.Threading.Thread.Sleep(30);
                    }
                    SaveState();
                }
                catch { }
            };
        }

        // the Add button: the link in the clipboard (if any) is put in the Download File Info window
        private void OpenInfoFromClipboard()
        {
            string u = "";
            try
            {
                string clip = (Clipboard.GetText() ?? "").Trim();
                if (Regex.IsMatch(clip, @"^(https?://|magnet:\?)\S+$")) u = clip;
            }
            catch { }
            DlWin.ShowInfo(u, "", "", "", false);
        }

        private void OpenRlistPicker()
        {
            try
            {
                Microsoft.Win32.OpenFileDialog fd = new Microsoft.Win32.OpenFileDialog();
                fd.Title = "Open an Rlist"; fd.Filter = "Rlist files (*.Rlist)|*.Rlist|All files (*.*)|*.*";
                string last = ""; List<string> k = Rlist.Known(); if (k.Count > 0) last = Path.GetDirectoryName(k[0]);
                if (!string.IsNullOrEmpty(last)) fd.InitialDirectory = last;
                if (fd.ShowDialog() == true) DlWin.OpenRlist(fd.FileName);
            }
            catch (Exception ex) { Ui.Log.Warn("Could not open the file: " + ex.Message, "Downloads"); }
        }

        // categories tree on the left (like IDM): All Downloads [Compressed, Documents, Music, Programs, Video], Unfinished, Finished, Queues
        private static TreeViewItem CatItem(string glyph, string color, string title, string key)
        {
            TreeViewItem ti = new TreeViewItem(); ti.Tag = key;
            StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal;
            TextBlock g = new TextBlock(); g.Text = glyph; g.FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"); g.FontSize = 15;
            g.Foreground = Ui.Brush(color); g.Width = 24; g.VerticalAlignment = VerticalAlignment.Center;
            TextBlock t = new TextBlock(); t.Text = Lang.L(title); t.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(g); sp.Children.Add(t); ti.Header = sp;
            return ti;
        }

        private void BuildTree()
        {
            TreeViewItem all = CatItem("\uE896", "#4FD1FF", "All Downloads", "all"); all.IsExpanded = true;
            all.Items.Add(CatItem(DlGlyph.Glyph("a.zip", ""), "#E0A76B", "Compressed", "cat:Compressed"));
            all.Items.Add(CatItem(DlGlyph.Glyph("a.pdf", ""), "#4DA3C4", "Documents", "cat:Documents"));
            all.Items.Add(CatItem(DlGlyph.Glyph("a.mp3", ""), "#D88C9A", "Music", "cat:Music"));
            all.Items.Add(CatItem(DlGlyph.Glyph("a.exe", ""), "#6BC4B0", "Programs", "cat:Programs"));
            all.Items.Add(CatItem(DlGlyph.Glyph("a.mp4", ""), "#7C9CD8", "Video", "cat:Video"));
            _tree.Items.Add(all);
            _tree.Items.Add(CatItem("\uE768", "#FFB454", "Unfinished", "unfinished"));
            _tree.Items.Add(CatItem("\uE73E", "#3DDC84", "Finished", "finished"));
            _tree.Items.Add(CatItem("\uE71D", "#9AA0B8", "Queues", "queues"));
            all.IsSelected = true;
            _tree.SelectedItemChanged += delegate
            {
                TreeViewItem ti = _tree.SelectedItem as TreeViewItem; if (ti == null) return;
                _cat = Convert.ToString(ti.Tag);
                StackPanel sp = ti.Header as StackPanel;
                if (sp != null && _catTitle != null) _catTitle.Text = ((TextBlock)sp.Children[1]).Text;
                ApplyFilter();
            };
        }

        private void SaveSoon() { _saveTimer.Stop(); _saveTimer.Start(); }

        // ---------------- list ----------------
        private void UpdateCount()
        {
            List<RockDownload> src = _lv.ItemsSource as List<RockDownload>;
            int n = _lv.SelectedItems.Count;
            _count.Text = "Selected: " + n + " of " + (src == null ? 0 : src.Count);
        }

        private void ApplyFilter()
        {
            string cat = _cat; string q = (_search.Text ?? "").Trim();
            List<RockDownload> list = new List<RockDownload>();
            foreach (RockDownload it in _items)
            {
                string st = it.Status;
                if (cat == "unfinished" && st == "Completed") continue;
                if (cat == "finished" && st != "Completed") continue;
                if (cat == "queues" && st != "Queued" && st != "Paused" && st != "Downloading") continue;
                if (cat.StartsWith("cat:") && DlGlyph.Category(it.FileName, it.Url) != cat.Substring(4)) continue;
                if (q.Length > 0 && it.FileName.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && it.Url.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(it);
            }
            SortList(list);
            _lv.ItemsSource = list; UpdateCount();
        }

        private List<RockDownload> Targets(bool allIfNone)
        {
            List<RockDownload> sel = new List<RockDownload>();
            foreach (object o in _lv.SelectedItems) { RockDownload sd = o as RockDownload; if (sd != null) sel.Add(sd); }
            if (sel.Count == 0 && allIfNone) { List<RockDownload> src = _lv.ItemsSource as List<RockDownload>; if (src != null) sel.AddRange(src); }
            return sel;
        }

        private static void UpdateRow(RockDownload it, double speed)
        {
            // percentage, downloaded size and speed are drawn by Smooth() (40 ms) so they count up smoothly; this only does the slow parts
            double sp = it.SmSpeed > 1 ? it.SmSpeed : speed;
            if (it.Total > 0)
            {
                it.SizeText = FormatSize(it.Total);
                it.EtaText = sp > 1 ? FormatEta((it.Total - it.SmDone) / sp) : "-";
            }
            else { it.PctText = ""; it.SizeText = FormatSize(it.Done); it.EtaText = "-"; }
            if (!it.SmInit) { it.SmDone = it.Done; it.SmSpeed = speed; it.SmInit = true; }
        }

        // glides the shown numbers towards the real ones, so the counters pass through every value instead of jumping
        private void Smooth()
        {
            DateTime now = DateTime.UtcNow;
            double dt = Math.Min(0.25, (now - _smT).TotalSeconds); _smT = now;
            if (_jobs.Count == 0) return;
            double a = 1.0 - Math.Exp(-dt * 9.0), b = 1.0 - Math.Exp(-dt * 3.5);
            foreach (RockDownload it in _items)
            {
                IDlJob eng;
                if (it.Status != "Downloading" || !_jobs.TryGetValue(it.Id, out eng) || eng.Finished) continue;
                double td = eng.Done, ts = eng.Speed;
                if (!it.SmInit || td < it.SmDone) { it.SmDone = td; it.SmSpeed = ts; it.SmInit = true; }
                else
                {
                    it.SmDone += (td - it.SmDone) * a; if (td - it.SmDone < 1.0) it.SmDone = td;
                    it.SmSpeed += (ts - it.SmSpeed) * b; if (Math.Abs(ts - it.SmSpeed) < 1.0) it.SmSpeed = ts;
                }
                if (it.Total > 0)
                {
                    double p = Math.Min(100.0, it.SmDone * 100.0 / it.Total);
                    it.Progress = p; it.PctText = p.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                    it.ProgText = FormatSize(it.SmDone) + " / " + FormatSize(it.Total);
                }
                else it.ProgText = FormatSize(it.SmDone);
                it.SpeedText = it.SmSpeed > 1 ? FormatSize(it.SmSpeed) + "/s" : "-";
            }
        }

        internal static readonly string[] AfterActions = new string[] { "Do nothing", "Close R-Downloader", "Sleep", "Shut down the PC", "Hibernate" };
        internal const string DefaultClipExt = "zip,rar,7z,tar,gz,iso,exe,msi,msix,apk,dmg,pdf,doc,docx,xls,xlsx,ppt,pptx,mp3,flac,wav,m4a,mp4,mkv,avi,mov,webm,torrent";
        private bool _wasBusy, _anyDone;
        private string _lastClip = "";
        private DispatcherTimer _clipTimer;

        // what to do when every download has finished (Settings > Downloads)
        private void AllFinished()
        {
            int a = 0; int.TryParse(Ui.Settings.GetString("AfterAll"), out a);
            if (a == 1) { Ui.Log.Info("All downloads finished - closing R-Downloader.", "Downloads"); AppLife.Exit(); }
            else if (a == 2) { Ui.Log.Info("All downloads finished - going to sleep.", "Downloads"); try { Tool.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"); } catch (Exception) { } }
            else if (a == 4) { Ui.Log.Info("All downloads finished - hibernating.", "Downloads"); try { Tool.Start("shutdown.exe", "/h"); } catch (Exception) { } }
            else if (a == 3)
            {
                Ui.Log.Info("All downloads finished - shutting down the PC in 60 seconds.", "Downloads");
                try { Tool.Start("shutdown.exe", "/s /t 60 /c \"R-Downloader: all downloads finished\""); } catch (Exception) { return; }
                if (Dlg.Ask("All downloads are finished.\n\nThe PC will shut down in 60 seconds.", "Cancel shutdown", "OK")) { try { Tool.Start("shutdown.exe", "/a"); } catch (Exception) { } }
            }
        }

        // a link copied anywhere (a file link, not a web page) is offered to the Download File Info window
        private static string ClipExtPattern()
        {
            string s = (Ui.Settings.GetString("ClipExt") ?? "").Trim(); if (s.Length == 0) s = DefaultClipExt;
            List<string> l = new List<string>();
            foreach (string x in s.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)) { string e = x.Trim().TrimStart('.'); if (e.Length > 0) l.Add(Regex.Escape(e)); }
            return l.Count == 0 ? "zip" : string.Join("|", l.ToArray());
        }

        private void WatchClipboard()
        {
            if (!Ui.Settings.GetBool("ClipWatch", false)) return;
            string clip = "";
            try { if (!Clipboard.ContainsText()) return; clip = (Clipboard.GetText() ?? "").Trim(); } catch (Exception) { return; }
            if (clip == _lastClip) return;
            _lastClip = clip;
            if (!Regex.IsMatch(clip, @"^(https?://|magnet:\?)\S+$")) return;
            if (!Aria.IsTorrentLink(clip) && !Regex.IsMatch(clip, @"\.(" + ClipExtPattern() + @")(\?|#|$)", RegexOptions.IgnoreCase)) return;
            DlWin.ShowInfo(clip, "", "", "", false);
        }

        internal static void PlayDoneSound() { RSound.Play("Complete"); }

        // ---------------- engine ----------------
        private void StartJob(RockDownload it)
        {
            IDlJob eng;
            if (!_engines.TryGetValue(it.Id, out eng))
            {
                if (Aria.IsTorrentLink(it.Url)) eng = new AriaJob(it.Url, it.Dir, LimitBytes);
                else eng = new RockDlEngine(it.Url, it.FullPath, it.Dir, it.Verify, it.NameFromServer, Conns, LimitBytes);
                if (!string.IsNullOrEmpty(it.Seg)) eng.SetResume(it.Seg);
                _engines[it.Id] = eng;
            }
            else eng.Limit = LimitBytes;
            eng.Begin();
            _jobs[it.Id] = eng;
            SetStatus(it, "Downloading");
            if (it.ShowWin) { it.ShowWin = false; try { DlWin.OpenProgress(it); } catch (Exception ex) { Ui.Log.Warn("Could not open the download window: " + ex.Message, "Downloads"); } }
        }

        private void StartQueued()
        {
            int max = 3; try { max = (int)_max.SelectedItem; } catch { }
            int running = _jobs.Count;
            if (_queueStopped) return;
            foreach (RockDownload it in new List<RockDownload>(_items))
            {
                if (running >= max) break;
                if (it.Status == "Queued" && !_jobs.ContainsKey(it.Id))
                {
                    try { StartJob(it); running++; }
                    catch (Exception ex) { SetStatus(it, "Failed"); Ui.Log.Error("Could not start " + it.FileName + ": " + ex.Message, "Downloads"); }
                }
            }
        }

        private void CompleteJob(RockDownload it, IDlJob eng)
        {
            _jobs.Remove(it.Id);
            it.SmInit = false;
            it.SpeedText = "-"; it.EtaText = "-";
            switch (eng.State)
            {
                case "done":
                    {
                        _engines.Remove(it.Id);
                        string final = eng.Final; if (string.IsNullOrEmpty(final)) final = it.FullPath;
                        it.FullPath = final; it.FileName = Path.GetFileName(final);
                        try { it.SizeText = FormatSize(new FileInfo(final).Length); } catch { }
                        it.Progress = 100.0; it.PctText = "100%"; it.SmDone = it.Total;
                        if (it.Total > 0) it.ProgText = FormatSize(it.Total) + " / " + FormatSize(it.Total);
                        SetStatus(it, "Completed"); SaveState();
                        Ui.Log.Info("Download finished: " + it.FileName, "Downloads");
                        if (!string.IsNullOrEmpty(eng.Hash)) Ui.Log.Info("SHA-256 of " + it.FileName + ": " + eng.Hash, "Downloads");
                        if (it.RunAfter) try { Tool.Start(final, ""); } catch (Exception ex) { Ui.Log.Warn("Could not open " + it.FileName + ": " + ex.Message, "Downloads"); }
                        _anyDone = true;
                        DlOpts.Scan(final);
                        PlayDoneSound();
                        try { DlWin.Finished(it); } catch (Exception ex) { Ui.Log.Warn("Could not show the completion window: " + ex.Message, "Downloads"); }
                        break;
                    }
                case "pause": SetStatus(it, "Paused"); SaveState(); break;
                case "cancel":
                    _engines.Remove(it.Id);
                    try { File.Delete(it.FullPath + ".part"); } catch { }
                    it.Done = 0; it.Progress = 0; it.PctText = "0%"; it.SizeText = "-"; SetStatus(it, "Cancelled");
                    break;
                default:
                    {
                        string err = eng.Err ?? "";
                        if (it.AutoResume && it.Retries < 10 && !Regex.IsMatch(err, @"\((401|404|410)\)"))
                        {
                            it.Retries++; SetStatus(it, "Queued");
                            Ui.Log.Warn("Connection problem with " + it.FileName + " - retrying (" + it.Retries + " of 10)...", "Downloads");
                        }
                        else { SetStatus(it, "Failed"); RSound.Play("Failed"); Ui.Log.Error("Download failed: " + it.FileName + " - " + err, "Downloads"); }
                        break;
                    }
            }
        }

        private void Tick()
        {
            if (_ticking) return;
            _ticking = true;
            try
            {
                RunScheduler();
                double speed = 0; int act = 0, que = 0, pau = 0;
                foreach (RockDownload it in new List<RockDownload>(_items))
                {
                    IDlJob eng;
                    if (_jobs.TryGetValue(it.Id, out eng))
                    {
                        if (eng.Finished) CompleteJob(it, eng);
                        else
                        {
                            it.Done = eng.Done; it.Total = eng.Total;
                            if (it.NameFromServer) { string sn = eng.SrvName; if (!string.IsNullOrEmpty(sn) && it.FileName != sn) it.FileName = sn; }
                            double sp = eng.Speed; speed += sp; act++; UpdateRow(it, sp);
                        }
                    }
                    else if (it.Status == "Queued") que++;
                    else if (it.Status == "Paused") pau++;
                }
                StartQueued();
                if (act > 0) _wasBusy = true;
                else if (_wasBusy && que == 0 && pau == 0 && _jobs.Count == 0) { _wasBusy = false; if (_anyDone) { _anyDone = false; RSound.Play("QueueEnd"); AllFinished(); } }
                _saveTick++;
                if (act > 0 && _saveTick % 10 == 0) SaveState();
                _info.Text = "Total speed: " + FormatSize(speed) + "/s   |   " + act + " active, " + que + " queued, " + pau + " paused";
            }
            finally { _ticking = false; }
        }

        // builds a download entry (not yet in the list). null = the link is not valid.
        internal RockDownload CreateItem(string u, string dir, string name, bool autoResume, bool runAfter, bool verify)
        {
            u = (u ?? "").Trim();
            bool tor = Aria.IsTorrentLink(u);
            Uri uri = null;
            if (!tor && (!Uri.TryCreate(u, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https")))
            { Ui.Log.Warn("Skipped (not an http/https/magnet link): " + u, "Downloads"); return null; }
            bool custom = !string.IsNullOrEmpty(name) && name.Trim().Length > 0;
            string nm = custom ? SafeName(name) : (tor ? SafeName(Aria.NameOf(u)) : SafeName(NameFromUrl(uri)));
            RockDownload it = new RockDownload();
            it.Id = Guid.NewGuid().ToString("N"); it.Url = u; it.Dir = dir;
            it.FullPath = UniquePath(dir, nm); it.FileName = Path.GetFileName(it.FullPath);
            it.NameFromServer = !tor && !custom && Path.GetExtension(nm) == "";
            it.AutoResume = autoResume; it.RunAfter = runAfter; it.Verify = verify; it.Added = DateTime.Now.ToString("o");
            SetStatus(it, "Paused");
            return it;
        }

        // puts a built entry in the list; startNow = begin right away, otherwise it waits (Paused)
        internal void Adopt(RockDownload it, bool startNow, bool showWin)
        {
            try { Directory.CreateDirectory(it.Dir); } catch (Exception ex) { Ui.Log.Error("Could not create the folder: " + ex.Message, "Downloads"); return; }
            try { File.Delete(it.FullPath + ".part"); } catch { }
            it.ShowWin = showWin && startNow;
            SetStatus(it, startNow ? "Queued" : "Paused");
            _items.Add(it);
            Ui.Log.Info("Download added: " + it.FileName, "Downloads");
            ApplyFilter(); Tick(); SaveState();
        }

        internal List<RockDownload> AddItems(string[] urls, string dir, string name, bool startNow, bool autoResume, bool runAfter, bool verify)
        {
            List<RockDownload> res = new List<RockDownload>();
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) { Ui.Log.Error("Could not create the folder: " + ex.Message, "Downloads"); return res; }
            bool single = urls.Length == 1;
            foreach (string u in urls)
            {
                RockDownload it = CreateItem(u, dir, single ? name : "", autoResume, runAfter, verify);
                if (it == null) continue;
                try { File.Delete(it.FullPath + ".part"); } catch { }
                SetStatus(it, startNow ? "Queued" : "Paused");
                _items.Add(it); res.Add(it);
            }
            if (res.Count > 0) { Ui.Log.Info(res.Count + " download(s) added.", "Downloads"); ApplyFilter(); Tick(); SaveState(); }
            return res;
        }

        // ---------------- used by the download windows ----------------
        internal void Rename(RockDownload it, string name)
        {
            string nm = SafeName(name);
            if (nm.Length == 0) return;
            it.FullPath = UniquePath(it.Dir, nm); it.FileName = Path.GetFileName(it.FullPath); it.NameFromServer = false;
        }

        internal IDlJob JobOf(RockDownload it) { IDlJob j; return _jobs.TryGetValue(it.Id, out j) ? j : null; }
        internal int ConnCount { get { return Conns; } }
        internal long CurrentLimit { get { return LimitBytes; } }
        internal bool Busy
        {
            get
            {
                foreach (RockDownload d in _items) if (d.Status == "Queued" || d.Status == "Downloading") return true;
                return false;
            }
        }

        internal void PauseItem(RockDownload it)
        {
            IDlJob eng;
            if (_jobs.TryGetValue(it.Id, out eng)) { if (it.Status == "Downloading") { eng.Stop("pause"); it.StatusText = Lang.L("Pausing..."); } }
            else if (it.Status == "Queued") SetStatus(it, "Paused");
            SaveSoon();
        }

        internal void ResumeItem(RockDownload it)
        {
            if (!_jobs.ContainsKey(it.Id) && (it.Status == "Paused" || it.Status == "Failed" || it.Status == "Cancelled")) { it.Retries = 0; SetStatus(it, "Queued"); }
            Tick(); SaveSoon();
        }

        internal void CancelItem(RockDownload it)
        {
            IDlJob eng;
            if (_jobs.TryGetValue(it.Id, out eng)) { eng.Stop("cancel"); it.StatusText = Lang.L("Cancelling..."); }
            else if (it.Status == "Queued" || it.Status == "Paused" || it.Status == "Failed")
            {
                try { File.Delete(it.FullPath + ".part"); } catch { }
                it.Done = 0; it.Progress = 0; it.PctText = "0%"; it.SizeText = "-"; SetStatus(it, "Cancelled");
            }
            SaveSoon();
        }

        // ---------------- saved list ----------------
        private static string StatePath
        {
            get
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDownloader");
                Directory.CreateDirectory(root);
                return Path.Combine(root, "downloads.json");
            }
        }

        private void SaveState()
        {
            try
            {
                List<Dictionary<string, object>> arr = new List<Dictionary<string, object>>();
                foreach (RockDownload it in _items)
                {
                    string seg = it.Seg; IDlJob eng;
                    if (_engines.TryGetValue(it.Id, out eng)) { try { string g = eng.GetSegState(); if (!string.IsNullOrEmpty(g)) seg = g; } catch { } }
                    if (it.Status == "Completed") seg = "";
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["Id"] = it.Id; d["Url"] = it.Url; d["Dir"] = it.Dir; d["FullPath"] = it.FullPath; d["FileName"] = it.FileName;
                    d["Total"] = it.Total; d["Done"] = it.Done; d["Status"] = it.Status; d["SizeText"] = it.SizeText;
                    d["AutoResume"] = it.AutoResume; d["RunAfter"] = it.RunAfter; d["Verify"] = it.Verify;
                    d["NameFromServer"] = it.NameFromServer; d["Seg"] = seg ?? ""; d["Added"] = it.Added ?? ""; d["LastTry"] = it.LastTry ?? "";
                    arr.Add(d);
                }
                string path = StatePath, tmp = path + ".tmp";
                JavaScriptSerializer ser = new JavaScriptSerializer(); ser.MaxJsonLength = int.MaxValue;
                File.WriteAllText(tmp, ser.Serialize(arr), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch { }
        }

        private static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : ""; }
        private static long L(Dictionary<string, object> d, string k) { try { return Convert.ToInt64(S(d, k)); } catch { return 0; } }
        private static bool B(Dictionary<string, object> d, string k) { bool b; return bool.TryParse(S(d, k), out b) && b; }

        private void LoadState()
        {
            try
            {
                string path = StatePath; if (!File.Exists(path)) return;
                JavaScriptSerializer ser = new JavaScriptSerializer(); ser.MaxJsonLength = int.MaxValue;
                System.Collections.ArrayList rows = Net.Arr(ser.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)));
                if (rows == null) return;
                foreach (object o in rows)
                {
                    Dictionary<string, object> r = o as Dictionary<string, object>;
                    if (r == null || S(r, "Url").Length == 0) continue;
                    RockDownload it = new RockDownload();
                    it.Id = S(r, "Id"); if (it.Id.Length == 0) it.Id = Guid.NewGuid().ToString("N");
                    it.Url = S(r, "Url"); it.Dir = S(r, "Dir"); it.FullPath = S(r, "FullPath"); it.FileName = S(r, "FileName");
                    it.Total = L(r, "Total"); it.Done = L(r, "Done");
                    it.AutoResume = B(r, "AutoResume"); it.RunAfter = false; it.Verify = B(r, "Verify");
                    it.NameFromServer = B(r, "NameFromServer"); it.Seg = S(r, "Seg"); it.Added = S(r, "Added"); it.LastTry = S(r, "LastTry");
                    it.SizeText = S(r, "SizeText"); if (it.SizeText.Length == 0) it.SizeText = "-";
                    string st = S(r, "Status");
                    if (st == "Downloading" || st == "Queued") st = "Paused";
                    if (st == "Completed") { it.Progress = 100.0; it.PctText = "100%"; }
                    else
                    {
                        double pct = it.Total > 0 ? Math.Min(100.0, it.Done * 100.0 / it.Total) : 0;
                        it.Progress = pct; it.PctText = pct.ToString("0") + "%";
                        if (it.Total > 0) it.SizeText = FormatSize(it.Total);
                        if ((st == "Paused" || st == "Failed") && !File.Exists(it.FullPath + ".part")) { it.Done = 0; it.Progress = 0; it.PctText = "0%"; it.Seg = ""; }
                    }
                    string keepTry = it.LastTry;
                    SetStatus(it, st);
                    if (keepTry.Length > 0) it.LastTry = keepTry;
                    _items.Add(it);
                }
                if (rows.Count > 0) Ui.Log.Info(rows.Count + " download(s) restored from the last session.", "Downloads");
            }
            catch (Exception ex) { Ui.Log.Warn("Could not read the saved download list: " + ex.Message, "Downloads"); }
        }

        // ---------------- actions ----------------
        private void Pause()
        {
            foreach (RockDownload it in Targets(false)) PauseItem(it);
            UpdateCount();
        }

        private void Resume()
        {
            foreach (RockDownload it in Targets(false)) { if (!_jobs.ContainsKey(it.Id) && (it.Status == "Paused" || it.Status == "Failed" || it.Status == "Cancelled")) { it.Retries = 0; SetStatus(it, "Queued"); } }
            UpdateCount(); Tick(); SaveSoon();
        }

        private void Cancel()
        {
            List<RockDownload> t = Targets(false);
            if (t.Count == 0) { Ui.Log.Warn("Select the downloads you want to delete first.", "Downloads"); return; }
            foreach (RockDownload it in t) CancelItem(it);
            UpdateCount();
        }

        private void OpenFolder()
        {
            RockDownload it = _lv.SelectedItem as RockDownload;
            try
            {
                if (it != null && File.Exists(it.FullPath)) { Tool.Start("explorer.exe", "/select,\"" + it.FullPath + "\""); return; }
                if (it != null && it.Status == "Completed") { Ui.Warn("File not found\n\nThis file is no longer on your PC. It may have been moved, renamed or deleted.\n\n" + it.FullPath); return; }
                string d = DlDir; if (it != null && Directory.Exists(it.Dir)) d = it.Dir;
                Directory.CreateDirectory(d); Tool.Start("explorer.exe", "\"" + d + "\"");
            }
            catch (Exception ex) { Ui.Log.Warn("Could not open the folder: " + ex.Message, "Downloads"); }
        }

        private const string AddXaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""Add download"" Width=""500"" SizeToContent=""Height"" ResizeMode=""NoResize"" ShowInTaskbar=""False""
        WindowStartupLocation=""CenterOwner"" Background=""#1F2329"" FontFamily=""Segoe UI"" FontSize=""13"">
  <Window.Resources>
    <Style x:Key=""DlgBtn"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""#F0F4FF""/>
      <Setter Property=""Background"" Value=""#1E3547""/>
      <Setter Property=""BorderBrush"" Value=""#35506A""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""1"" CornerRadius=""3"" Padding=""22,7"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <StackPanel Margin=""18"">
    <TextBlock Text=""Add download"" FontSize=""15"" FontWeight=""Bold"" Foreground=""White"" Margin=""0,0,0,12""/>
    <TextBlock Text=""Links (one per line)"" Foreground=""#9AA0B8"" Margin=""0,0,0,4""/>
    <TextBox x:Name=""dlUrls"" Height=""86"" AcceptsReturn=""True"" TextWrapping=""NoWrap"" VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Auto""
             Background=""#14171B"" Foreground=""White"" CaretBrush=""White"" BorderBrush=""#35506A"" FontFamily=""Consolas"" Padding=""4""/>
    <TextBlock Text=""Save to:"" Foreground=""#9AA0B8"" Margin=""0,12,0,4""/>
    <DockPanel>
      <Button x:Name=""dlBrowse"" Content=""Browse..."" DockPanel.Dock=""Right"" Style=""{StaticResource DlgBtn}"" Margin=""6,0,0,0""/>
      <TextBox x:Name=""dlDir"" Height=""30"" VerticalContentAlignment=""Center"" Background=""#14171B"" Foreground=""White"" CaretBrush=""White"" BorderBrush=""#35506A"" FontFamily=""Consolas""/>
    </DockPanel>
    <TextBlock Text=""File name (optional)"" Foreground=""#9AA0B8"" Margin=""0,12,0,4""/>
    <TextBox x:Name=""dlName"" Height=""30"" VerticalContentAlignment=""Center"" Background=""#14171B"" Foreground=""White"" CaretBrush=""White"" BorderBrush=""#35506A""
             ToolTip=""Only used when you add a single link""/>
    <StackPanel Margin=""0,14,0,0"">
      <CheckBox x:Name=""dlStart"" Content=""Start downloading right away"" Foreground=""#E6E8F2"" IsChecked=""True"" Margin=""0,3""/>
      <CheckBox x:Name=""dlResume"" Content=""Resume if the connection drops"" Foreground=""#E6E8F2"" IsChecked=""True"" Margin=""0,3""/>
      <CheckBox x:Name=""dlRun"" Content=""Run the file when finished"" Foreground=""#E6E8F2"" Margin=""0,3""/>
      <CheckBox x:Name=""dlVerify"" Content=""Calculate SHA-256 hash"" Foreground=""#E6E8F2"" Margin=""0,3""/>
    </StackPanel>
    <TextBlock x:Name=""dlErr"" Foreground=""#FF6B6B"" TextWrapping=""Wrap"" Visibility=""Collapsed"" Margin=""0,10,0,0""/>
    <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Right"" Margin=""0,16,0,0"">
      <Button x:Name=""dlCancel"" Content=""Cancel"" Style=""{StaticResource DlgBtn}"" IsCancel=""True""/>
      <Button x:Name=""dlOk"" Content=""Add to queue"" Style=""{StaticResource DlgBtn}"" Background=""#2F6FB0"" Foreground=""#FFFFFF"" IsDefault=""True"" Margin=""8,0,0,0""/>
    </StackPanel>
  </StackPanel>
</Window>";

        private void AddDialog()
        {
            Window w = (Window)XamlReader.Parse(Lang.ConvertXaml(Theme.ConvertXaml(AddXaml)));
            w.Owner = Ui.Win;
            TextBox urls = (TextBox)w.FindName("dlUrls"), dir = (TextBox)w.FindName("dlDir"), name = (TextBox)w.FindName("dlName");
            TextBlock err = (TextBlock)w.FindName("dlErr");
            dir.Text = DlDir;
            try
            {
                string clip = Clipboard.GetText();
                if (!string.IsNullOrEmpty(clip))
                {
                    List<string> found = new List<string>();
                    foreach (string l in clip.Split('\n')) { string s = l.Trim(); if (Regex.IsMatch(s, @"^(https?://|magnet:\?)\S+$")) found.Add(s); }
                    if (found.Count > 0) urls.Text = string.Join("\r\n", found.ToArray());
                }
            }
            catch { }
            ((Button)w.FindName("dlBrowse")).Click += delegate
            {
                try
                {
                    using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
                    { fd.SelectedPath = dir.Text; if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK) dir.Text = fd.SelectedPath; }
                }
                catch { }
            };
            urls.TextChanged += delegate { err.Visibility = Visibility.Collapsed; };
            bool ok = false; string[] list = new string[0];
            ((Button)w.FindName("dlOk")).Click += delegate
            {
                List<string> l = new List<string>();
                foreach (string x in urls.Text.Split('\n')) { string s = x.Trim(); if (Regex.IsMatch(s, @"^(https?://|magnet:\?)\S+$")) l.Add(s); }
                if (l.Count == 0) { err.Text = "Enter at least one link that starts with http://, https:// or magnet:"; err.Visibility = Visibility.Visible; return; }
                if (dir.Text.Trim().Length == 0) { err.Text = "Choose a folder to save the files in."; err.Visibility = Visibility.Visible; return; }
                list = l.ToArray(); ok = true; w.Close();
            };
            w.Loaded += delegate { urls.Focus(); };
            w.ShowDialog();
            if (ok)
                AddItems(list, dir.Text.Trim(), name.Text, ((CheckBox)w.FindName("dlStart")).IsChecked == true, ((CheckBox)w.FindName("dlResume")).IsChecked == true,
                    ((CheckBox)w.FindName("dlRun")).IsChecked == true, ((CheckBox)w.FindName("dlVerify")).IsChecked == true);
        }
    }
}
