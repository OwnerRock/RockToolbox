using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace RockToolbox.Native
{
    internal sealed class WpItem
    {
        public string Thumb, Full, Title, Src, Id; public int W, H; public BitmapSource Img;
    }

    // C# port of src\pages\Wallpapers.ps1: Wallhaven + Wikimedia + Openverse feed, set desktop / lock screen, daily Bing task.
    internal sealed class WallpapersPage : IPage
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SystemParametersInfo(int a, int b, string c, int d);

        private const string TaskName = "Rock Daily Wallpaper";
        private readonly List<WpItem> _items = new List<WpItem>();
        private readonly List<Border> _cards = new List<Border>();
        private WrapPanel _grid;
        private StackPanel _stack;
        private Border _msg;
        private string _custom = "";
        private ScrollViewer _scroll;
        private TextBox _query;
        private TextBlock _info;
        private ComboBox _style;
        private Border _sel;
        private int _page = 1;
        private bool _hasMore = true, _loaded;
        private string _mode = "feed", _q = "", _title = "";
        private string CacheDir { get { return Path.Combine(WpDir, "cache"); } }
        private string WpDir { get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox"), "Wallpapers"); } }

        public string Key { get { return "Wallpapers"; } }

        public void Init(Window w, Logger log)
        {
            Panel host = Ui.Find<Panel>("wpPanel");
            _stack = new StackPanel(); host.Children.Add(_stack);
            _grid = new WrapPanel(); _stack.Children.Add(_grid);
            try { _custom = Ui.Settings.GetString("WpFolder") ?? ""; } catch { _custom = ""; }
            _scroll = Ui.Find<ScrollViewer>("wpScroll");
            _query = Ui.Find<TextBox>("txtWpQuery");
            _info = Ui.Find<TextBlock>("txtWpInfo");
            _style = Ui.Find<ComboBox>("cmbWpStyle");
            foreach (string s in new string[] { "Fill", "Fit", "Stretch", "Tile", "Center", "Span" }) _style.Items.Add(s);
            _style.SelectedIndex = 0;

            Ui.Find<Button>("btnWpSearch").Click += delegate { SearchBox(); };
            _query.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Return) { e.Handled = true; SearchBox(); } };
            Ui.Find<Button>("btnWpMore").Click += delegate { if (_cards.Count > 0) Search(_mode, _q, _title, true); };
            Ui.Find<Button>("btnWpMine").Click += delegate { Search("local", "", "", false); };
            Ui.Find<Button>("btnWpFolder").Click += delegate { ChooseFolder(); };
            Ui.Find<Button>("btnWpAdd").Click += delegate { AddPictures(); };
            Ui.Find<Button>("btnWpSimilar").Click += delegate
            {
                if (_sel == null) { Ui.Log.Warn("Click a picture first, then press Find similar.", "Wallpaper"); return; }
                WpItem it = (WpItem)_sel.Tag;
                if (it.Src == "Wallhaven" && it.Id.Length > 0) Search("like", it.Id, it.Title, false);
                else { string t = it.Title; if (string.IsNullOrEmpty(t)) t = _query.Text.Trim(); Search("search", t, t, false); }
            };
            _scroll.ScrollChanged += delegate(object s, ScrollChangedEventArgs e)
            {
                if (e.VerticalChange <= 0 || !_hasMore || _searching || _cards.Count == 0) return;
                if (_scroll.VerticalOffset >= _scroll.ScrollableHeight - 260) Search(_mode, _q, _title, true);
            };
            WrapPanel chips = Ui.Find<WrapPanel>("wpChips");
            foreach (string c in new string[] { "Nature", "Space", "Anime", "Cars", "Minimal", "Dark", "Cyberpunk", "City", "Abstract", "Gaming", "Mountains", "Ocean", "Sunset", "Forest" })
            {
                Button b = new Button(); b.Content = c; b.Tag = c; b.Padding = new Thickness(12, 4, 12, 4); b.Margin = new Thickness(3, 2, 3, 2);
                try { b.Style = (Style)Ui.Win.FindResource("ChipBtn"); b.Margin = new Thickness(3, 2, 3, 4); } catch { }
                b.Click += delegate(object s, RoutedEventArgs e) { string t = (string)((Button)s).Tag; _query.Text = t; Search("search", t, "", false); };
                chips.Children.Add(b);
            }
            Ui.Find<Button>("btnWpSet").Click += delegate { ApplySel("Desktop"); };
            Ui.Find<Button>("btnWpLock").Click += delegate { ApplySel("Lock"); };
            Ui.Find<Button>("btnWpBoth").Click += delegate { ApplySel("Both"); };
            Ui.Find<Button>("btnWpRandom").Click += delegate
            {
                if (_items.Count == 0) { Ui.Log.Warn("Search for some wallpapers first.", "Wallpaper"); return; }
                Apply(_items[new Random().Next(_items.Count)], "Desktop");
            };
            Ui.Find<Button>("btnWpSave").Click += delegate
            {
                if (_sel == null) { Ui.Log.Warn("Select a wallpaper first.", "Wallpaper"); return; }
                WpItem it = (WpItem)_sel.Tag;
                Work.Run("Saving picture...", delegate(WorkCtx c)
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WinToolbox");
                    Directory.CreateDirectory(dir);
                    c.Info("Saved: " + DownloadItem(c, it, dir), "Wallpaper");
                });
            };
            Ui.Find<Button>("btnWpLocal").Click += delegate
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog(); dlg.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp";
                if (dlg.ShowDialog() == true) { SetDesktop(dlg.FileName, (string)_style.SelectedItem); Ui.Log.Info("Wallpaper applied: " + dlg.FileName, "Wallpaper"); }
            };
            Ui.Find<Button>("btnWpLockLocal").Click += delegate
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog(); dlg.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp";
                if (dlg.ShowDialog() != true) return;
                string picked = dlg.FileName;
                Work.Run("Setting lock screen...", delegate(WorkCtx c)
                {
                    try { SetLockScreen(picked); c.Info("Lock screen image applied: " + picked, "Wallpaper"); }
                    catch (Exception ex) { c.Error("Could not set the lock screen: " + ex.Message, "Wallpaper"); }
                });
            };
            Ui.Find<Button>("btnWpDailyOn").Click += delegate { DailyOn(); };
            Ui.Find<Button>("btnWpDailyOff").Click += delegate { TaskSched.DeleteByPrefix(TaskName); Ui.Log.Info("Daily Bing wallpaper disabled.", "Wallpaper"); };
        }

        public void OnShown() { if (!_loaded && !_searching) Search("feed", "", "", false); }

        private void SearchBox()
        {
            string q = _query.Text.Trim();
            if (q.Length == 0) Search("feed", "", "", false); else Search("search", q, "", false);
        }

        // ---------------- search ----------------
        private static List<WpItem> Fetch(string mode, string query, string title, int page, List<string> errs)
        {
            List<WpItem> wh = new List<WpItem>(), wm = new List<WpItem>(), ov = new List<WpItem>(), bn = new List<WpItem>();
            try
            {
                string q = query, sort = "relevance";
                if (mode == "feed") { q = ""; sort = "toplist"; } else if (mode == "like") q = "like:" + query;
                string u = "https://wallhaven.cc/api/v1/search?q=" + Uri.EscapeDataString(q) + "&categories=111&purity=100&sorting=" + sort + "&order=desc&atleast=1280x720&page=" + page;
                if (sort == "toplist") u += "&topRange=1M";
                object r = Net.GetJson(u, 25000);
                ArrayList data = Net.Arr(Net.Get(r, "data"));
                if (data != null)
                    foreach (object w in data)
                    {
                        WpItem it = new WpItem();
                        it.Thumb = Net.Str(Net.Get(w, "thumbs"), "large"); it.Full = Net.Str(w, "path"); it.Id = Net.Str(w, "id");
                        it.Title = "Wallhaven " + it.Id; it.W = Net.Int(w, "dimension_x"); it.H = Net.Int(w, "dimension_y"); it.Src = "Wallhaven";
                        wh.Add(it);
                    }
            }
            catch (Exception ex) { errs.Add("Wallhaven: " + Net.FriendlyError(ex)); }
            string text = mode == "like" ? title : query;
            if (mode != "feed" && !string.IsNullOrEmpty(text) && (mode != "like" || (!string.IsNullOrEmpty(title) && !title.StartsWith("Wallhaven "))))
            {
                string qe = Uri.EscapeDataString(text);
                try
                {
                    int off = (page - 1) * 20;
                    string u = "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=20&gsroffset=" + off + "&gsrsearch=" + qe + "%20filetype:bitmap&prop=imageinfo&iiprop=url|size|mime&iiurlwidth=500";
                    object r = Net.GetJson(u, 25000);
                    Dictionary<string, object> pages = Net.Get(Net.Get(r, "query"), "pages") as Dictionary<string, object>;
                    if (pages != null)
                        foreach (KeyValuePair<string, object> kv in pages)
                        {
                            ArrayList ii = Net.Arr(Net.Get(kv.Value, "imageinfo"));
                            if (ii == null || ii.Count == 0) continue;
                            object info = ii[0];
                            if (Net.Int(info, "width") < 1400 || Net.Int(info, "height") < 700) continue;
                            string mime = Net.Str(info, "mime");
                            if (mime != "image/jpeg" && mime != "image/png") continue;
                            string t = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(Net.Str(kv.Value, "title"), "^File:", ""), @"\.[A-Za-z]+$", "");
                            WpItem it = new WpItem(); it.Thumb = Net.Str(info, "thumburl"); it.Full = Net.Str(info, "url"); it.Title = t;
                            it.W = Net.Int(info, "width"); it.H = Net.Int(info, "height"); it.Src = "Wikimedia"; it.Id = "";
                            wm.Add(it);
                        }
                }
                catch (Exception ex) { errs.Add("Wikimedia: " + Net.FriendlyError(ex)); }
                try
                {
                    string u = "https://api.openverse.org/v1/images/?q=" + qe + "&page=" + page + "&page_size=20&aspect_ratio=wide&size=large&mature=false";
                    object r = Net.GetJson(u, 25000);
                    ArrayList res = Net.Arr(Net.Get(r, "results"));
                    if (res != null)
                        foreach (object x in res)
                        {
                            if (Net.Str(x, "url").Length == 0 || Net.Str(x, "thumbnail").Length == 0) continue;
                            WpItem it = new WpItem(); it.Thumb = Net.Str(x, "thumbnail"); it.Full = Net.Str(x, "url"); it.Title = Net.Str(x, "title");
                            it.W = Net.Int(x, "width"); it.H = Net.Int(x, "height"); it.Src = "Openverse"; it.Id = "";
                            ov.Add(it);
                        }
                }
                catch (Exception ex) { errs.Add("Openverse: " + Net.FriendlyError(ex)); }
            }
            // Bing's daily pictures: a source that nearly always works, used in the trending feed and as a fallback
            if (mode == "feed" || (wh.Count == 0 && wm.Count == 0 && ov.Count == 0))
            {
                try
                {
                    object r = Net.GetJson("https://www.bing.com/HPImageArchive.aspx?format=js&idx=" + ((page - 1) * 8) + "&n=8&mkt=en-US", 25000);
                    ArrayList imgs = Net.Arr(Net.Get(r, "images"));
                    if (imgs != null)
                        foreach (object x in imgs)
                        {
                            string bu = Net.Str(x, "urlbase"); if (bu.Length == 0) continue;
                            WpItem it = new WpItem(); it.Thumb = "https://www.bing.com" + bu + "_400x240.jpg"; it.Full = "https://www.bing.com" + bu + "_1920x1080.jpg";
                            it.Title = Net.Str(x, "title"); if (it.Title.Length == 0) it.Title = "Bing picture";
                            it.W = 1920; it.H = 1080; it.Src = "Bing"; it.Id = "";
                            bn.Add(it);
                        }
                }
                catch (Exception ex) { errs.Add("Bing: " + Net.FriendlyError(ex)); }
            }
            List<WpItem> mixed = new List<WpItem>(); Dictionary<string, bool> seen = new Dictionary<string, bool>();
            int max = Math.Max(Math.Max(wh.Count, bn.Count), Math.Max(wm.Count, ov.Count));
            for (int i = 0; i < max; i++)
                foreach (List<WpItem> l in new List<WpItem>[] { wh, bn, wm, ov })
                    if (i < l.Count && !seen.ContainsKey(l[i].Full)) { seen[l[i].Full] = true; mixed.Add(l[i]); }
            return mixed;
        }

        private bool _searching;

        private static readonly string[] ImgExt = new string[] { ".jpg", ".jpeg", ".png", ".bmp" };

        private List<string> LocalDirs()
        {
            List<string> dirs = new List<string>();
            if (_custom.Length > 0) dirs.Add(_custom);
            else dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            dirs.Add(WpDir);
            return dirs;
        }

        private static void CollectImages(string dir, int depth, List<FileInfo> into)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                foreach (FileInfo f in new DirectoryInfo(dir).GetFiles())
                    if (Array.IndexOf(ImgExt, f.Extension.ToLowerInvariant()) >= 0) into.Add(f);
            }
            catch { }
            if (depth <= 0) return;
            try { foreach (DirectoryInfo d in new DirectoryInfo(dir).GetDirectories()) { if (d.Name != "cache") CollectImages(d.FullName, depth - 1, into); } } catch { }
        }

        // pictures from the local folders (no internet needed); newest first, capped
        private static List<WpItem> LocalItems(List<string> dirs)
        {
            List<FileInfo> files = new List<FileInfo>();
            foreach (string d in dirs) CollectImages(d, 1, files);
            files.Sort(delegate(FileInfo x, FileInfo y) { return y.LastWriteTime.CompareTo(x.LastWriteTime); });
            List<WpItem> res = new List<WpItem>(); Dictionary<string, bool> seen = new Dictionary<string, bool>();
            foreach (FileInfo f in files)
            {
                if (res.Count >= 150) break;
                string key = f.FullName.ToLowerInvariant();
                if (seen.ContainsKey(key)) continue;
                seen[key] = true;
                WpItem it = new WpItem(); it.Thumb = f.FullName; it.Full = f.FullName; it.Title = Path.GetFileNameWithoutExtension(f.Name);
                it.Src = "Local"; it.Id = ""; res.Add(it);
            }
            return res;
        }

        private static void LoadPreviews(List<WpItem> want, List<string> errs)
        {
            Parallel.ForEach(want, new ParallelOptions { MaxDegreeOfParallelism = 6 }, delegate(WpItem it)
            {
                try
                {
                    byte[] data = it.Src == "Local" ? File.ReadAllBytes(it.Thumb) : Net.GetBytes(it.Thumb, 20000);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit(); bi.StreamSource = new MemoryStream(data); bi.DecodePixelWidth = 320; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                    it.Img = bi;
                    if (it.Src == "Local") { it.W = bi.PixelWidth; }
                }
                catch (Exception ex) { lock (errs) errs.Add("Preview: " + Net.FriendlyError(ex)); }
            });
        }

        // runs on its own thread (not the global task runner), so a running install / download can never block the pictures
        private void Search(string mode, string query, string title, bool append)
        {
            if (_searching) return;
            _searching = true;
            int page = append ? _page + 1 : 1;
            _info.Text = mode == "local" ? "Loading your pictures..." : "Searching pictures...";
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            if (append) foreach (WpItem x in _items) seen[x.Full] = true;
            bool wantLocal = !append && mode != "local" && _cards.Count == 0;
            List<string> dirs = LocalDirs();
            Thread t = new Thread(delegate()
            {
                List<string> errs = new List<string>();
                List<WpItem> fresh = new List<WpItem>();
                List<WpItem> localFresh = new List<WpItem>();
                bool online = true;
                try
                {
                    List<WpItem> rows = mode == "local" ? LocalItems(dirs) : Fetch(mode, query, title, page, errs);
                    List<WpItem> want = rows.FindAll(delegate(WpItem x) { return x.Full.Length > 0 && x.Thumb.Length > 0 && !seen.ContainsKey(x.Full); });
                    // download the small previews here (in parallel) and hand finished pictures to the window
                    LoadPreviews(want, errs);
                    foreach (WpItem it in want) if (it.Img != null) fresh.Add(it);
                }
                catch (Exception ex) { errs.Add(Net.FriendlyError(ex)); }
                if (fresh.Count == 0 && mode != "local" && errs.Count > 0)
                {
                    try { online = Net.IsOnline(); } catch { }
                    if (wantLocal)
                    {
                        try
                        {
                            List<WpItem> lw = LocalItems(dirs);
                            LoadPreviews(lw, new List<string>());
                            foreach (WpItem it in lw) if (it.Img != null) localFresh.Add(it);
                        }
                        catch { }
                    }
                }
                Ui.Post(delegate
                {
                    _searching = false;
                    if (fresh.Count == 0)
                    {
                        _hasMore = false;
                        string first = errs.Count > 0 ? errs[0] : "";
                        int ci = first.IndexOf(": ");
                        string reason = ci > 0 && ci < 12 ? first.Substring(ci + 2) : first;
                        if (!online) reason = "No internet connection or DNS cannot reach the server.";
                        if (append) { _info.Text = "No more results for this search."; return; }
                        if (mode == "local")
                        {
                            _grid.Children.Clear(); _items.Clear(); _cards.Clear(); _sel = null; _mode = "local"; _loaded = true;
                            ShowMsg("ℹ", "No pictures in this folder", "Looked in: " + string.Join(";  ", dirs.ToArray()), mode, query, title, false);
                            _info.Text = "Use \"Add pictures...\" to copy images here, or \"Choose folder...\" to pick another folder.";
                            return;
                        }
                        if (errs.Count == 0)
                        {
                            ShowMsg("\u2315", "Nothing found", "Nothing matched this search. Try another word (for example: forest, space, sports car).", mode, query, title, true);
                            _info.Text = "No pictures found.";
                            return;
                        }
                        if (localFresh.Count > 0)
                        {
                            _mode = "local"; _q = ""; _title = ""; _loaded = true;
                            ShowCards(localFresh, true);
                            ShowMsg("⚠", "Can't reach the online sources", reason + "  Showing your own pictures instead.", mode, query, title, true);
                            _info.Text = "Offline mode - " + _items.Count + " pictures from your computer. Press Retry when your connection is back.";
                            Ui.Log.Warn("Online search failed: " + reason + " Showing local pictures.", "Wallpaper");
                            return;
                        }
                        ShowMsg("⚠", "Can't reach the online sources", reason + "  You can still use your own pictures.", mode, query, title, true);
                        _info.Text = "No pictures found. Check your internet connection, or use My pictures / Add pictures...";
                        Ui.Log.Warn("No pictures found. " + reason, "Wallpaper");
                        return;
                    }
                    HideMsg();
                    if (append) _page = page;
                    else { _page = 1; _mode = mode; _q = query; _title = title; }
                    _hasMore = mode != "local"; _loaded = true;
                    ShowCards(fresh, !append);
                    string what = _mode == "local" ? "My pictures" : (_mode == "feed" ? "Trending wallpapers" : (_mode == "like" ? "Similar to your pick" : "Results for \"" + _q + "\""));
                    _info.Text = what + " - " + _items.Count + " pictures. Click one to select it, double-click to set it as wallpaper, or press Find similar.";
                    Ui.Log.Info(what + " : " + _items.Count + " pictures.", "Wallpaper");
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void ShowCards(List<WpItem> fresh, bool reset)
        {
            if (reset) { _grid.Children.Clear(); _items.Clear(); _cards.Clear(); _sel = null; _scroll.ScrollToTop(); }
            int idx = 0;
            foreach (WpItem it in fresh) { _items.Add(it); Border card = NewCard(it, idx++); _cards.Add(card); _grid.Children.Add(card); }
        }

        private void HideMsg()
        {
            if (_msg != null) { _stack.Children.Remove(_msg); _msg = null; }
        }

        // friendly in-page message card (icon + title + reason + buttons); pops in with an animation
        private void ShowMsg(string icon, string title, string reason, string mode, string query, string qtitle, bool canRetry)
        {
            HideMsg();
            Border card = new Border(); card.Margin = new Thickness(2, 2, 2, 10); card.Padding = new Thickness(18, 14, 18, 14); card.CornerRadius = new CornerRadius(10);
            card.Background = Ui.Brush("#22272E"); card.BorderBrush = Ui.Brush("#3A414C"); card.BorderThickness = new Thickness(1);
            card.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock ic = new TextBlock(); ic.Text = icon; ic.FontSize = 34; ic.Margin = new Thickness(0, 0, 16, 0); ic.VerticalAlignment = VerticalAlignment.Top;
            ic.Foreground = Ui.Brush("#F5B94A");
            Grid.SetColumn(ic, 0); g.Children.Add(ic);
            StackPanel sp = new StackPanel(); Grid.SetColumn(sp, 1);
            TextBlock h = new TextBlock(); h.Text = title; h.FontSize = 16; h.FontWeight = FontWeights.SemiBold; h.Foreground = Ui.Brush("#F2F4FF");
            TextBlock r = new TextBlock(); r.Text = reason; r.TextWrapping = TextWrapping.Wrap; r.Foreground = Ui.Brush("#9AA0B8"); r.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(h); sp.Children.Add(r);
            WrapPanel bar = new WrapPanel(); bar.Margin = new Thickness(0, 10, 0, 0);
            if (canRetry)
            {
                Button retry = new Button(); retry.Content = "Retry"; retry.Padding = new Thickness(20, 6, 20, 6); retry.Margin = new Thickness(0, 0, 6, 0);
                retry.Click += delegate { Search(mode == "local" ? "feed" : mode, query, qtitle, false); };
                bar.Children.Add(retry);
            }
            if (canRetry && reason.IndexOf("DNS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Button fix = new Button(); fix.Content = "Fix DNS"; fix.Padding = new Thickness(16, 6, 16, 6); fix.Margin = new Thickness(0, 0, 6, 0);
                fix.ToolTip = "Switch this PC to Cloudflare DNS (1.1.1.1), then try again";
                fix.Click += delegate { TweaksPage.OfferDnsFix(delegate { Search(mode == "local" ? "feed" : mode, query, qtitle, false); }); };
                bar.Children.Add(fix);
            }
            Button mine = new Button(); mine.Content = "My pictures"; mine.Padding = new Thickness(14, 6, 14, 6); mine.Margin = new Thickness(0, 0, 6, 0);
            mine.Click += delegate { Search("local", "", "", false); };
            Button fld = new Button(); fld.Content = "Choose folder..."; fld.Padding = new Thickness(14, 6, 14, 6); fld.Margin = new Thickness(0, 0, 6, 0);
            fld.Click += delegate { ChooseFolder(); };
            Button add = new Button(); add.Content = "Add pictures..."; add.Padding = new Thickness(14, 6, 14, 6);
            add.Click += delegate { AddPictures(); };
            bar.Children.Add(mine); bar.Children.Add(fld); bar.Children.Add(add);
            sp.Children.Add(bar);
            g.Children.Add(sp);
            card.Child = g;
            // pop-in
            ScaleTransform sc = new ScaleTransform(0.92, 0.92);
            card.RenderTransformOrigin = new Point(0.5, 0.0); card.RenderTransform = sc; card.Opacity = 0;
            _msg = card;
            _stack.Children.Insert(0, card);
            try
            {
                DoubleAnimation pop = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(380)); pop.EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, pop); sc.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            }
            catch { card.Opacity = 1; }
        }

        private void ChooseFolder()
        {
            using (System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Choose a folder with pictures";
                if (_custom.Length > 0 && Directory.Exists(_custom)) dlg.SelectedPath = _custom;
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                _custom = dlg.SelectedPath;
            }
            try { Ui.Settings.Set("WpFolder", _custom); Ui.Settings.SaveSoon(); } catch { }
            Search("local", "", "", false);
        }

        private void AddPictures()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"; dlg.Multiselect = true; dlg.Title = "Add pictures";
            if (dlg.ShowDialog() != true) return;
            int n = 0;
            try
            {
                Directory.CreateDirectory(WpDir);
                foreach (string f in dlg.FileNames)
                {
                    try
                    {
                        string dest = Path.Combine(WpDir, Path.GetFileName(f));
                        if (File.Exists(dest)) dest = Path.Combine(WpDir, Path.GetFileNameWithoutExtension(f) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + Path.GetExtension(f));
                        File.Copy(f, dest); n++;
                    }
                    catch (Exception ex) { Ui.Log.Warn("Could not copy " + f + ": " + ex.Message, "Wallpaper"); }
                }
            }
            catch (Exception ex) { Ui.Warn("Could not add the pictures.\n\n" + ex.Message); return; }
            Ui.Log.Info(n + " picture(s) added to " + WpDir, "Wallpaper");
            Search("local", "", "", false);
        }

        private Border NewCard(WpItem it, int index)
        {
            Border b = new Border(); b.Width = 250; b.Height = 150; b.Margin = new Thickness(0, 0, 8, 8); b.CornerRadius = new CornerRadius(6);
            b.BorderThickness = new Thickness(2); b.BorderBrush = Brushes.Transparent; b.Background = Ui.Brush("#1B1F24"); b.Cursor = Cursors.Hand; b.Tag = it;
            b.ToolTip = it.Title + "  (" + it.Src + (it.W > 0 && it.H > 0 ? ", " + it.W + "x" + it.H : "") + ")";
            Image img = new Image(); img.Stretch = Stretch.UniformToFill; img.Source = it.Img;
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            Border clip = new Border(); clip.CornerRadius = new CornerRadius(4); clip.ClipToBounds = true; clip.Child = img;
            b.Child = clip;
            // stagger fade / slide-in, hover scale
            ScaleTransform sc = new ScaleTransform(1, 1); TranslateTransform tt = new TranslateTransform(0, 18);
            TransformGroup tg = new TransformGroup(); tg.Children.Add(sc); tg.Children.Add(tt);
            b.RenderTransformOrigin = new Point(0.5, 0.5); b.RenderTransform = tg; b.Opacity = 0;
            try
            {
                TimeSpan begin = TimeSpan.FromMilliseconds(Math.Min(index, 24) * 30);
                DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)); fade.BeginTime = begin;
                DoubleAnimation slide = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(360)); slide.BeginTime = begin; slide.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                b.BeginAnimation(UIElement.OpacityProperty, fade);
                tt.BeginAnimation(TranslateTransform.YProperty, slide);
            }
            catch { b.Opacity = 1; tt.Y = 0; }
            b.MouseEnter += delegate
            {
                DoubleAnimation up = new DoubleAnimation(1.04, TimeSpan.FromMilliseconds(130)); up.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, up); sc.BeginAnimation(ScaleTransform.ScaleYProperty, up);
                Panel.SetZIndex(b, 5);
            };
            b.MouseLeave += delegate
            {
                DoubleAnimation dn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160)); dn.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, dn); sc.BeginAnimation(ScaleTransform.ScaleYProperty, dn);
                Panel.SetZIndex(b, 0);
            };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                if (_sel != null) _sel.BorderBrush = Brushes.Transparent;
                _sel = b; b.BorderBrush = Ui.Brush("#2F6FB0");
                if (e.ClickCount >= 2) Apply(it, "Desktop");
            };
            return b;
        }

        // ---------------- apply ----------------
        private void ApplySel(string target)
        {
            if (_sel == null) { Ui.Log.Warn("Select a wallpaper first.", "Wallpaper"); return; }
            Apply((WpItem)_sel.Tag, target);
        }

        private static string DownloadItem(WorkCtx c, WpItem it, string folder)
        {
            string ext = Path.GetExtension(it.Full.Split('?')[0]);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
            string file = Path.Combine(folder, "wp_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext);
            if (it.Src == "Local") { File.Copy(it.Full, file); return file; }
            c.Info("Downloading: " + it.Title, "Wallpaper");
            Net.Download(it.Full, file, delegate(long got, long total) { if (total > 0) c.Progress(100.0 * got / total, null); }, delegate { return c.Cancelled; });
            return file;
        }

        private void Apply(WpItem it, string target)
        {
            string style = (string)_style.SelectedItem; string dir = CacheDir;
            Work.Run("Setting wallpaper...", delegate(WorkCtx c)
            {
                Directory.CreateDirectory(dir);
                string file = it.Src == "Local" ? it.Full : DownloadItem(c, it, dir);
                if (target == "Desktop" || target == "Both") { SetDesktop(file, style); c.Info("Desktop wallpaper applied.", "Wallpaper"); }
                if (target == "Lock" || target == "Both")
                {
                    try { SetLockScreen(file); c.Info("Lock screen image applied (press Win+L to preview).", "Wallpaper"); }
                    catch (Exception ex) { c.Error("Could not set the lock screen: " + ex.Message, "Wallpaper"); }
                }
                try
                {
                    List<FileInfo> files = new List<FileInfo>(new DirectoryInfo(dir).GetFiles());
                    files.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                    for (int i = 20; i < files.Count; i++) try { files[i].Delete(); } catch { }
                }
                catch { }
            });
        }

        public static void SetDesktop(string path, string style)
        {
            string[] m;
            switch (style) { case "Fit": m = new string[] { "6", "0" }; break; case "Stretch": m = new string[] { "2", "0" }; break; case "Tile": m = new string[] { "0", "1" }; break;
                case "Center": m = new string[] { "0", "0" }; break; case "Span": m = new string[] { "22", "0" }; break; default: m = new string[] { "10", "0" }; break; }
            Reg.SetString(@"HKCU:\Control Panel\Desktop", "WallpaperStyle", m[0]);
            Reg.SetString(@"HKCU:\Control Panel\Desktop", "TileWallpaper", m[1]);
            SystemParametersInfo(20, 0, path, 3);
        }

        private static void SetLockScreen(string path)
        {
            Type sf = Type.GetType("Windows.Storage.StorageFile, Windows, ContentType=WindowsRuntime");
            Type ls = Type.GetType("Windows.System.UserProfile.LockScreen, Windows, ContentType=WindowsRuntime");
            Type gen = Type.GetType("Windows.Foundation.IAsyncOperation`1, Windows, ContentType=WindowsRuntime");
            if (sf == null || ls == null || gen == null) throw new InvalidOperationException("Windows Runtime lock screen API not available.");
            object op = sf.GetMethod("GetFileFromPathAsync").Invoke(null, new object[] { path });
            Appx.Wait(op);
            object file = gen.MakeGenericType(sf).GetMethod("GetResults").Invoke(op, null);
            object act = ls.GetMethod("SetImageFileAsync").Invoke(null, new object[] { file });
            Appx.Wait(act);
        }

        // ---------------- daily Bing wallpaper (scheduled task -> RockToolboxDaily.exe, no PowerShell) ----------------
        private static void DailyOn()
        {
            Work.Run("Enabling daily wallpaper...", delegate(WorkCtx c)
            {
                string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string exe = Path.Combine(dir, "RockToolboxDaily.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException("RockToolboxDaily.exe was not found next to the program: " + exe);
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); svc.Connect();
                dynamic root = svc.GetFolder("\\");
                dynamic def = svc.NewTask(0);
                def.RegistrationInfo.Description = "Rock Toolbox - daily Bing wallpaper";
                def.Settings.StartWhenAvailable = true; def.Settings.DisallowStartIfOnBatteries = false; def.Settings.StopIfGoingOnBatteries = false;
                dynamic t1 = def.Triggers.Create(9); t1.UserId = user;
                dynamic t2 = def.Triggers.Create(2); t2.StartBoundary = DateTime.Today.AddHours(9).ToString("s"); t2.DaysInterval = 1;
                dynamic act = def.Actions.Create(0); act.Path = exe;
                def.Principal.UserId = user; def.Principal.LogonType = 3; def.Principal.RunLevel = 0;
                root.RegisterTaskDefinition(TaskName, def, 6, null, null, 3);
                try { root.GetTask(TaskName).Run(null); } catch { }
                c.Info("Daily Bing wallpaper enabled (runs at logon and every day at 9:00 AM, even when this tool is closed).", "Wallpaper");
            });
        }
    }
}
