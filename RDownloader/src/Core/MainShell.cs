using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RDownloader.Native
{
    // Two screens (Downloads, Settings), the caption buttons and the window size.
    internal sealed class MainShell
    {
        private static readonly string[][] PageMap = new string[][]
        {
            new[] { "Downloads", "pnlDownloads" }, new[] { "Settings", "pnlSettings" }
        };

        private readonly Window _w;
        private readonly AppSettings _s;
        private readonly Logger _log;
        private readonly Dictionary<string, FrameworkElement> _panels = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, IPage> _pages = new Dictionary<string, IPage>();
        private string _cur = "";
        public static MainShell Current;
        public static string StartOverride;       // screen to open after the window was rebuilt (look / language change)
        public string CurTab { get { return _cur; } }
        private bool _first;

        public MainShell(Window w, AppSettings s, Logger log, IEnumerable<IPage> pages)
        {
            _w = w; _s = s; _log = log; Current = this;
            foreach (IPage p in pages) _pages[p.Key] = p;
        }

        public IPage PageOf(string key) { IPage p; return _pages.TryGetValue(key, out p) ? p : null; }
        public string CurrentPage { get { return _cur; } }

        public void Start()
        {
            foreach (string[] m in PageMap)
            {
                FrameworkElement el = _w.FindName(m[1]) as FrameworkElement;
                if (el != null) _panels[m[0]] = el;
            }
            WireCaptionButtons();
            WireHistory();
            foreach (IPage p in _pages.Values) p.Init(_w, _log);

            Button gear = _w.FindName("btnGear") as Button;
            if (gear != null) gear.Visibility = Visibility.Collapsed;      // the settings live in Options now

            string start = "Downloads";
            if (StartOverride != null && _panels.ContainsKey(StartOverride)) start = StartOverride;
            Show(start);
            _log.Info("R-Downloader ready.", "UI");
            SizeWindow();
            if (_s.GetBool("StartMaximized")) _w.WindowState = WindowState.Maximized;
            if (_s.GetBool("Topmost")) _w.Topmost = true;
        }

        public void Show(string name)
        {
            Record(name);
            foreach (KeyValuePair<string, FrameworkElement> kv in _panels)
                kv.Value.Visibility = kv.Key == name ? Visibility.Visible : Visibility.Collapsed;
            _cur = name;
            TextBlock crumb = _w.FindName("txtCrumb") as TextBlock;
            if (crumb != null) crumb.Text = name == "Settings" ? Lang.L("Settings") : "";
            if (_panels.ContainsKey(name) && _first) Anim.PageTransition(_panels[name]);
            _first = true;
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

        // open at a size that fits THIS screen, centered
        private void SizeWindow()
        {
            Rect wa = SystemParameters.WorkArea;
            double tw = Math.Max(900, Math.Min(1500, Math.Round(wa.Width * 0.70)));
            double th = Math.Max(540, Math.Min(900, Math.Round(wa.Height * 0.70)));
            _w.Width = Math.Min(tw, wa.Width - 20);
            _w.Height = Math.Min(th, wa.Height - 20);
            _w.WindowStartupLocation = WindowStartupLocation.Manual;
            _w.Left = wa.Left + (wa.Width - _w.Width) / 2;
            _w.Top = wa.Top + (wa.Height - _w.Height) / 2;
        }
    }
}
