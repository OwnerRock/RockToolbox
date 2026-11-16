using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    // C# port of src\pages\Install.ps1 (core): app catalog with categories + search, install / uninstall / upgrade with winget
    // (or Chocolatey when installed), custom package ID, "show installed". Icons/animations of the old page are not ported.
    internal sealed partial class InstallPage : IPage
    {
        private sealed class Card { public AppEntry App; public Border Box; public TextBlock Badge; public Panel Section; public Image Icon; public Border Tile; public Border Check; public TextBlock Tick; public ScaleTransform Scale; }
        private readonly Queue<Card> _iconQueue = new Queue<Card>();
        private DispatcherTimer _iconTimer;

        private static readonly int[] OkCodes = { 0, 3010, 1641, -1978334967, -1978334966, -1978334963 };
        private static readonly int[] RestartCodes = { 3010, 1641, -1978334967, -1978334966 };
        private static readonly int[] FinalCodes = { -1978335212, -1978335216, -1978335215, -1978335224, -1978334964, -1978334969, -1978334971, -1978334961, -1978334957 };
        private static readonly int[] NoFallback = { -1978335224, -1978334964, -1978334969, -1978334971, -1978334961 };

        private readonly Dictionary<string, bool> _selected = new Dictionary<string, bool>();
        private readonly List<Card> _cards = new List<Card>();
        private readonly Dictionary<string, FrameworkElement> _sectionByCat = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, Panel> _wrapByCat = new Dictionary<string, Panel>();
        private readonly Dictionary<string, TextBlock> _chevByCat = new Dictionary<string, TextBlock>();
        private StackPanel _apps;
        private WrapPanel _chips;
        private TextBox _search, _custom;
        private TextBlock _count;
        private RadioButton _choco;
        private DispatcherTimer _debounce;
        private string _cat = "";
        private bool _built;

        public static InstallPage Instance;
        public string Key { get { return "Install"; } }
        public bool UseChocoPublic { get { return UseChoco; } }
        public TextBox CustomBox { get { return _custom; } }
        public RadioButton ChocoRadio { get { return _choco; } }
        public RadioButton WingetRadio { get { return Ui.Find<RadioButton>("rdPkgWinget"); } }

        private bool UseChoco { get { return _choco.IsChecked == true; } }

        public void Init(Window w, Logger log)
        {
            Instance = this;
            _apps = Ui.Find<StackPanel>("appsPanel");
            _chips = Ui.Find<WrapPanel>("chipPanel");
            _search = Ui.Find<TextBox>("txtAppSearch");
            _custom = Ui.Find<TextBox>("txtCustomId");
            _count = Ui.Find<TextBlock>("txtSelCount");
            _choco = Ui.Find<RadioButton>("rdPkgChoco");
            RadioButton wg = Ui.Find<RadioButton>("rdPkgWinget");
            if (Ui.Settings.GetString("PkgManager") == "Chocolatey") _choco.IsChecked = true; else wg.IsChecked = true;
            RoutedEventHandler save = delegate { Ui.Settings.Set("PkgManager", UseChoco ? "Chocolatey" : "WinGet"); Ui.Settings.SaveSoon(); };
            _choco.Click += save; wg.Click += save;

            _debounce = new DispatcherTimer(); _debounce.Interval = TimeSpan.FromMilliseconds(250);
            _debounce.Tick += delegate { _debounce.Stop(); ApplyFilter(); };
            _search.TextChanged += delegate { _debounce.Stop(); _debounce.Start(); };

            Ui.Find<Button>("btnInstall").Click += delegate { RunPkg("install"); };
            Ui.Find<Button>("btnUninstall").Click += delegate { RunPkg("uninstall"); };
            Ui.Find<Button>("btnUpgrade").Click += delegate { UpgradeAll(); };
            Ui.Find<Button>("btnStopAction").Click += delegate { Work.RequestCancel(); };
            Ui.Find<Button>("btnAppsClear").Click += delegate
            {
                _selected.Clear();
                foreach (Card c in _cards) Look(c);
                UpdateCount();
            };
            Ui.Find<Button>("btnCollapse").Click += delegate { EnsureBuilt(); foreach (string k in new List<string>(_wrapByCat.Keys)) SetOpen(k, false, false); };
            Ui.Find<Button>("btnExpand").Click += delegate { EnsureBuilt(); foreach (string k in new List<string>(_wrapByCat.Keys)) SetOpen(k, true, false); };
            Ui.Find<Button>("btnInstallCustom").Click += delegate
            {
                string id = _custom.Text.Trim();
                if (id.Length == 0) { Ui.Log.Warn(UseChoco ? "Type a Chocolatey package ID first (example: steam)." : "Type a winget ID first (example: Valve.Steam).", "Install"); return; }
                Run("install", new List<string> { id });
            };
            Ui.Find<Button>("btnShowInstalled").Click += delegate { ShowInstalled(); };
            if (Theme.Modern) ModernLayout();      // Modern look: "Your list" panel on the right, see InstallModern.cs
        }

        public void OnShown() { if (!_built) Build(); }
        public void EnsureBuilt() { if (!_built) Build(); }

        public List<string> SelectedIds() { return new List<string>(_selected.Keys); }

        public void SetSelected(List<string> ids)
        {
            EnsureBuilt(); _selected.Clear();
            foreach (string id in ids) _selected[id] = true;
            foreach (Card c in _cards) Look(c);
            UpdateCount();
        }

        public static List<string> InstalledNameList() { return InstalledNames(); }

        private void Build()
        {
            _built = true;
            AddChip(Lang.L("All"), "");
            foreach (string cat in AppData.Cats) AddChip(Lang.L(cat), cat);
            bool anim = Ui.Settings.GetBool("CardAnim", true);
            foreach (string cat in AppData.Cats)
            {
                StackPanel section = new StackPanel(); section.Margin = new Thickness(0, 4, 0, 4);
                // header: chevron, category name, number of apps - click to fold the group
                int count = 0; foreach (AppEntry ae in AppData.All) if (ae.Cat == cat) count++;
                Border head = new Border(); head.Cursor = Cursors.Hand; head.Background = Brushes.Transparent; head.Padding = new Thickness(4, 8, 4, 8); head.CornerRadius = new CornerRadius(8);
                DockPanel hp = new DockPanel();
                TextBlock chev = new TextBlock(); chev.FontFamily = new FontFamily("Segoe MDL2 Assets"); chev.Text = ""; chev.FontSize = 11; chev.Width = 22; chev.VerticalAlignment = VerticalAlignment.Center; chev.Foreground = Ui.Brush("#4FD1FF");
                TextBlock title = new TextBlock(); title.Text = Lang.L(cat); title.FontSize = 14.5; title.FontWeight = FontWeights.Bold; title.Foreground = Ui.Brush("#F2F4FF"); title.VerticalAlignment = VerticalAlignment.Center;
                Border pill = new Border(); pill.CornerRadius = new CornerRadius(9); pill.Padding = new Thickness(8, 1, 8, 2); pill.Margin = new Thickness(10, 0, 0, 0); pill.Background = Ui.Brush("#262B33"); pill.VerticalAlignment = VerticalAlignment.Center;
                TextBlock pt = new TextBlock(); pt.Text = "" + count; pt.FontSize = 11; pt.Foreground = Ui.Brush("#9AA0B8"); pill.Child = pt;
                hp.Children.Add(chev); hp.Children.Add(title); hp.Children.Add(pill); head.Child = hp;
                WrapPanel wrap = new WrapPanel(); wrap.Margin = new Thickness(0, 2, 0, 4);
                string c0 = cat;
                head.MouseEnter += delegate { head.Background = Ui.Brush("#262B33"); };
                head.MouseLeave += delegate { head.Background = Brushes.Transparent; };
                head.MouseLeftButtonUp += delegate { SetOpen(c0, wrap.Visibility != Visibility.Visible, true); };
                section.Children.Add(head); section.Children.Add(wrap);
                _apps.Children.Add(section);
                _sectionByCat[cat] = section; _wrapByCat[cat] = wrap; _chevByCat[cat] = chev;
                foreach (AppEntry a in AppData.All)
                {
                    if (a.Cat != cat) continue;
                    Card card = new Card(); card.App = a; card.Section = wrap;
                    Border b = new Border(); b.Width = 214; b.Height = 54; b.Margin = new Thickness(0, 0, 8, 8); b.Padding = new Thickness(10, 0, 10, 0);
                    b.CornerRadius = new CornerRadius(12); b.BorderThickness = new Thickness(1); b.Cursor = Cursors.Hand; b.ToolTip = a.Id;
                    b.RenderTransformOrigin = new Point(0.5, 0.5); ScaleTransform st = new ScaleTransform(1, 1); b.RenderTransform = st;
                    DockPanel dp = new DockPanel(); dp.VerticalAlignment = VerticalAlignment.Center;
                    // icon: the site icon when it is available, a coloured letter tile until then (or when there is no internet)
                    Grid ih = new Grid(); ih.Width = 32; ih.Height = 32; ih.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(ih, Dock.Left);
                    Border tile = new Border(); tile.CornerRadius = new CornerRadius(9); tile.Background = TileBrush(a.Name);
                    TextBlock letter = new TextBlock(); letter.Text = a.Name.Length > 0 ? a.Name.Substring(0, 1).ToUpperInvariant() : "?"; letter.FontWeight = FontWeights.Bold; letter.FontSize = 15; letter.Foreground = Brushes.White; letter.HorizontalAlignment = HorizontalAlignment.Center; letter.VerticalAlignment = VerticalAlignment.Center;
                    tile.Child = letter;
                    Image icon = new Image(); icon.Margin = new Thickness(5); icon.Stretch = Stretch.Uniform; icon.Visibility = Visibility.Collapsed;
                    RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
                    ih.Children.Add(tile); ih.Children.Add(icon);
                    dp.Children.Add(ih);
                    // right side: installed mark + selection circle
                    StackPanel right = new StackPanel(); right.Orientation = Orientation.Horizontal; DockPanel.SetDock(right, Dock.Right);
                    TextBlock badge = new TextBlock(); badge.Text = "✔"; badge.Foreground = Ui.Brush("#3DDC84"); badge.Visibility = Visibility.Collapsed; badge.Margin = new Thickness(0, 0, 8, 0); badge.VerticalAlignment = VerticalAlignment.Center; badge.ToolTip = "Installed";
                    Border chk = new Border(); chk.Width = 20; chk.Height = 20; chk.CornerRadius = new CornerRadius(10); chk.BorderThickness = new Thickness(1.5); chk.VerticalAlignment = VerticalAlignment.Center;
                    chk.RenderTransformOrigin = new Point(0.5, 0.5); chk.RenderTransform = new ScaleTransform(1, 1);
                    TextBlock tick = new TextBlock(); tick.Text = "✓"; tick.FontSize = 12; tick.FontWeight = FontWeights.Bold; tick.Foreground = Brushes.White; tick.HorizontalAlignment = HorizontalAlignment.Center; tick.VerticalAlignment = VerticalAlignment.Center; tick.Margin = new Thickness(0, -1, 0, 0);
                    chk.Child = tick;
                    right.Children.Add(badge); right.Children.Add(chk);
                    dp.Children.Add(right);
                    TextBlock name = new TextBlock(); name.Text = a.Name; name.FontWeight = FontWeights.SemiBold; name.FontSize = 13.5; name.Foreground = Ui.Brush("#F2F4FF"); name.TextTrimming = TextTrimming.CharacterEllipsis; name.VerticalAlignment = VerticalAlignment.Center;
                    dp.Children.Add(name);
                    b.Child = dp;
                    card.Box = b; card.Badge = badge; card.Icon = icon; card.Tile = tile; card.Check = chk; card.Tick = tick; card.Scale = st;
                    b.MouseLeftButtonUp += delegate
                    {
                        if (_selected.ContainsKey(card.App.Id)) _selected.Remove(card.App.Id); else _selected[card.App.Id] = true;
                        Look(card, true); UpdateCount();
                    };
                    Look(card, false);
                    if (anim)
                    {
                        b.MouseEnter += delegate { Scale(card.Scale, 1.035, 130); if (!_selected.ContainsKey(card.App.Id)) b.Background = Ui.Brush("#2B3038"); };
                        b.MouseLeave += delegate { Scale(card.Scale, 1.0, 180); Look(card, false); };
                    }
                    if (!string.IsNullOrEmpty(a.Dom)) _iconQueue.Enqueue(card);
                    wrap.Children.Add(b);
                    _cards.Add(card);
                }
            }
            UpdateChips();
            UpdateCount();
            if (Theme.Modern) ResizeCards();
            StartIcons();
            new InstallExtras().Init();
        }

        private static readonly string[][] TileColors = {
            new[] { "#FF9F0A", "#FF375F" }, new[] { "#2F8BFF", "#5AC8FA" }, new[] { "#BF5AF2", "#FF375F" }, new[] { "#30B96B", "#2DD4BF" },
            new[] { "#FF6B6B", "#FFB454" }, new[] { "#5E5CE6", "#BF5AF2" }, new[] { "#0A84FF", "#5E5CE6" }, new[] { "#FF6BC1", "#FF9F0A" } };

        private static Brush TileBrush(string name)
        {
            int h = 7; foreach (char ch in name) h = h * 31 + ch;
            string[] c = TileColors[(h & 0x7FFFFFFF) % TileColors.Length];
            LinearGradientBrush g = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(c[0]), (Color)ColorConverter.ConvertFromString(c[1]), 45);
            g.Freeze();
            return g;
        }

        private static void Scale(ScaleTransform st, double to, int ms)
        {
            try
            {
                System.Windows.Media.Animation.DoubleAnimation a = new System.Windows.Media.Animation.DoubleAnimation(to, new Duration(TimeSpan.FromMilliseconds(ms)));
                System.Windows.Media.Animation.CubicEase e = new System.Windows.Media.Animation.CubicEase(); e.EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut; a.EasingFunction = e;
                st.BeginAnimation(ScaleTransform.ScaleXProperty, a); st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
            catch { st.ScaleX = to; st.ScaleY = to; }
        }

        // fold / unfold one category (header chevron + the apps below it)
        private void SetOpen(string cat, bool open, bool animate)
        {
            Panel wrap; TextBlock chev;
            if (!_wrapByCat.TryGetValue(cat, out wrap) || !_chevByCat.TryGetValue(cat, out chev)) return;
            wrap.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            chev.Text = open ? "" : "";
            if (open && animate) Anim.PageAnim(wrap, 10);
        }

        // app icons (site favicons) are downloaded in the background and cached on disk - a few at a time so the UI stays smooth.
        // Several icon servers are tried; when none can be reached the letter tile stays.
        private void StartIcons()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RockToolbox\icons");
            try { Directory.CreateDirectory(dir); } catch { }
            _iconTimer = new DispatcherTimer(); _iconTimer.Interval = TimeSpan.FromMilliseconds(120);
            _iconTimer.Tick += delegate
            {
                for (int n = 0; n < 4 && _iconQueue.Count > 0; n++)
                {
                    Card c = _iconQueue.Dequeue();
                    string file = Path.Combine(dir, c.App.Dom.Replace("/", "_") + ".png");
                    Card cc = c;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            BitmapImage bi = LoadIcon(file);
                            if (bi == null)
                            {
                                if (_iconsOffline) return;
                                string d = cc.App.Dom;
                                string[] urls = { "https://www.google.com/s2/favicons?domain=" + d + "&sz=64", "https://icons.duckduckgo.com/ip3/" + d + ".ico", "https://favicon.im/" + d + "?larger=true" };
                                int fails = 0;
                                foreach (string u in urls)
                                {
                                    try { Net.Download(u, file, null, null); bi = LoadIcon(file); if (bi != null) break; }
                                    catch { fails++; }
                                    try { if (File.Exists(file)) File.Delete(file); } catch { }
                                }
                                if (bi == null && fails == urls.Length) _iconsOffline = true;      // no server could be reached: stop trying for this session
                            }
                            if (bi == null) return;
                            BitmapImage show = bi;
                            Ui.Post(delegate
                            {
                                cc.Icon.Source = show; cc.Icon.Visibility = Visibility.Visible;
                                cc.Tile.Background = Ui.Brush("#FFFFFF"); ((UIElement)cc.Tile.Child).Visibility = Visibility.Collapsed;
                                cc.Icon.Opacity = 0; cc.Icon.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(260))));
                                try { RefreshList(); } catch { }
                            });
                        }
                        catch { }
                    });
                }
                if (_iconQueue.Count == 0) _iconTimer.Stop();
            };
            _iconTimer.Start();
        }

        private static bool _iconsOffline;

        private static BitmapImage LoadIcon(string file)
        {
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length < 64) return null;
                BitmapImage bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(file); bi.DecodePixelWidth = 48; bi.EndInit(); bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        private void AddChip(string text, string cat)
        {
            Button b = new Button(); b.Content = text; b.Tag = cat;
            try { b.Style = (Style)Ui.Win.FindResource("ChipBtn"); } catch { b.Margin = new Thickness(0, 0, 6, 6); b.Padding = new Thickness(12, 4, 12, 4); }
            b.Click += delegate { _cat = (string)((Button)b).Tag; ApplyFilter(); UpdateChips(); };
            _chips.Children.Add(b);
        }

        private void UpdateChips()
        {
            foreach (UIElement u in _chips.Children)
            {
                Button b = u as Button; if (b == null) continue;
                System.Windows.Controls.Primitives.Selector.SetIsSelected(b, (string)b.Tag == _cat);
            }
        }

        private void Look(Card c) { Look(c, false); }

        private void Look(Card c, bool animate)
        {
            bool on = _selected.ContainsKey(c.App.Id);
            c.Box.Background = Ui.Brush(on ? "#1E3547" : "#262B33");
            c.Box.BorderBrush = Ui.Brush(on ? "#2F6FB0" : "#2B3038");
            c.Check.Background = on ? Ui.Brush("#2F6FB0") : Brushes.Transparent;
            c.Check.BorderBrush = on ? Ui.Brush("#2F6FB0") : Ui.Brush("#6B7186");
            c.Tick.Opacity = on ? 1 : 0;
            if (animate)
            {
                ScaleTransform st = c.Check.RenderTransform as ScaleTransform;
                if (st != null && on)
                {
                    System.Windows.Media.Animation.BackEase be = new System.Windows.Media.Animation.BackEase(); be.EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut; be.Amplitude = 0.9;
                    System.Windows.Media.Animation.DoubleAnimation a = new System.Windows.Media.Animation.DoubleAnimation(0.5, 1.0, new Duration(TimeSpan.FromMilliseconds(340))); a.EasingFunction = be;
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, a); st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                }
            }
        }

        private void UpdateCount() { _count.Text = Lang.L("Selected Apps") + ": " + _selected.Count; RefreshList(); }

        private void ApplyFilter()
        {
            string q = (_search.Text ?? "").Trim();
            foreach (Card c in _cards)
            {
                bool vis = (_cat.Length == 0 || c.App.Cat == _cat) &&
                    (q.Length == 0 || c.App.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || c.App.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                c.Box.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            }
            foreach (KeyValuePair<string, FrameworkElement> kv in _sectionByCat)
            {
                bool any = _cards.Exists(delegate(Card c) { return c.App.Cat == kv.Key && c.Box.Visibility == Visibility.Visible; });
                kv.Value.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ---------------- package manager helpers ----------------
        private static string FindChoco()
        {
            string p = Path.Combine(Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData", @"chocolatey\bin\choco.exe");
            return File.Exists(p) ? p : null;
        }

        private static string ChocoId(string id)
        {
            string s = id.Trim();
            if (Regex.IsMatch(s, "^python\\.python\\.", RegexOptions.IgnoreCase)) return "python";
            if (s.Contains(".")) { string[] parts = s.Split('.'); s = parts[parts.Length - 1]; }
            return Regex.Replace(s, "[^A-Za-z0-9._-]", "").ToLowerInvariant();
        }

        private static string WingetMessage(int code)
        {
            switch (code)
            {
                case 0: return "OK";
                case -1978335189: return "already installed (no newer version available)";
                case -1978335212: return "package not found in the winget source";
                case -1978335216: return "no applicable installer for this PC";
                case -1978335215: return "installer hash mismatch";
                case -1978335224: return "download failed (check your internet)";
                case 3010: case 1641: case -1978334967: case -1978334966: return "installed - restart required";
                case -1978334963: return "already installed";
                case -1978334964: return "installer was cancelled";
                case -1978334969: return "no network connection during install";
                case -1978334971: return "not enough disk space";
                case -1978334972: return "a required dependency is missing";
                case -1978334975: case -1978334959: return "the app is currently running - close it and try again";
                case -1978334961: return "blocked by a policy on this PC";
                case -1978334957: return "not supported on this system";
            }
            return "finished with exit code " + code;
        }

        internal static string EnsureWinget(WorkCtx c)
        {
            string wg = Removers.FindWinget();
            if (wg != null) return wg;
            c.Info("winget was not found - trying to register App Installer...", "Winget");
            try { Appx.RegisterByFamily("Microsoft.DesktopAppInstaller_8wekyb3d8bbwe"); } catch (Exception ex) { c.Warn("  " + ex.Message, "Winget"); }
            wg = Removers.FindWinget();
            if (wg == null) wg = PkgBootstrap.InstallWinget(c);
            if (wg == null) c.Warn("winget could not be installed automatically. Install \"App Installer\" from the Microsoft Store, then try again.", "Winget");
            return wg;
        }

        private static string EnsureChoco(WorkCtx c)
        {
            string ch = FindChoco();
            if (ch == null) ch = PkgBootstrap.InstallChoco(c);
            if (ch == null) c.Warn("Chocolatey (choco) could not be installed. Install it from chocolatey.org, or switch back to WinGet.", "Pkg");
            return ch;
        }

        private static void Pump(WorkCtx c, string tag, string text)
        {
            foreach (string l in Regex.Split(text, "[\r\n]+"))
            {
                string s = l.Trim();
                if (s.Length < 3 || Regex.IsMatch(s, @"^[\-\\|/\s\u2588\u2592\u2591]+$")) continue;
                if (Regex.IsMatch(s, @"^\d+(\.\d+)?\s*(KB|MB|GB)\s*/")) continue;
                if (s.StartsWith("Progress:", StringComparison.OrdinalIgnoreCase)) continue;
                if (Regex.IsMatch(s, @"^[\u2588\u2592\u2591\s\d\.]+(KB|MB|GB)?\s*/?\s*[\d\.]*\s*(KB|MB|GB)?$") && s.IndexOf('\u2588') >= 0) continue;
                c.Info("  " + s, tag);
            }
        }

        // ---------------- actions ----------------
        private void RunPkg(string verb)
        {
            List<string> ids = new List<string>(_selected.Keys);
            if (ids.Count == 0) { Ui.Log.Warn("No apps selected.", UseChoco ? "Pkg" : "Winget"); return; }
            Run(verb, ids);
        }

        private void Run(string verb, List<string> ids) { RunQueue(verb, ids, UseChoco); }

        public static void RunQueue(string verb, List<string> ids, bool choco)
        {
            Work.Run(verb + "...", delegate(WorkCtx c)
            {
                if (choco) ChocoRun(c, verb, ids);
                else if (EnsureWinget(c) != null) WingetRun(c, verb, ids);
                else
                {
                    c.Warn("winget is not available on this PC - using Chocolatey instead.", "Winget");
                    ChocoRun(c, verb, ids);
                }
                if (Work.Failed > 0) c.Warn("Queue finished: " + Work.Failed + " of " + ids.Count + " app(s) did NOT complete - see the errors above.", "Winget");
                else c.Info((verb == "install" ? "Install" : "Uninstall") + " queue finished.", "Winget");
            });
        }

        private static bool WingetHas(WorkCtx c, string wg, string id)
        {
            int code;
            Tool.Capture(wg, "list --id " + id + " -e --accept-source-agreements", out code);
            return code == 0;
        }

        private static void WingetRun(WorkCtx c, string verb, List<string> ids)
        {
            string wg = EnsureWinget(c);
            if (wg == null) { Work.Failed++; return; }
            int n = ids.Count, i = 0;
            PkgProg pp = new PkgProg(c, verb, n);
            try
            {
            foreach (string id in ids)
            {
                c.ThrowIfCancelled();
                pp.Begin(i, id);
                c.Info("winget " + verb + " : " + id + "  (" + (i + 1) + "/" + n + ")", "Winget");
                string bas = verb + " --id " + id + " -e --accept-source-agreements";
                if (verb == "install" || verb == "upgrade") bas += " --accept-package-agreements";
                int code = c.Tool(wg, bas + " --silent", delegate(string t) { pp.Feed(t); Pump(c, "Winget", t); }, Encoding.UTF8);
                if (verb == "install")
                {
                    bool done = false;
                    if (Array.IndexOf(RestartCodes, code) >= 0) done = true;
                    else if (Array.IndexOf(OkCodes, code) >= 0 || code == -1978335189) done = WingetHas(c, wg, id);
                    if (!done && Array.IndexOf(FinalCodes, code) < 0)
                    {
                        c.Warn("  not installed yet (" + WingetMessage(code) + ") - retrying with the installer window...", "Winget");
                        code = Tool.RunLong(wg, bas + " --interactive", true, 1800000);
                        if (Array.IndexOf(RestartCodes, code) >= 0) done = true;
                        else if (Array.IndexOf(FinalCodes, code) >= 0) done = false;
                        else done = WingetHas(c, wg, id);
                    }
                    if (!done && code == 0) { System.Threading.Thread.Sleep(5000); done = WingetHas(c, wg, id); }
                    if (!done && code == -1978335215)
                    {
                        c.Warn("  hash mismatch (the vendor updated the installer) - retrying with the hash check relaxed...", "Winget");
                        try { c.Tool(wg, "settings --enable InstallerHashOverride"); } catch { }
                        code = c.Tool(wg, bas + " --silent --ignore-security-hash", delegate(string t) { pp.Feed(t); Pump(c, "Winget", t); }, Encoding.UTF8);
                        if (Array.IndexOf(RestartCodes, code) >= 0) done = true;
                        else if (Array.IndexOf(OkCodes, code) >= 0) done = WingetHas(c, wg, id);
                    }
                    if (!done && Array.IndexOf(NoFallback, code) < 0)
                    {
                        string ch = FindChoco();
                        if (ch != null)
                        {
                            c.Warn("  winget could not finish this install - trying Chocolatey...", "Winget");
                            int cc = c.Tool(ch, "install " + ChocoId(id) + " -y", delegate(string t) { pp.Feed(t); Pump(c, "Pkg", t); }, Encoding.UTF8);
                            if (cc == 0 || cc == 1641 || cc == 3010) { done = true; code = 0; }
                        }
                    }
                    if (done) c.Info(code == 0 ? "  installed OK" : "  " + WingetMessage(code), "Winget");
                    else c.Error("  FAILED - " + id + " was NOT installed (" + WingetMessage(code) + ")", "Winget");
                }
                else
                {
                    string msg = WingetMessage(code);
                    if (Array.IndexOf(OkCodes, code) >= 0 || code == -1978335189 || (verb == "uninstall" && code == -1978335212)) c.Info("  " + msg, "Winget");
                    else c.Warn("  " + msg, "Winget");
                    if (Array.IndexOf(OkCodes, code) < 0 && code != -1978335189 && !(verb == "uninstall" && code == -1978335212)) Work.Failed++;
                }
                pp.End();
                i++;
            }
            }
            finally { pp.Stop(); }
        }

        private static void ChocoRun(WorkCtx c, string verb, List<string> ids)
        {
            string ch = EnsureChoco(c);
            if (ch == null) { Work.Failed++; return; }
            int n = ids.Count, i = 0;
            PkgProg pp = new PkgProg(c, verb, n);
            try
            {
            foreach (string id in ids)
            {
                c.ThrowIfCancelled();
                string cid = ChocoId(id);
                pp.Begin(i, cid);
                c.Info("choco " + verb + " : " + cid, "Pkg");
                int code = c.Tool(ch, verb + " " + cid + " -y", delegate(string t) { pp.Feed(t); Pump(c, "Pkg", t); }, Encoding.UTF8);
                if (code == 0 || code == 1641 || code == 3010) c.Info("  OK", "Pkg");
                else c.Error("  FAILED (exit code " + code + ")", "Pkg");
                pp.End();
                i++;
            }
            }
            finally { pp.Stop(); }
        }

        private void UpgradeAll()
        {
            bool choco = UseChoco;
            Work.Run("Checking for updates...", delegate(WorkCtx c)
            {
                if (choco)
                {
                    string ch = EnsureChoco(c); if (ch == null) return;
                    c.Info("Upgrading all Chocolatey packages...", "Pkg");
                    PkgProg pc = new PkgProg(c, "upgrade", 1); pc.Begin(0, "all packages");
                    try { c.Tool(ch, "upgrade all -y", delegate(string t) { pc.Feed(t); Pump(c, "Pkg", t); }, Encoding.UTF8); } finally { pc.Stop(); }
                    c.Info("Upgrade finished.", "Pkg");
                    return;
                }
                string wg = EnsureWinget(c); if (wg == null) return;
                c.Info("Upgrading all packages...", "Winget");
                PkgProg pu = new PkgProg(c, "upgrade", 1); pu.Begin(0, "all packages");
                int code;
                try { code = c.Tool(wg, "upgrade --all --silent --accept-source-agreements --accept-package-agreements", delegate(string t) { pu.Feed(t); Pump(c, "Winget", t); }, Encoding.UTF8); }
                finally { pu.Stop(); }
                c.Info("Upgrade finished (exit code " + code + ").", "Winget");
            });
        }

        private static List<string> InstalledNames()
        {
            List<string> names = new List<string>();
            RegistryKey[] roots = { RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64), RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64) };
            string[] subs = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            foreach (RegistryKey r in roots)
                foreach (string s in subs)
                    try
                    {
                        using (RegistryKey k = r.OpenSubKey(s))
                        {
                            if (k == null) continue;
                            foreach (string sk in k.GetSubKeyNames())
                                using (RegistryKey e = k.OpenSubKey(sk))
                                {
                                    string dn = e == null ? "" : Convert.ToString(e.GetValue("DisplayName"));
                                    if (dn.Length > 0) names.Add(dn);
                                }
                        }
                    }
                    catch { }
            return names;
        }

        private static bool NameInList(List<string> list, string word)
        {
            word = word.Trim();
            if (word.Length < 3) return false;
            if (word.Equals("Edge", StringComparison.OrdinalIgnoreCase)) return Removers.EdgeInstalled();
            Regex rx = new Regex("(?<![A-Za-z0-9])" + Regex.Escape(word) + "(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
            foreach (string dn in list)
                if (rx.IsMatch(dn)) { if (dn.Contains("WebView") && !word.Contains("WebView")) continue; return true; }
            return false;
        }

        private void ShowInstalled()
        {
            if (!_built) { Ui.Log.Warn("The app list is still loading - try again in a few seconds.", "Install"); return; }
            bool choco = UseChoco;
            Work.Run("Scanning installed apps...", delegate(WorkCtx c)
            {
                string txt = "";
                if (choco)
                {
                    string ch = EnsureChoco(c); if (ch == null) return;
                    txt = Tool.Capture(ch, "list --limit-output");
                }
                else
                {
                    string wg = EnsureWinget(c); if (wg == null) return;
                    c.Info("Scanning installed apps (winget list)...", "Winget");
                    string tmp = Path.Combine(Path.GetTempPath(), "rock_winget_list.txt");
                    try { File.Delete(tmp); } catch { }
                    c.Tool(Environment.GetEnvironmentVariable("ComSpec"), "/d /c \"mode con: cols=400 lines=9999 >nul 2>&1 & \"" + wg + "\" list --accept-source-agreements > \"" + tmp + "\"\"");
                    if (File.Exists(tmp)) txt = File.ReadAllText(tmp, Encoding.UTF8);
                }
                List<string> reg = InstalledNames();
                int found = 0;
                List<bool> hits = new List<bool>();
                foreach (Card card in _cards)
                {
                    bool hit;
                    if (choco) hit = Regex.IsMatch(txt, "(?im)^" + Regex.Escape(ChocoId(card.App.Id)) + "\\|");
                    else hit = txt.Length > 0 && Regex.IsMatch(txt, "(?<![\\w.-])" + Regex.Escape(card.App.Id) + "(?![\\w.-])");
                    if (!hit) hit = NameInList(reg, card.App.Name);
                    if (hit) found++;
                    hits.Add(hit);
                }
                c.UI(delegate { for (int i = 0; i < _cards.Count; i++) _cards[i].Badge.Visibility = hits[i] ? Visibility.Visible : Visibility.Collapsed; });
                c.Info(found + " of the listed apps are already installed (marked in green).", choco ? "Pkg" : "Winget");
            });
        }
    }

    // Real progress for winget / choco: reads the program's own output (download size, percent, "Starting package install", "Successfully installed")
    // and moves the bar with it. While the installer itself runs (no numbers are printed) the bar creeps forward slowly so it never looks frozen.
    internal sealed class PkgProg
    {
        private static readonly Regex Sz = new Regex(@"(\d+(?:\.\d+)?)\s*(KB|MB|GB)\s*/\s*(\d+(?:\.\d+)?)\s*(KB|MB|GB)", RegexOptions.IgnoreCase);
        private static readonly Regex Pc = new Regex(@"(?<![\d.])(\d{1,3})\s*%");
        private static readonly Regex Found = new Regex(@"\((\d+)/(\d+)\)\s+Found\s+(.+?)\s+\[");
        private readonly WorkCtx _c; private readonly string _verb;
        private int _i, _n, _stage;
        private double _frac;
        private string _name = "", _detail = "", _buf = "";
        private System.Threading.Timer _tm;

        public PkgProg(WorkCtx c, string verb, int n) { _c = c; _verb = verb; _n = n < 1 ? 1 : n; }

        private static double Mb(string v, string u)
        {
            double d = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
            u = u.ToUpperInvariant();
            return u == "GB" ? d * 1024 : (u == "KB" ? d / 1024 : d);
        }

        public void Begin(int i, string name)
        {
            lock (this) { _i = i; _name = name; _frac = 0; _stage = 0; _detail = ""; _buf = ""; }
            Push();
            if (_tm == null) _tm = new System.Threading.Timer(delegate { Creep(); }, null, 600, 600);
        }

        public void End() { lock (this) { _frac = 1.0; _detail = ""; } Push(); }

        public void Stop()
        {
            System.Threading.Timer t = _tm; _tm = null;
            if (t != null) { try { t.Dispose(); } catch { } }
        }

        private void Creep()
        {
            lock (this)
            {
                if (_stage == 0) _frac += (0.07 - _frac) * 0.2;
                else if (_stage == 2) _frac += (0.97 - _frac) * 0.045;
                else return;
            }
            Push();
        }

        public void Feed(string chunk)
        {
            lock (this)
            {
                _buf += chunk;
                if (_buf.Length > 800) _buf = _buf.Substring(_buf.Length - 800);
                string s = _buf;
                Match f = null;
                foreach (Match m in Found.Matches(s)) f = m;
                if (f != null)
                {
                    int idx = int.Parse(f.Groups[1].Value), tot = int.Parse(f.Groups[2].Value);
                    if (idx - 1 != _i || tot != _n) { _i = idx - 1; _n = tot < 1 ? 1 : tot; _frac = 0; _stage = 0; _detail = ""; _name = f.Groups[3].Value; }
                    // forget everything printed before this package (its "Successfully installed" belongs to the previous one)
                    _buf = _buf.Substring(f.Index + f.Length); s = _buf;
                }
                bool verified = s.IndexOf("verified installer hash", StringComparison.OrdinalIgnoreCase) >= 0;
                bool starting = s.IndexOf("Starting package", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Installing ", StringComparison.OrdinalIgnoreCase) >= 0 && s.IndexOf("Progress: Downloading", StringComparison.OrdinalIgnoreCase) < 0;
                bool finished = s.IndexOf("Successfully installed", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Successfully uninstalled", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("has been installed", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("has been uninstalled", StringComparison.OrdinalIgnoreCase) >= 0;
                if (_stage < 2)
                {
                    Match z = null;
                    foreach (Match m in Sz.Matches(s)) z = m;
                    if (z != null)
                    {
                        double cur = Mb(z.Groups[1].Value, z.Groups[2].Value), tot = Mb(z.Groups[3].Value, z.Groups[4].Value);
                        if (tot > 0)
                        {
                            _stage = 1; double dl = cur / tot; if (dl > 1) dl = 1;
                            _frac = Math.Max(_frac, 0.07 + 0.70 * dl);
                            _detail = "  " + z.Groups[1].Value + " / " + z.Groups[3].Value + " " + z.Groups[4].Value.ToUpperInvariant();
                        }
                    }
                    else if (s.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Match p = null;
                        foreach (Match m in Pc.Matches(s)) p = m;
                        if (p != null)
                        {
                            int v = int.Parse(p.Groups[1].Value);
                            if (v <= 100) { _stage = 1; _frac = Math.Max(_frac, 0.07 + 0.70 * v / 100.0); _detail = "  " + v + "%"; }
                        }
                    }
                }
                if (_stage < 2 && (verified || starting))
                {
                    _stage = 2; _detail = ""; _frac = Math.Max(_frac, verified || starting ? 0.78 : _frac);
                    if (_verb == "uninstall") _frac = Math.Max(_frac, 0.3);
                }
                if (finished) _frac = Math.Max(_frac, 0.99);
            }
            Push();
        }

        private void Push()
        {
            double pct; string text;
            lock (this)
            {
                double fr = _frac > 1 ? 1 : _frac;
                pct = 100.0 * (_i + fr) / _n; if (pct > 100) pct = 100;
                string what = _stage == 1 ? "Downloading" : (_verb == "uninstall" ? "Uninstalling" : (_verb == "upgrade" ? "Updating" : "Installing"));
                text = Lang.L(what) + " " + _name + _detail + "  (" + (_i + 1) + "/" + _n + ")";
            }
            if (pct < 0.5) pct = -1;      // nothing measured yet: the moving (indeterminate) bar instead of an empty one stuck at 0%
            try { _c.Progress(pct, text); } catch { }
        }
    }
}
