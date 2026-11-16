using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Threading;

namespace RDownloader.Native
{
    // icon + colour of a file in the download windows
    internal static class DlGlyph
    {
        private static string Ext(string name, string url)
        {
            if (Aria.IsTorrentLink(url)) return "torrent";
            string e = "";
            try { e = Path.GetExtension(name ?? "").TrimStart('.').ToLowerInvariant(); } catch (Exception) { }
            return e;
        }

        // IDM-like category of a file: Compressed, Documents, Music, Programs, Video (or "" for anything else)
        public static string Category(string name, string url)
        {
            switch (Ext(name, url))
            {
                case "zip": case "rar": case "7z": case "gz": case "tar": case "cab": case "bz2": case "xz": case "tgz": return "Compressed";
                case "pdf": case "doc": case "docx": case "xls": case "xlsx": case "ppt": case "pptx": case "txt": case "epub": case "rtf": case "csv": case "odt": return "Documents";
                case "mp3": case "wav": case "flac": case "m4a": case "ogg": case "aac": case "wma": return "Music";
                case "exe": case "msi": case "msix": case "appx": case "iso": case "img": case "apk": case "dmg": case "deb": case "bat": return "Programs";
                case "mp4": case "mkv": case "avi": case "mov": case "webm": case "wmv": case "flv": case "m4v": case "mpg": case "mpeg": return "Video";
                default: return "";
            }
        }

        public static string Glyph(string name, string url)
        {
            switch (Ext(name, url))
            {
                case "torrent": return "";
                case "mp4": case "mkv": case "avi": case "mov": case "webm": case "wmv": return "";
                case "mp3": case "wav": case "flac": case "m4a": case "ogg": return "";
                case "jpg": case "jpeg": case "png": case "gif": case "webp": case "bmp": return "";
                case "zip": case "rar": case "7z": case "gz": case "tar": case "cab": return "";
                case "exe": case "msi": case "msix": case "appx": return "";
                case "iso": case "img": return "";
                default: return "";
            }
        }

        public static string Color(string name, string url)
        {
            return DlTheme.Map(RawColor(name, url));
        }

        private static string RawColor(string name, string url)
        {
            switch (Ext(name, url))
            {
                case "torrent": return "#52B788";
                case "mp4": case "mkv": case "avi": case "mov": case "webm": case "wmv": return "#7C9CD8";
                case "mp3": case "wav": case "flac": case "m4a": case "ogg": return "#D88C9A";
                case "jpg": case "jpeg": case "png": case "gif": case "webp": case "bmp": return "#E0B26B";
                case "zip": case "rar": case "7z": case "gz": case "tar": case "cab": return "#E0A76B";
                case "iso": case "img": return "#6BC4B0";
                default: return "#4DA3C4";
            }
        }
    }

    internal sealed class ConnRow : Notify
    {
        private string _n = "", _d = "", _i = "";
        public string N { get { return _n; } set { if (_n != value) { _n = value; Raise("N"); } } }
        public string Downloaded { get { return _d; } set { if (_d != value) { _d = value; Raise("Downloaded"); } } }
        public string Info { get { return _i; } set { if (_i != value) { _i = value; Raise("Info"); } } }
    }

    internal sealed class GrabRow : Notify
    {
        private bool _c = true;
        public bool Checked { get { return _c; } set { if (_c != value) { _c = value; Raise("Checked"); } } }
        public string Url { get; set; }
    }

    internal sealed class ProbeResult
    {
        public string Name = "", Mime = "", Err = "";
        public long Size = -1;
        public bool Resume;
    }

    // asks the server for name / size / resume support of a link (HEAD, then a 1-byte GET)
    internal static class DlProbe
    {
        public static ProbeResult Run(string url, string referrer, string ua)
        {
            ProbeResult r = new ProbeResult();
            try
            {
                if (Aria.IsTorrentLink(url)) { r.Name = Aria.NameOf(url); r.Resume = true; r.Mime = "torrent"; return r; }
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    HttpWebRequest q = (HttpWebRequest)WebRequest.Create(url);
                    q.Method = attempt == 0 ? "HEAD" : "GET";
                    q.Timeout = 15000; q.ReadWriteTimeout = 15000; q.AllowAutoRedirect = true;
                    q.UserAgent = string.IsNullOrEmpty(ua) ? DlOpts.UserAgent : ua; DlOpts.Apply(q, url); if (!string.IsNullOrEmpty(ua)) q.UserAgent = ua;
                    if (!string.IsNullOrEmpty(referrer)) { try { q.Referer = referrer; } catch (Exception) { } }
                    if (attempt == 1) q.AddRange(0, 0);
                    try
                    {
                        using (HttpWebResponse resp = (HttpWebResponse)q.GetResponse())
                        {
                            r.Name = RockDlEngine.GuessName(resp, url);
                            r.Mime = resp.ContentType ?? "";
                            long len = resp.ContentLength;
                            string cr = resp.Headers["Content-Range"];
                            if (!string.IsNullOrEmpty(cr))
                            {
                                int sl = cr.LastIndexOf('/'); long t;
                                if (sl >= 0 && long.TryParse(cr.Substring(sl + 1), out t)) len = t;
                            }
                            r.Size = len > 0 ? len : -1;
                            string ar = resp.Headers["Accept-Ranges"];
                            r.Resume = (ar != null && ar.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0) || resp.StatusCode == HttpStatusCode.PartialContent;
                            r.Err = "";
                            q.Abort();
                            return r;
                        }
                    }
                    catch (WebException ex) { r.Err = Net.FriendlyError(ex); }
                }
            }
            catch (Exception ex) { r.Err = ex.Message; }
            return r;
        }
    }

    // small animated pop-up menu (used by "Add to Rlist")
    internal static class RMenu
    {
        public static void Show(Window owner, FrameworkElement anchor, List<KeyValuePair<string, Action>> items, List<string> hints)
        {
            Popup p = new Popup();
            p.PlacementTarget = anchor; p.Placement = PlacementMode.Top; p.StaysOpen = false; p.AllowsTransparency = true; p.PopupAnimation = PopupAnimation.None;
            Border box = new Border();
            box.Background = DlTheme.Br("#232A33"); box.BorderBrush = DlTheme.Br("#313B47"); box.BorderThickness = new Thickness(1); box.CornerRadius = new CornerRadius(10);
            box.Padding = new Thickness(4); box.MinWidth = 230; box.MaxWidth = 420;
            DropShadowEffect fx = new DropShadowEffect(); fx.BlurRadius = 18; fx.ShadowDepth = 3; fx.Opacity = 0.5; fx.Color = Colors.Black; box.Effect = fx;
            StackPanel sp = new StackPanel();
            Style st = null;
            try { st = (Style)owner.FindResource("CalmMenuItem"); } catch (Exception) { }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Key == "-")
                {
                    Border sep = new Border(); sep.Height = 1; sep.Margin = new Thickness(8, 3, 8, 3); sep.Background = DlTheme.Br("#313B47"); sp.Children.Add(sep); continue;
                }
                Button b = new Button();
                if (st != null) b.Style = st;
                StackPanel c = new StackPanel();
                TextBlock t1 = new TextBlock(); t1.Text = items[i].Key; t1.TextTrimming = TextTrimming.CharacterEllipsis; c.Children.Add(t1);
                if (hints != null && i < hints.Count && !string.IsNullOrEmpty(hints[i]))
                {
                    TextBlock t2 = new TextBlock(); t2.Text = hints[i]; t2.FontSize = 10.5; t2.Foreground = DlTheme.Br("#8895A6"); t2.TextTrimming = TextTrimming.CharacterEllipsis; c.Children.Add(t2);
                }
                b.Content = c;
                Action act = items[i].Value;
                b.Click += delegate { p.IsOpen = false; if (act != null) act(); };
                sp.Children.Add(b);
            }
            box.Child = sp;
            Grid host = new Grid(); host.Margin = new Thickness(12); host.Children.Add(box);
            p.Child = host;
            p.IsOpen = true;
            Anim.PopIn(box, 0.0, 1.0);
        }
    }

    // the three download windows: Download File Info, per-file progress (IDM style) and the Rlist playlist
    internal static class DlWin
    {
        private static readonly Dictionary<string, Window> ProgWins = new Dictionary<string, Window>();
        private static readonly Dictionary<string, Window> ListWins = new Dictionary<string, Window>(StringComparer.OrdinalIgnoreCase);
        private static int _open;
        public static bool AnyOpen { get { return _open > 0; } }

        private static TT Find<TT>(Window w, string n) where TT : class { return w.FindName(n) as TT; }

        private static Window Load(string rel)
        {
            Window w = DlTheme.Load(rel);
            if (Lang.Rtl) w.FlowDirection = FlowDirection.RightToLeft;
            Button c = Find<Button>(w, "btnClose"); Button m = Find<Button>(w, "btnMin");
            Grid root = Find<Grid>(w, "root");
            bool closing = false;
            if (c != null) c.Click += delegate { w.Close(); };
            if (m != null) m.Click += delegate { w.WindowState = WindowState.Minimized; };
            w.Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                if (closing || root == null) return;
                e.Cancel = true; closing = true;
                try
                {
                    DoubleAnimation f = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(140)));
                    f.Completed += delegate { try { w.Close(); } catch (Exception) { } };
                    root.BeginAnimation(UIElement.OpacityProperty, f);
                }
                catch (Exception) { closing = true; w.Close(); }
            };
            w.Loaded += delegate { if (root != null) Anim.WindowIntro(root); };
            _open++;
            w.Closed += delegate { _open--; };
            return w;
        }

        private static void Front(Window w)
        {
            try
            {
                if (!w.IsVisible) w.Show();
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();
                bool top = w.Topmost; w.Topmost = true; w.Topmost = top;
            }
            catch (Exception) { }
        }

        private static void Flash(TextBlock t, string text, bool ok)
        {
            t.Text = Lang.L(text);
            t.Foreground = DlTheme.Br(ok ? "#52B788" : "#E5827F");
            DoubleAnimation a = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(200)));
            a.AutoReverse = false;
            t.BeginAnimation(UIElement.OpacityProperty, a);
        }

        private static string Folder()
        {
            try
            {
                using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
                { return fd.ShowDialog() == System.Windows.Forms.DialogResult.OK ? fd.SelectedPath : ""; }
            }
            catch (Exception) { return ""; }
        }

        // ================= Download File Info =================
        private static readonly Dictionary<string, Window> InfoWins = new Dictionary<string, Window>();

        public static void ShowInfo(string url, string name, string referrer, string ua, bool fromBrowser)
        {
            DownloadsPage page = DownloadsPage.Instance;
            if (page == null) return;
            // the same link is already waiting in a Download File Info window: bring that one to the front instead of opening another
            string key = (url ?? "").Trim();
            Window open;
            if (key.Length > 0 && InfoWins.TryGetValue(key, out open))
            {
                try { if (open.IsLoaded) { if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal; open.Activate(); return; } } catch (Exception) { }
                InfoWins.Remove(key);
            }
            Window w;
            try { w = Load("ui/DlInfo.xaml"); }
            catch (Exception ex) { Ui.Log.Error("Could not open the download window: " + ex.Message, "Downloads"); return; }
            if (key.Length > 0) { InfoWins[key] = w; w.Closed += delegate { Window cur; if (InfoWins.TryGetValue(key, out cur) && cur == w) InfoWins.Remove(key); }; }
            TextBox txtUrl = Find<TextBox>(w, "txtUrl"), txtDir = Find<TextBox>(w, "txtDir"), txtName = Find<TextBox>(w, "txtName");
            TextBlock txtFile = Find<TextBlock>(w, "txtFile"), txtSize = Find<TextBlock>(w, "txtSize"), txtResume = Find<TextBlock>(w, "txtResume"), txtIcon = Find<TextBlock>(w, "txtIcon"), txtMsg = Find<TextBlock>(w, "txtMsg");
            ScaleTransform icoSc = Find<ScaleTransform>(w, "icoSc");
            Button btnStart = Find<Button>(w, "btnStart"), btnLater = Find<Button>(w, "btnLater"), btnCancel = Find<Button>(w, "btnCancel"), btnBrowse = Find<Button>(w, "btnBrowse"), btnRlist = Find<Button>(w, "btnRlist");
            txtUrl.Text = url ?? ""; txtDir.Text = page.DlDir; txtName.Text = name ?? "";
            int gen = 0; bool nameEdited = !string.IsNullOrEmpty(name); string lastUrl = "";
            txtName.TextChanged += delegate { if (txtName.IsKeyboardFocused) nameEdited = true; };

            Action<string> probe = delegate(string u)
            {
                u = (u ?? "").Trim(); lastUrl = u;
                int my = ++gen;
                if (u.Length == 0) { txtFile.Text = "-"; txtSize.Text = Lang.L("Paste a link above"); txtResume.Text = ""; return; }
                txtSize.Text = Lang.L("Checking the link...");
                DoubleAnimation pulse = new DoubleAnimation(1.0, 0.35, new Duration(TimeSpan.FromMilliseconds(650)));
                pulse.AutoReverse = true; pulse.RepeatBehavior = RepeatBehavior.Forever;
                txtSize.BeginAnimation(UIElement.OpacityProperty, pulse);
                txtResume.Text = "";
                string rf = referrer, ag = ua;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    ProbeResult r = DlProbe.Run(u, rf, ag);
                    Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (my != gen) return;
                        txtSize.BeginAnimation(UIElement.OpacityProperty, null); txtSize.Opacity = 1;
                        string nm = r.Name;
                        if (!nameEdited && !string.IsNullOrEmpty(nm)) txtName.Text = nm;
                        string shown = txtName.Text.Trim().Length > 0 ? txtName.Text.Trim() : nm;
                        txtFile.Text = string.IsNullOrEmpty(shown) ? "-" : shown;
                        txtIcon.Text = DlGlyph.Glyph(shown, u); txtIcon.Foreground = DlTheme.Br(DlGlyph.Color(shown, u));
                        if (r.Mime == "torrent") txtSize.Text = Lang.L("Torrent / magnet - the size is known when the download starts");
                        else if (r.Size > 0) txtSize.Text = Lang.L("File size") + ": " + DownloadsPage.FormatSize(r.Size) + "  (" + r.Size.ToString("N0") + " bytes)";
                        else txtSize.Text = r.Err.Length > 0 ? Lang.L(r.Err) : Lang.L("File size") + ": " + Lang.L("unknown");
                        txtResume.Text = r.Err.Length > 0 && r.Size <= 0 ? "" : (r.Resume ? Lang.L("Resume is supported") : Lang.L("Resume is not supported"));
                        txtResume.Foreground = DlTheme.Br(r.Resume ? "#52B788" : "#E0A76B");
                        try
                        {
                            DoubleAnimation z = new DoubleAnimation(0.75, 1.0, new Duration(TimeSpan.FromMilliseconds(320)));
                            BackEase be = new BackEase(); be.EasingMode = EasingMode.EaseOut; be.Amplitude = 0.6; z.EasingFunction = be;
                            icoSc.BeginAnimation(ScaleTransform.ScaleXProperty, z); icoSc.BeginAnimation(ScaleTransform.ScaleYProperty, z);
                        }
                        catch (Exception) { }
                    }));
                });
            };
            txtUrl.LostFocus += delegate { if (txtUrl.Text.Trim() != lastUrl) probe(txtUrl.Text); };
            txtUrl.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter && txtUrl.Text.Trim() != lastUrl) probe(txtUrl.Text); };
            btnBrowse.Click += delegate { string f = Folder(); if (f.Length > 0) txtDir.Text = f; };
            btnCancel.Click += delegate { w.Close(); };

            Func<RockDownload> make = delegate
            {
                string u = txtUrl.Text.Trim();
                if (!Regex.IsMatch(u, @"^(https?://|magnet:\?)\S+$") && !Aria.IsTorrentLink(u)) { Flash(txtMsg, "Enter a link that starts with http://, https:// or magnet:", false); return null; }
                if (txtDir.Text.Trim().Length == 0) { Flash(txtMsg, "Choose a folder to save the file in.", false); return null; }
                return page.CreateItem(u, txtDir.Text.Trim(), txtName.Text.Trim(), true, false, false);
            };
            btnStart.IsDefault = true;
            btnStart.Click += delegate { RockDownload it = make(); if (it == null) return; page.Adopt(it, true, true); w.Close(); };
            btnLater.Click += delegate { RockDownload it = make(); if (it == null) return; page.Adopt(it, false, false); w.Close(); };
            btnRlist.Click += delegate
            {
                string u = txtUrl.Text.Trim();
                if (u.Length == 0) { Flash(txtMsg, "Paste a link first.", false); return; }
                string nm = txtName.Text.Trim();
                PickRlist(w, btnRlist, u, nm, delegate(string msg, bool ok) { Flash(txtMsg, msg, ok); });
            };
            w.Loaded += delegate { probe(txtUrl.Text); if (txtUrl.Text.Length == 0) txtUrl.Focus(); };
            if (!fromBrowser && Ui.Win != null && Ui.Win.IsVisible) { w.Owner = Ui.Win; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            w.Show();
            Front(w);
        }

        // puts a link into an Rlist: pick an existing list or make a new one (asks for the name and place)
        private static void PickRlist(Window w, FrameworkElement anchor, string url, string name, Action<string, bool> report)
        {
            List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
            List<string> hints = new List<string>();
            foreach (string path in Rlist.Known())
            {
                string p = path;
                items.Add(new KeyValuePair<string, Action>(Path.GetFileName(p), delegate { AddTo(p, url, name, report); }));
                hints.Add(Path.GetDirectoryName(p));
            }
            if (items.Count > 0) { items.Add(new KeyValuePair<string, Action>("-", null)); hints.Add(""); }
            items.Add(new KeyValuePair<string, Action>(Lang.L("New Rlist..."), delegate
            {
                try
                {
                    Microsoft.Win32.SaveFileDialog fd = new Microsoft.Win32.SaveFileDialog();
                    fd.Title = "Create a new Rlist"; fd.Filter = "Rlist (*.Rlist)|*.Rlist"; fd.DefaultExt = ".Rlist"; fd.FileName = "R-Downloader.Rlist"; fd.OverwritePrompt = false;
                    string d = DownloadsPage.Instance.DlDir; try { Directory.CreateDirectory(d); fd.InitialDirectory = d; } catch (Exception) { }
                    if (fd.ShowDialog(w) == true) AddTo(fd.FileName, url, name, report);
                }
                catch (Exception ex) { report("Could not create the Rlist: " + ex.Message, false); }
            }));
            hints.Add("");
            RMenu.Show(w, anchor, items, hints);
        }

        private static void AddTo(string path, string url, string name, Action<string, bool> report)
        {
            try
            {
                Rlist r = File.Exists(path) ? Rlist.Load(path) : new Rlist();
                r.Path = path;
                if (string.IsNullOrEmpty(r.Name)) r.Name = Path.GetFileNameWithoutExtension(path);
                bool added = r.Add(url, name);
                r.Save();
                Ui.Log.Info((added ? "Added to " : "Already in ") + Path.GetFileName(path) + ": " + url, "Rlist");
                report(Lang.L(added ? "Added to" : "Already in") + " " + Path.GetFileName(path), true);
            }
            catch (Exception ex) { report("Could not save the Rlist: " + ex.Message, false); }
        }

        // ================= per-file progress (IDM style) =================
        public static void OpenProgress(RockDownload it)
        {
            DownloadsPage page = DownloadsPage.Instance;
            if (page == null) return;
            Window old;
            if (ProgWins.TryGetValue(it.Id, out old)) { Front(old); return; }
            Window w = Load("ui/DlProgress.xaml");
            ProgWins[it.Id] = w;
            TextBlock txtTitle = Find<TextBlock>(w, "txtTitle"), txtState = Find<TextBlock>(w, "txtState"), vUrl = Find<TextBlock>(w, "vUrl"), vStatus = Find<TextBlock>(w, "vStatus"), vSize = Find<TextBlock>(w, "vSize");
            TextBlock vDone = Find<TextBlock>(w, "vDone"), vRate = Find<TextBlock>(w, "vRate"), vEta = Find<TextBlock>(w, "vEta"), vResume = Find<TextBlock>(w, "vResume"), barText = Find<TextBlock>(w, "barText"), txtPath = Find<TextBlock>(w, "txtPath");
            Ellipse dot = Find<Ellipse>(w, "dot");
            ProgressBar bar = Find<ProgressBar>(w, "bar");
            Canvas cnv = Find<Canvas>(w, "cnv");
            ItemsControl lst = Find<ItemsControl>(w, "lstConn");
            Border cardInfo = Find<Border>(w, "cardInfo");
            Grid gridConn = Find<Grid>(w, "gridConn");
            Panel pStatus = Find<Panel>(w, "pageStatus"), pLimit = Find<Panel>(w, "pageLimit"), pDone = Find<Panel>(w, "pageDone");
            RadioButton tStatus = Find<RadioButton>(w, "tabStatus"), tLimit = Find<RadioButton>(w, "tabLimit"), tDone = Find<RadioButton>(w, "tabDone");
            CheckBox chkLimit = Find<CheckBox>(w, "chkLimit"), chkOpenFile = Find<CheckBox>(w, "chkOpenFile"), chkOpenFolder = Find<CheckBox>(w, "chkOpenFolder"), chkClose = Find<CheckBox>(w, "chkClose");
            TextBox txtLimit = Find<TextBox>(w, "txtLimit");
            Button btnHide = Find<Button>(w, "btnHide"), btnPause = Find<Button>(w, "btnPause"), btnCancel = Find<Button>(w, "btnCancel");
            ObservableCollection<ConnRow> rows = new ObservableCollection<ConnRow>();
            lst.ItemsSource = rows;
            vUrl.Text = it.Url; txtTitle.Text = it.FileName; txtPath.Text = it.Dir; txtPath.ToolTip = it.FullPath;
            bool detailsHidden = false, finishedHandled = false, closing = false;
            string[] pal = new string[] { "#4DA3C4", "#4FB7A8", "#7C9CD8", "#E0B26B", "#D88C9A", "#52B788" };

            Action<Panel> showTab = delegate(Panel p)
            {
                pStatus.Visibility = p == pStatus ? Visibility.Visible : Visibility.Collapsed;
                pLimit.Visibility = p == pLimit ? Visibility.Visible : Visibility.Collapsed;
                pDone.Visibility = p == pDone ? Visibility.Visible : Visibility.Collapsed;
                Anim.PageAnim(p, 10);
            };
            tStatus.Checked += delegate { showTab(pStatus); };
            tLimit.Checked += delegate { showTab(pLimit); };
            tDone.Checked += delegate { showTab(pDone); };

            Action tick = delegate
            {
                string st = it.Status;
                IDlJob job = page.JobOf(it);
                txtTitle.Text = it.FileName;
                vStatus.Text = it.StatusText;
                txtState.Text = it.StatusText;
                vSize.Text = it.Total > 0 ? DownloadsPage.FormatSize(it.Total) + "  (" + it.Total.ToString("N0") + " bytes)" : Lang.L("Unknown");
                if (st != "Downloading")
                {
                    vDone.Text = DownloadsPage.FormatSize(it.Done) + (it.Total > 0 ? "  (" + it.PctText + ")" : "");
                    vRate.Text = it.SpeedText;
                }
                vEta.Text = it.EtaText;
                long[][] segs = job != null ? job.SegInfo() : new long[0][];
                vResume.Text = (segs.Length > 0 && it.Total > 0) || Aria.IsTorrentLink(it.Url) ? Lang.L("Yes") : (st == "Downloading" ? Lang.L("Checking...") : "-");
                if (st != "Downloading")
                {
                    barText.Text = it.Total > 0 ? it.PctText : DownloadsPage.FormatSize(it.Done);
                    bar.BeginAnimation(ProgressBar.ValueProperty, null); bar.Value = it.Progress;
                }
                // colour + pulse of the state dot
                string col = st == "Downloading" ? "#52B788" : (st == "Failed" || st == "Cancelled" ? "#E5827F" : (st == "Completed" ? "#4DA3C4" : "#E0A76B"));
                dot.Fill = DlTheme.Br(col);
                if (st == "Downloading")
                {
                    if (dot.Opacity == 1.0)
                    {
                        DoubleAnimation pl = new DoubleAnimation(1.0, 0.3, new Duration(TimeSpan.FromMilliseconds(700))); pl.AutoReverse = true; pl.RepeatBehavior = RepeatBehavior.Forever;
                        dot.BeginAnimation(UIElement.OpacityProperty, pl);
                    }
                }
                else { dot.BeginAnimation(UIElement.OpacityProperty, null); dot.Opacity = 1.0; }
                // connection positions bar + table
                if (!detailsHidden)
                {
                    cnv.Children.Clear();
                    double wid = cnv.ActualWidth;
                    if (segs.Length > 0 && it.Total > 0 && wid > 0)
                        for (int i = 0; i < segs.Length; i++)
                        {
                            double x = segs[i][0] * wid / it.Total, ww = Math.Max(0, (segs[i][2] - segs[i][0]) * wid / it.Total);
                            if (segs[i][3] == 1) ww = Math.Max(0, (segs[i][1] + 1 - segs[i][0]) * wid / it.Total);
                            Rectangle rc = new Rectangle(); rc.Height = 12; rc.Width = ww; rc.Fill = DlTheme.Br(pal[i % pal.Length]);
                            Canvas.SetLeft(rc, x); cnv.Children.Add(rc);
                        }
                    else if (it.Total > 0 && wid > 0)
                    { Rectangle rc = new Rectangle(); rc.Height = 12; rc.Width = wid * it.Progress / 100.0; rc.Fill = DlTheme.Br(pal[0]); cnv.Children.Add(rc); }
                    int n = segs.Length > 0 ? segs.Length : (st == "Downloading" ? 1 : 0);
                    while (rows.Count > n) rows.RemoveAt(rows.Count - 1);
                    while (rows.Count < n) rows.Add(new ConnRow());
                    for (int i = 0; i < n; i++)
                    {
                        rows[i].N = (i + 1).ToString();
                        if (segs.Length > 0)
                        {
                            long len = segs[i][1] - segs[i][0] + 1, got = segs[i][3] == 1 ? len : Math.Max(0, segs[i][2] - segs[i][0]);
                            rows[i].Downloaded = DownloadsPage.FormatLive(got) + " / " + DownloadsPage.FormatLive(len);
                            rows[i].Info = segs[i][3] == 1 ? Lang.L("Finished") : Lang.L("Receiving data...");
                        }
                        else { rows[i].Downloaded = DownloadsPage.FormatSize(it.Done); rows[i].Info = Lang.L("Receiving data..."); }
                    }
                }
                // speed limiter of this file
                if (job != null)
                {
                    long want = 0;
                    if (chkLimit.IsChecked == true) { long kb; if (long.TryParse(txtLimit.Text.Trim(), out kb) && kb > 0) want = kb * 1024; }
                    else want = page.CurrentLimit;
                    if (job.Limit != want) { try { job.Limit = want; } catch (Exception) { } }
                }
                // buttons
                if (st == "Completed")
                {
                    btnPause.Content = Lang.L("Open file"); btnCancel.Content = Lang.L("Close");
                    if (!finishedHandled) { finishedHandled = true; ((Action)w.Tag)(); }
                }
                else if (st == "Downloading" || st == "Queued") { btnPause.Content = Lang.L("Pause"); btnCancel.Content = Lang.L("Cancel"); }
                else if (st == "Cancelled") { if (!closing) { closing = true; w.Close(); } }
                else { btnPause.Content = Lang.L("Resume"); btnCancel.Content = Lang.L("Cancel"); }
            };
            // finished: do what "Options on completion" asks, close this window; the "Download complete" window opens next (DlWin.Finished)
            w.Tag = (Action)delegate
            {
                if (finishedHandled && closing) return;
                finishedHandled = true; closing = true;
                if (chkOpenFile.IsChecked == true) { try { Tool.Start(it.FullPath, ""); } catch (Exception) { } }
                if (chkOpenFolder.IsChecked == true) { try { Tool.Start("explorer.exe", "/select,\"" + it.FullPath + "\""); } catch (Exception) { } }
                try { w.Close(); } catch (Exception) { }
            };
            DispatcherTimer timer = new DispatcherTimer(); timer.Interval = TimeSpan.FromMilliseconds(120);
            timer.Tick += delegate { try { tick(); } catch (Exception) { } };
            // numbers of the status page count up smoothly (40 ms)
            DispatcherTimer fast = new DispatcherTimer(); fast.Interval = TimeSpan.FromMilliseconds(40);
            fast.Tick += delegate
            {
                try
                {
                    if (it.Status != "Downloading") return;
                    vDone.Text = DownloadsPage.FormatLive(it.SmDone) + (it.Total > 0 ? "  (" + it.PctText + ")" : "");
                    vRate.Text = it.SpeedText;
                    barText.Text = it.Total > 0 ? it.PctText : DownloadsPage.FormatLive(it.SmDone);
                    bar.Value = it.Progress;
                }
                catch (Exception) { }
            };
            btnPause.Click += delegate
            {
                string st = it.Status;
                if (st == "Completed") { try { Tool.Start(it.FullPath, ""); } catch (Exception ex) { Ui.Log.Warn("Could not open the file: " + ex.Message, "Downloads"); } }
                else if (st == "Downloading" || st == "Queued") page.PauseItem(it);
                else page.ResumeItem(it);
                tick();
            };
            btnCancel.Click += delegate
            {
                string st = it.Status;
                if (st == "Completed" || st == "Failed" || st == "Cancelled") { w.Close(); return; }
                page.CancelItem(it);
            };
            btnHide.Click += delegate
            {
                detailsHidden = !detailsHidden;
                btnHide.Content = Lang.L(detailsHidden ? "Show details" : "Hide details");
                if (detailsHidden) { cardInfo.Visibility = Visibility.Collapsed; gridConn.Visibility = Visibility.Collapsed; }
                else { cardInfo.Visibility = Visibility.Visible; gridConn.Visibility = Visibility.Visible; Anim.PageAnim(pStatus, 10); }
                DoubleAnimation ha = new DoubleAnimation(detailsHidden ? 176.0 : 440.0, new Duration(TimeSpan.FromMilliseconds(320)));
                CubicEase e2 = new CubicEase(); e2.EasingMode = EasingMode.EaseOut; ha.EasingFunction = e2;
                w.BeginAnimation(Window.HeightProperty, ha);
            };
            w.Closed += delegate { timer.Stop(); fast.Stop(); ProgWins.Remove(it.Id); };
            w.Loaded += delegate { tick(); timer.Start(); fast.Start(); };
            Front(w);
            w.Show();
            Front(w);
        }

        // ================= Grabber: every file link of a web page =================
        private static Window _grabWin;

        public static void OpenGrabber()
        {
            DownloadsPage page = DownloadsPage.Instance;
            if (page == null) return;
            if (_grabWin != null) { Front(_grabWin); return; }
            Window w = Load("ui/Grabber.xaml");
            _grabWin = w;
            TextBox txtPage = Find<TextBox>(w, "txtPage"), txtTypes = Find<TextBox>(w, "txtTypes");
            Button btnScan = Find<Button>(w, "btnScan"), btnAll = Find<Button>(w, "btnAll"), btnNone = Find<Button>(w, "btnNone"), btnGet = Find<Button>(w, "btnGet"), btnShut = Find<Button>(w, "btnShut");
            ItemsControl lst = Find<ItemsControl>(w, "lstLinks");
            TextBlock txtEmpty = Find<TextBlock>(w, "txtEmpty"), txtCount = Find<TextBlock>(w, "txtCount");
            ObservableCollection<GrabRow> rows = new ObservableCollection<GrabRow>();
            lst.ItemsSource = rows;
            try { string clip = (Clipboard.GetText() ?? "").Trim(); if (Regex.IsMatch(clip, @"^https?://\S+$")) txtPage.Text = clip; } catch (Exception) { }
            Action count = delegate { int n = 0; foreach (GrabRow r in rows) if (r.Checked) n++; txtCount.Text = rows.Count == 0 ? "" : n + " of " + rows.Count + " selected"; };
            bool busy = false;
            btnScan.Click += delegate
            {
                if (busy) return;
                string pageUrl = txtPage.Text.Trim();
                Uri baseUri;
                if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out baseUri) || (baseUri.Scheme != "http" && baseUri.Scheme != "https"))
                { txtEmpty.Text = "Enter a full page address that starts with http:// or https://"; txtEmpty.Visibility = Visibility.Visible; return; }
                List<string> types = new List<string>();
                foreach (string t in txtTypes.Text.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)) types.Add(t.Trim().TrimStart('.').ToLowerInvariant());
                busy = true; btnScan.IsEnabled = false; btnScan.Content = "Scanning...";
                rows.Clear(); txtEmpty.Text = "Reading the page..."; txtEmpty.Visibility = Visibility.Visible; count();
                ThreadPool.QueueUserWorkItem(delegate
                {
                    List<string> found = new List<string>(); string err = "";
                    try { found = GrabLinks(baseUri, types); }
                    catch (WebException ex) { err = Net.FriendlyError(ex); }
                    catch (Exception ex) { err = ex.Message; }
                    w.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        busy = false; btnScan.IsEnabled = true; btnScan.Content = "Scan";
                        foreach (string u in found) rows.Add(new GrabRow { Url = u });
                        txtEmpty.Text = err.Length > 0 ? "Could not read the page: " + err : "No download links found on this page.";
                        txtEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        foreach (GrabRow r in rows) r.PropertyChanged += delegate { count(); };
                        count();
                    }));
                });
            };
            btnAll.Click += delegate { foreach (GrabRow r in rows) r.Checked = true; count(); };
            btnNone.Click += delegate { foreach (GrabRow r in rows) r.Checked = false; count(); };
            btnGet.Click += delegate
            {
                List<string> pick = new List<string>();
                foreach (GrabRow r in rows) if (r.Checked) pick.Add(r.Url);
                if (pick.Count == 0) return;
                page.AddItems(pick.ToArray(), page.DlDir, "", true, true, false, false);
                w.Close();
            };
            btnShut.Click += delegate { w.Close(); };
            txtPage.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) btnScan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            w.Closed += delegate { _grabWin = null; };
            w.Loaded += delegate { txtPage.Focus(); };
            Front(w);
            w.Show();
            Front(w);
        }

        // reads a page and returns the file links on it (filtered by file types, or "anything that looks like a file")
        private static List<string> GrabLinks(Uri pageUri, List<string> types)
        {
            HttpWebRequest q = (HttpWebRequest)WebRequest.Create(pageUri);
            q.Timeout = 20000; q.ReadWriteTimeout = 20000; q.AllowAutoRedirect = true;
            q.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RDownloader";
            string html;
            using (HttpWebResponse r = (HttpWebResponse)q.GetResponse())
            using (Stream s = r.GetResponseStream())
            {
                byte[] buf = new byte[65536]; MemoryStream ms = new MemoryStream(); int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0 && ms.Length < 6 * 1024 * 1024) ms.Write(buf, 0, n);
                html = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
            string[] skip = new string[] { "html", "htm", "php", "asp", "aspx", "jsp", "js", "css", "ico", "json", "xml", "map", "txt" };
            List<string> res = new List<string>(); HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(html, "(?:href|src|data-src|data-url)\\s*=\\s*[\"']([^\"'#\\s]+)[\"']", RegexOptions.IgnoreCase))
            {
                Uri u;
                string raw = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
                if (!Uri.TryCreate(pageUri, raw, out u) || (u.Scheme != "http" && u.Scheme != "https")) continue;
                string ext = Path.GetExtension(u.AbsolutePath).TrimStart('.').ToLowerInvariant();
                if (types.Count > 0) { if (!types.Contains(ext)) continue; }
                else if (ext.Length == 0 || ext.Length > 5 || Array.IndexOf(skip, ext) >= 0) continue;
                if (u.Scheme == "http" || u.Scheme == "https") { string abs = u.AbsoluteUri; if (seen.Add(abs)) res.Add(abs); }
            }
            return res;
        }

        // a download finished: close its progress window and open the "Download complete" window (like IDM)
        public static void Finished(RockDownload it)
        {
            Window w;
            if (!ProgWins.TryGetValue(it.Id, out w)) return;
            Action a = w.Tag as Action;
            if (a != null) a(); else { try { w.Close(); } catch (Exception) { } }
            OpenComplete(it);
        }

        private static readonly Dictionary<string, Window> DoneWins = new Dictionary<string, Window>();

        public static void OpenComplete(RockDownload it)
        {
            Window old;
            if (DoneWins.TryGetValue(it.Id, out old)) { Front(old); return; }
            Window w = Load("ui/DlDone.xaml");
            DoneWins[it.Id] = w;
            TextBlock vName = Find<TextBlock>(w, "vName"), vSize = Find<TextBlock>(w, "vSize"), vPath = Find<TextBlock>(w, "vPath"), vUrl = Find<TextBlock>(w, "vUrl");
            Button bOpen = Find<Button>(w, "btnOpen"), bFolder = Find<Button>(w, "btnFolder"), bDone = Find<Button>(w, "btnDone");
            ScaleTransform sc = w.FindName("badgeScale") as ScaleTransform;
            FrameworkElement hdr = Find<FrameworkElement>(w, "hdr"), card = Find<FrameworkElement>(w, "card"), name = vName;
            vName.Text = it.FileName; vName.ToolTip = it.FullPath;
            long len = 0; try { len = new FileInfo(it.FullPath).Length; } catch (Exception) { }
            vSize.Text = len > 0 ? DownloadsPage.FormatSize(len) + "  (" + len.ToString("N0") + " bytes)" : it.SizeText;
            vPath.Text = it.Dir; vPath.ToolTip = it.Dir;
            vUrl.Text = it.Url; vUrl.ToolTip = it.Url;
            bOpen.Click += delegate { try { Tool.Start(it.FullPath, ""); } catch (Exception ex) { Ui.Log.Warn("Could not open the file: " + ex.Message, "Downloads"); } w.Close(); };
            bFolder.Click += delegate { try { Tool.Start("explorer.exe", "/select,\"" + it.FullPath + "\""); } catch (Exception) { } w.Close(); };
            bDone.Click += delegate { w.Close(); };
            w.Closed += delegate { DoneWins.Remove(it.Id); };
            w.Loaded += delegate
            {
                try
                {
                    if (sc != null)
                    {
                        BackEase be = new BackEase(); be.EasingMode = EasingMode.EaseOut; be.Amplitude = 0.7;
                        DoubleAnimation pop = new DoubleAnimation(0.2, 1.0, Fx.Dur(520)); pop.EasingFunction = be;
                        sc.BeginAnimation(ScaleTransform.ScaleXProperty, pop); sc.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                    }
                    if (hdr != null) Fx.FadeUp(hdr, 120, 8);
                    if (name != null) Fx.FadeUp(name, 190, 8);
                    if (card != null) Fx.FadeUp(card, 260, 10);
                }
                catch (Exception) { }
            };
            Front(w);
            w.Show();
            Front(w);
        }

        // ================= Rlist (playlist) =================
        public static void OpenRlist(string path)
        {
            DownloadsPage page = DownloadsPage.Instance;
            if (page == null) return;
            Window old;
            if (ListWins.TryGetValue(path, out old)) { Front(old); return; }
            Rlist rl;
            try { rl = Rlist.Load(path); }
            catch (Exception ex) { Dlg.Error("Could not read this Rlist file\n\n" + ex.Message); return; }
            Rlist.Remember(path);
            if (rl.Entries.Count == 0) { Dlg.Info("This Rlist is empty\n\nUse \"Add to Rlist\" in the Download File Info window to put links in it."); return; }
            Window w;
            try { w = Load("ui/RlistWin.xaml"); }
            catch (Exception ex) { Ui.Log.Error("Could not open the Rlist window: " + ex.Message, "Rlist"); return; }
            ListWins[path] = w;
            TextBlock txtName = Find<TextBlock>(w, "txtName"), txtSub = Find<TextBlock>(w, "txtSub"), txtDone = Find<TextBlock>(w, "txtDone"), txtPath = Find<TextBlock>(w, "txtPath");
            ProgressBar barAll = Find<ProgressBar>(w, "barAll");
            Border pill = Find<Border>(w, "pill");
            ItemsControl rows = Find<ItemsControl>(w, "lstRows");
            Button btnNow = Find<Button>(w, "btnNow"), btnLater = Find<Button>(w, "btnLater"), btnCancel = Find<Button>(w, "btnCancel");
            txtName.Text = rl.Name; txtPath.Text = path; txtPath.ToolTip = path;
            string dir = string.IsNullOrEmpty(rl.Dir) ? page.DlDir : rl.Dir;
            List<RockDownload> items = new List<RockDownload>();
            foreach (RlistEntry e in rl.Entries)
            {
                RockDownload it = page.CreateItem(e.Url, dir, e.Name, true, false, false);
                if (it == null) continue;
                it.StatusText = Lang.L("Ready to download"); it.PctText = "0%"; it.ProgText = "";
                items.Add(it);
            }
            if (items.Count == 0) { Dlg.Warn("No usable links in this Rlist\n\nOnly http, https and magnet links can be downloaded."); ListWins.Remove(path); w.Close(); return; }
            rows.ItemsSource = items;
            bool adopted = false, allDone = false, paused = false, finishedAnnounced = false;
            Action header = delegate
            {
                long total = 0; int unknown = 0, done = 0; double sum = 0;
                foreach (RockDownload d in items)
                {
                    if (d.Total > 0) total += d.Total; else unknown++;
                    if (d.Status == "Completed") done++;
                    sum += d.Progress;
                }
                string sz = total > 0 ? DownloadsPage.FormatSize(total) + (unknown > 0 ? "+" : "") : Lang.L("size unknown");
                txtSub.Text = items.Count + " " + Lang.L(items.Count == 1 ? "file" : "files") + "  ·  " + sz;
                txtDone.Text = done + " / " + items.Count + " " + Lang.L("done");
                double pct = items.Count > 0 ? sum / items.Count : 0;
                DoubleAnimation ba = new DoubleAnimation(pct, new Duration(TimeSpan.FromMilliseconds(420)));
                barAll.BeginAnimation(ProgressBar.ValueProperty, ba);
                allDone = adopted && done == items.Count;
                if (allDone && !finishedAnnounced)
                {
                    finishedAnnounced = true;
                    pill.Background = DlTheme.Br("#1F3A33"); pill.BorderBrush = DlTheme.Br("#52B788");
                    btnNow.Content = Lang.L("Open folder"); btnLater.Content = Lang.L("Close"); btnCancel.Visibility = Visibility.Collapsed;
                    Anim.PopIn(pill, 0.5, 0.0);
                    Ui.Log.Info("Rlist finished: " + rl.Name, "Rlist");
                }
            };
            // sizes and names of every file, one by one in the background
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (RockDownload d in items)
                {
                    RockDownload dd = d;
                    ProbeResult r = DlProbe.Run(dd.Url, "", "");
                    Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (dd.Status != "Paused" || adopted) { if (adopted) return; }
                        if (r.Size > 0) { dd.Total = r.Size; dd.SizeText = DownloadsPage.FormatSize(r.Size); dd.ProgText = "0 B / " + DownloadsPage.FormatSize(r.Size); }
                        else if (r.Mime == "torrent") dd.ProgText = Lang.L("Torrent");
                        if (dd.NameFromServer && !string.IsNullOrEmpty(r.Name)) page.Rename(dd, r.Name);
                        header();
                    }));
                }
            });
            btnNow.Click += delegate
            {
                if (!adopted)
                {
                    adopted = true;
                    foreach (RockDownload d in items) page.Adopt(d, true, false);
                    btnNow.Content = Lang.L("Pause all"); btnLater.Content = Lang.L("Hide"); btnCancel.Content = Lang.L("Cancel all");
                    header(); return;
                }
                if (allDone) { try { Directory.CreateDirectory(dir); Tool.Start("explorer.exe", "\"" + dir + "\""); } catch (Exception) { } return; }
                paused = !paused;
                foreach (RockDownload d in items) { if (paused) page.PauseItem(d); else page.ResumeItem(d); }
                btnNow.Content = Lang.L(paused ? "Resume all" : "Pause all");
            };
            btnLater.Click += delegate
            {
                if (!adopted)
                {
                    adopted = true;
                    foreach (RockDownload d in items) page.Adopt(d, false, false);
                    Ui.Log.Info(items.Count + " file(s) from " + rl.Name + " were added to the Downloads page (paused).", "Rlist");
                }
                w.Close();
            };
            btnCancel.Click += delegate
            {
                if (!adopted) { w.Close(); return; }
                foreach (RockDownload d in items) if (d.Status != "Completed") page.CancelItem(d);
                w.Close();
            };
            rows.MouseDoubleClick += delegate(object s, System.Windows.Input.MouseButtonEventArgs e)
            {
                FrameworkElement fe = e.OriginalSource as FrameworkElement;
                RockDownload d = fe != null ? fe.DataContext as RockDownload : null;
                if (d == null || !adopted) return;
                if (d.Status == "Completed") { try { Tool.Start("explorer.exe", "/select,\"" + d.FullPath + "\""); } catch (Exception) { } }
                else DlWin.OpenProgress(d);
            };
            DispatcherTimer timer = new DispatcherTimer(); timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += delegate { try { header(); } catch (Exception) { } };
            w.Closed += delegate { timer.Stop(); ListWins.Remove(path); };
            w.Loaded += delegate { header(); timer.Start(); };
            w.Show();
            Front(w);
        }
    }
}
