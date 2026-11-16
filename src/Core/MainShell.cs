using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RockToolbox.Native
{
    // C# port of src\core\Navigation.ps1 (tabs, page switching, window size) + the caption buttons.
    internal sealed class MainShell
    {
        // tab name -> panel x:Name in ui\MainWindow.xaml (same order as the PowerShell $pages table)
        private static readonly string[][] PageMap = new string[][]
        {
            new[] { "Install", "pnlInstall" }, new[] { "Tweaks", "pnlTweaks" }, new[] { "Debloat", "pnlDebloat" },
            new[] { "Config", "pnlConfig" }, new[] { "Updates", "pnlUpdates" }, new[] { "Windows Update", "pnlWU" },
            new[] { "Uninstall Apps", "pnlUnApps" }, new[] { "Drivers", "pnlDrv" }, new[] { "System Info", "pnlSysInfo" },
            new[] { "Cleanup", "pnlClean" }, new[] { "Startup", "pnlStartup" }, new[] { "Repair", "pnlRepair" },
            new[] { "Wallpapers", "pnlWall" }, new[] { "God Mode", "pnlGod" },
            new[] { "Settings", "pnlSettings" }
        };

        private readonly Window _w;
        private readonly AppSettings _s;
        private readonly Logger _log;
        private readonly Dictionary<string, FrameworkElement> _panels = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, Button> _tabs = new Dictionary<string, Button>();
        private readonly Dictionary<string, IPage> _pages = new Dictionary<string, IPage>();
        private string _cur = "";
        public static MainShell Current;
        public static string StartOverride;       // tab to open after the window was rebuilt (look / language change)
        public string CurTab { get { return _cur; } }
        private bool _first;
        private bool _navStyled;

        public MainShell(Window w, AppSettings s, Logger log, IEnumerable<IPage> pages)
        {
            _w = w; _s = s; _log = log; Current = this;
            foreach (IPage p in pages) _pages[p.Key] = p;
        }

        private static Brush B(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }

        public void Start()
        {
            foreach (string[] m in PageMap)
            {
                FrameworkElement el = _w.FindName(m[1]) as FrameworkElement;
                if (el != null) _panels[m[0]] = el;
            }

            WireCaptionButtons();
            WireHistory();

            Panel tabBar = (Panel)_w.FindName("tabBar");
            Style navStyle = null;
            try { navStyle = (Style)_w.FindResource("NavBtn"); } catch { }
            _navStyled = navStyle != null;
            if (Theme.Modern)
            {
                // side menu with groups
                string[][] groups = new string[][]
                {
                    new[] { "Apps", "Install", "Updates", "Uninstall Apps" },
                    new[] { "System", "Tweaks", "Debloat", "Config", "Windows Update", "Drivers", "Startup" },
                    new[] { "Maintenance", "Cleanup", "Repair", "System Info" },
                    new[] { "More", "Wallpapers", "God Mode" }
                };
                bool firstGroup = true;
                foreach (string[] g in groups)
                {
                    TextBlock h = new TextBlock();
                    h.Text = Lang.L(g[0]); h.FontSize = 10.5; h.FontWeight = FontWeights.SemiBold;
                    h.Margin = firstGroup ? new Thickness(10, 2, 0, 5) : new Thickness(10, 12, 0, 5);
                    h.Foreground = Ui.Brush("#9AA0B8");
                    firstGroup = false;
                    tabBar.Children.Add(h);
                    for (int i = 1; i < g.Length; i++) AddNav(tabBar, navStyle, g[i], true);
                }
                foreach (string[] m in PageMap)
                    if (m[0] != "Settings" && _panels.ContainsKey(m[0]) && !_tabs.ContainsKey(m[0])) AddNav(tabBar, navStyle, m[0], true);
            }
            else
            {
                foreach (string[] m in PageMap)
                    if (m[0] != "Settings") AddNav(tabBar, navStyle, m[0], false);
            }

            foreach (IPage p in _pages.Values) p.Init(_w, _log);

            string start = "";
            foreach (string[] m in PageMap) if (_pages.ContainsKey(m[0]) && _panels.ContainsKey(m[0])) { start = m[0]; break; }
            string last = _s.GetString("LastTab");
            string cfg = _s.GetString("StartTab");
            if (_pages.ContainsKey(cfg) && _panels.ContainsKey(cfg)) start = cfg;
            if (_s.GetBool("RememberTab") && _pages.ContainsKey(last)) start = last;
            if (StartOverride != null && _pages.ContainsKey(StartOverride) && _panels.ContainsKey(StartOverride)) start = StartOverride;
            Show(start);

            _log.Info("Rock Toolbox ready.", "UI");
            SizeWindow();
            if (_s.GetBool("StartMaximized")) _w.WindowState = WindowState.Maximized;
            if (_s.GetBool("Topmost")) _w.Topmost = true;
        }

        private void AddNav(Panel tabBar, Style navStyle, string key, bool side)
        {
            if (!_panels.ContainsKey(key) || _tabs.ContainsKey(key)) return;
            bool ported = _pages.ContainsKey(key);
            Button b = new Button();
            if (navStyle != null) b.Style = navStyle;
            b.Content = Lang.L(key);
            b.Tag = key;
            b.FontWeight = FontWeights.SemiBold;
            b.BorderThickness = new Thickness(0);
            if (side)
            {
                b.FontSize = 13;
                b.HorizontalAlignment = HorizontalAlignment.Stretch;
                b.HorizontalContentAlignment = HorizontalAlignment.Left;
                b.Padding = new Thickness(14, 8, 12, 8);
                b.Margin = new Thickness(0, 0, 0, 2);
            }
            else
            {
                b.FontSize = 13;
                b.HorizontalAlignment = HorizontalAlignment.Stretch;
                b.HorizontalContentAlignment = HorizontalAlignment.Left;
                b.Padding = new Thickness(12, 8, 12, 8);
                b.Margin = new Thickness(0, 0, 0, 2);
            }
            if (navStyle == null) { b.Foreground = B("#E6E8F2"); b.Background = Brushes.Transparent; }
            if (ported) b.Click += delegate(object s, RoutedEventArgs e) { Show((string)((Button)s).Tag); };
            else { b.IsEnabled = false; b.Opacity = 0.45; b.ToolTip = "Not ported to C# yet (still in the PowerShell version)."; }
            tabBar.Children.Add(b);
            _tabs[key] = b;
        }

        public void Show(string name)
        {
            Record(name);
            IPage leaving;
            if (_cur.Length > 0 && _cur != name && _pages.TryGetValue(_cur, out leaving))
            {
                IHidable hid = leaving as IHidable;
                if (hid != null) hid.OnHidden();
            }
            foreach (KeyValuePair<string, FrameworkElement> kv in _panels)
                kv.Value.Visibility = kv.Key == name ? Visibility.Visible : Visibility.Collapsed;
            _cur = name;
            foreach (KeyValuePair<string, Button> kv in _tabs)
            {
                bool on = kv.Key == _cur;
                System.Windows.Controls.Primitives.Selector.SetIsSelected(kv.Value, on);
                if (!_navStyled) kv.Value.Background = on ? Ui.Brush("#2F6FB0") : Brushes.Transparent;
            }

            TextBlock crumb = _w.FindName("txtCrumb") as TextBlock;
            if (crumb != null) crumb.Text = Lang.L(name);
            if (_panels.ContainsKey(name) && _first) Anim.PageTransition(_panels[name]);
            _first = true;

            _s.Set("LastTab", name);
            if (_s.GetBool("RememberTab")) _s.SaveSoon();

            UpdateArrows();
            IPage page;
            if (_pages.TryGetValue(name, out page)) page.OnShown();
        }

        // ---------------- back / forward history ----------------
        private readonly List<string> _hist = new List<string>();
        private int _hi = -1;
        private bool _navigating;
        private Button _back, _fwd;

        private void WireHistory()
        {
            _back = _w.FindName("btnBack") as Button; _fwd = _w.FindName("btnFwd") as Button;
            if (_back != null) _back.Click += delegate { GoBack(); };
            if (_fwd != null) _fwd.Click += delegate { GoForward(); };
            _w.PreviewMouseDown += delegate(object s, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (e.ChangedButton == System.Windows.Input.MouseButton.XButton1) { GoBack(); e.Handled = true; }
                else if (e.ChangedButton == System.Windows.Input.MouseButton.XButton2) { GoForward(); e.Handled = true; }
            };
            _w.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
            {
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == 0) return;
                if (e.SystemKey == System.Windows.Input.Key.Left) { GoBack(); e.Handled = true; }
                else if (e.SystemKey == System.Windows.Input.Key.Right) { GoForward(); e.Handled = true; }
            };
        }
        private void Record(string name)
        {
            if (_navigating) return;
            if (_hi >= 0 && _hist[_hi] == name) return;
            if (_hi < _hist.Count - 1) _hist.RemoveRange(_hi + 1, _hist.Count - _hi - 1);
            _hist.Add(name); if (_hist.Count > 60) _hist.RemoveAt(0);
            _hi = _hist.Count - 1;
        }
        private void UpdateArrows()
        {
            if (_back != null) _back.IsEnabled = _hi > 0;
            if (_fwd != null) _fwd.IsEnabled = _hi >= 0 && _hi < _hist.Count - 1;
        }
        public void GoBack() { Go(_hi - 1); }
        public void GoForward() { Go(_hi + 1); }
        private void Go(int i)
        {
            if (i < 0 || i >= _hist.Count || !_panels.ContainsKey(_hist[i])) return;
            _hi = i; _navigating = true;
            try { Show(_hist[i]); } finally { _navigating = false; }
        }

        private void WireCaptionButtons()
        {
            Button min = _w.FindName("btnMin") as Button;
            Button max = _w.FindName("btnMax") as Button;
            Button close = _w.FindName("btnClose") as Button;
            FrameworkElement pathMax = _w.FindName("pathMax") as FrameworkElement;
            FrameworkElement pathRestore = _w.FindName("pathRestore") as FrameworkElement;

            if (min != null) min.Click += delegate { try { SystemCommands.MinimizeWindow(_w); } catch { _w.WindowState = WindowState.Minimized; } };
            if (max != null) max.Click += delegate
            {
                // the system commands give the normal smooth Windows animation (setting WindowState by hand stutters on a borderless window)
                try { if (_w.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(_w); else SystemCommands.MaximizeWindow(_w); }
                catch { _w.WindowState = _w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
            };
            if (close != null) close.Click += delegate { _w.Close(); };
            _w.StateChanged += delegate
            {
                bool m = _w.WindowState == WindowState.Maximized;
                // a borderless maximized window overhangs the screen by the resize border - pull the content back in
                FrameworkElement rg = _w.FindName("rootGrid") as FrameworkElement;
                if (rg != null) rg.Margin = m ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
                if (pathMax != null) pathMax.Visibility = m ? Visibility.Collapsed : Visibility.Visible;
                if (pathRestore != null) pathRestore.Visibility = m ? Visibility.Visible : Visibility.Collapsed;
            };
            _w.Closing += delegate { _s.SaveNow(); };
        }

        // open at a size that fits THIS screen: 75% x 72% of the work area, centered
        private void SizeWindow()
        {
            Rect wa = SystemParameters.WorkArea;
            double tw = Math.Max(1000, Math.Min(1800, Math.Round(wa.Width * 0.75)));
            double th = Math.Max(600, Math.Min(1000, Math.Round(wa.Height * 0.72)));
            _w.Width = Math.Min(tw, wa.Width - 20);
            _w.Height = Math.Min(th, wa.Height - 20);
            _w.WindowStartupLocation = WindowStartupLocation.Manual;
            _w.Left = wa.Left + (wa.Width - _w.Width) / 2;
            _w.Top = wa.Top + (wa.Height - _w.Height) / 2;
        }
    }
}
