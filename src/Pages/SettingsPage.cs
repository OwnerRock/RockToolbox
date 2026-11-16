using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace RockToolbox.Native
{
    // C# port of src\pages\Settings.ps1 (appearance, behaviour, safety, gear menu, About, update check).
    // Theme / accent / background changes are applied by restarting the window (the whole UI is re-themed on load).
    internal sealed partial class SettingsPage : IPage
    {
        public const string Version = "4.1.0";
        private const string RepoUrl = "https://github.com/OwnerRock/RockToolbox";
        private static readonly int[] FontSizes = { 12, 13, 14, 15, 16 };
        private static readonly string[] Scales = { "90%", "100%", "110%", "125%", "150%" };

        private bool _busy;
        private CheckBox _light, _gearLight;
        private TextBox _accentHex, _bgHex;
        private Panel _swatches;
        private ComboBox _bgStyle, _font, _scale, _startTab;
        private CheckBox _remember;

        public string Key { get { return "Settings"; } }
        public void OnShown() { Sync(); }

        public static void Restart() { AppLife.Restart(); }

        private static bool _updChecked;

        // colours, light / dark, background style, Classic / Modern: shown at once by rebuilding the window (no restart)
        private static void ApplyThemeChange()
        {
            Ui.Settings.SaveNow();
            Ui.Win.Dispatcher.BeginInvoke(new Action(UiHost.Reapply), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void Init(Window w, Logger log)
        {
            _light = Ui.Find<CheckBox>("chkLight"); _gearLight = Ui.Find<CheckBox>("chkGearLight");
            _accentHex = Ui.Find<TextBox>("txtAccentHex"); _bgHex = Ui.Find<TextBox>("txtBgHex");
            _swatches = Ui.Find<Panel>("swatchPanel");
            _bgStyle = Ui.Find<ComboBox>("cmbBgStyle"); _font = Ui.Find<ComboBox>("cmbFontSize"); _scale = Ui.Find<ComboBox>("cmbScale");
            _startTab = Ui.Find<ComboBox>("cmbStartTab"); _remember = Ui.Find<CheckBox>("chkRemember");

            foreach (string b in Theme.BgNames) _bgStyle.Items.Add(b);
            foreach (int f in FontSizes) _font.Items.Add(f);
            foreach (string s in Scales) _scale.Items.Add(s);
            foreach (string t in new string[] { "Install", "Tweaks", "Debloat", "Config", "Updates", "Windows Update", "Uninstall Apps", "Drivers", "System Info", "Cleanup", "Startup", "Repair", "Wallpapers", "God Mode" })
                _startTab.Items.Add(t);

            foreach (string n in new string[] { "chkHideConsole" })
            {
                FrameworkElement el = Ui.Win.FindName(n) as FrameworkElement;
                if (el != null) { el.IsEnabled = false; el.ToolTip = "Not used in the native version (there is no console and only one look)."; }
            }
            foreach (string x in new string[] { "Off", "Smooth", "Glide", "Snappy", "Bouncy" }) Ui.Find<ComboBox>("cmbScrollAnim").Items.Add(x);
            foreach (string x in new string[] { "Slow", "Normal", "Fast" }) Ui.Find<ComboBox>("cmbScrollSpeed").Items.Add(x);
            foreach (string x in new string[] { "Slide up", "Fade", "Zoom", "Off" }) Ui.Find<ComboBox>("cmbPageAnim").Items.Add(x);
            BindCombo("cmbScrollAnim", "ScrollAnim", "Smooth", delegate { Anim.ApplyScrollSettings(); });
            BindCombo("cmbScrollSpeed", "ScrollSpeed", "Normal", delegate { Anim.ApplyScrollSettings(); });
            BindCombo("cmbPageAnim", "PageAnim", "Slide up", null);
            Bind("chkCardAnim", "CardAnim", null);
            WireBackground();
            WireTaskbar();

            BuildSwatches();
            RoutedEventHandler themeClick = delegate(object s, RoutedEventArgs e)
            {
                if (_busy) return;
                bool darkOn = ((CheckBox)s).IsChecked == true;     // the switch says "Dark mode": on = dark
                Ui.Settings.Set("Theme", darkOn ? "Dark" : "Light");
                Sync(); ApplyThemeChange();
            };
            _light.Click += themeClick; _gearLight.Click += themeClick;
            try
            {
                Button bLogs = new Button(); bLogs.Content = Lang.L("Show logs"); bLogs.Padding = new Thickness(18, 6, 18, 6); bLogs.HorizontalAlignment = HorizontalAlignment.Left; bLogs.Margin = new Thickness(0, 0, 0, 12);
                bLogs.Click += delegate { LogView.Show(false); };
                ScrollViewer svs = Ui.Find<ScrollViewer>("pnlSettings");
                StackPanel spn = svs.Content as StackPanel;
                if (spn != null) spn.Children.Insert(2, bLogs);
            }
            catch { }
            Ui.Find<Button>("btnAccentApply").Click += delegate
            {
                string x = _accentHex.Text.Trim(); if (x.Length > 0 && x[0] != '#') x = "#" + x;
                if (!Regex.IsMatch(x, "^#[0-9A-Fa-f]{6}$")) { Ui.Log.Warn("Accent colour: use a hex code like #4FD1FF.", "Settings"); return; }
                Ui.Settings.Set("Accent", "Custom"); Ui.Settings.Set("AccentHex", x.ToUpperInvariant()); ApplyThemeChange();
            };
            Ui.Find<Button>("btnAccentReset").Click += delegate { Ui.Settings.Set("Accent", "Ocean"); Ui.Settings.Set("AccentHex", "#4FD1FF"); ApplyThemeChange(); };
            _bgStyle.SelectionChanged += delegate
            {
                if (_busy || _bgStyle.SelectedItem == null) return;
                Ui.Settings.Set("Background", (string)_bgStyle.SelectedItem); ApplyThemeChange();
            };
            Ui.Find<Button>("btnBgColorApply").Click += delegate
            {
                string x = _bgHex.Text.Trim(); if (x.Length > 0 && x[0] != '#') x = "#" + x;
                if (!Regex.IsMatch(x, "^#[0-9A-Fa-f]{6}$")) { Ui.Log.Warn("Background colour: use a hex code like #1F2329.", "Settings"); return; }
                Ui.Settings.Set("BgColor", x.ToUpperInvariant()); ApplyThemeChange();
            };
            Ui.Find<Button>("btnBgColorClear").Click += delegate { Ui.Settings.Set("BgColor", ""); ApplyThemeChange(); };

            _font.SelectionChanged += delegate
            {
                if (_busy || _font.SelectedItem == null) return;
                Ui.Settings.Set("FontSize", (int)_font.SelectedItem); Ui.Settings.SaveSoon(); ApplyLook();
            };
            _scale.SelectionChanged += delegate
            {
                if (_busy || _scale.SelectedItem == null) return;
                Ui.Settings.Set("Scale", int.Parse(((string)_scale.SelectedItem).TrimEnd('%'))); Ui.Settings.SaveSoon(); ApplyLook();
            };
            Bind("chkTopmost", "Topmost", delegate { ApplyLook(); });
            Bind("chkMaxStart", "StartMaximized", null);
            Bind("chkCloseTray", "CloseToTray", null);
            Bind("chkStatusBar", "StatusBar", delegate { ApplyLook(); });
            Bind("chkRemember", "RememberTab", delegate { _startTab.IsEnabled = !_remember.IsChecked.GetValueOrDefault(); });
            Bind("chkAutoRestore", "AutoRestore", null);
            Bind("chkAutoPkg", "AutoInstallPkgMgrs", null);
            Bind("chkAutoUpd", "AutoUpdateCheck", null);
            Bind("chkInstant", "InstantApply", null);
            _startTab.SelectionChanged += delegate
            {
                if (_busy || _startTab.SelectedItem == null) return;
                Ui.Settings.Set("StartTab", (string)_startTab.SelectedItem); Ui.Settings.SaveSoon();
            };

            Ui.Find<CheckBox>("chkClassicUi").Click += delegate(object cs, RoutedEventArgs ce)
            {
                if (_busy) return;
                Ui.Settings.Set("UiStyle", ((CheckBox)cs).IsChecked == true ? "Classic" : "Modern");
                ApplyThemeChange();
            };
            WireGear();
            Sync();
            ApplyLook();
            if (!_updChecked && Ui.Settings.GetBool("AutoUpdateCheck", true)) { _updChecked = true; UpdateCheck(true); }
        }

        private void BindCombo(string name, string key, string def, Action extra)
        {
            ComboBox cb = Ui.Find<ComboBox>(name);
            cb.SelectionChanged += delegate
            {
                if (_busy || cb.SelectedItem == null) return;
                Ui.Settings.Set(key, Convert.ToString(cb.SelectedItem)); Ui.Settings.SaveSoon();
                if (extra != null) extra();
            };
        }

        // ---------------- background picture ----------------
        private void WireBackground()
        {
            Slider op = Ui.Find<Slider>("sldBgOpacity"), bl = Ui.Find<Slider>("sldBgBlur");
            TextBlock opT = Ui.Find<TextBlock>("txtBgOpacity"), blT = Ui.Find<TextBlock>("txtBgBlur");
            op.ValueChanged += delegate
            {
                int v = (int)Math.Round(op.Value); opT.Text = v + "%";
                if (_busy) return;
                Ui.Settings.Set("BgOpacity", v); BgFx.SetOpacity(); Ui.Settings.SaveSoon();
            };
            bl.ValueChanged += delegate
            {
                int v = (int)Math.Round(bl.Value); blT.Text = "" + v;
                if (_busy) return;
                Ui.Settings.Set("BgBlur", v); BgFx.ApplyBlur(); Ui.Settings.SaveSoon();
            };
            Ui.Find<Button>("btnBgImgPick").Click += delegate
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog(); dlg.Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp";
                if (dlg.ShowDialog() != true) return;
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox");
                    Directory.CreateDirectory(dir);
                    string dest = Path.Combine(dir, "background" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                    File.Copy(dlg.FileName, dest, true);
                    Ui.Settings.Set("BgImage", dest); Ui.Settings.SaveSoon();
                    BgFx.Apply(); Sync();
                    Ui.Log.Info("Background picture applied.", "Settings");
                }
                catch (Exception ex) { Ui.Log.Error("Could not use that picture: " + ex.Message, "Settings"); }
            };
            Ui.Find<Button>("btnBgImgClear").Click += delegate { Ui.Settings.Set("BgImage", ""); Ui.Settings.SaveSoon(); BgFx.Apply(); Sync(); };
            Ui.Find<Button>("btnBgRestore").Click += delegate
            {
                Ui.Settings.Set("BgImage", ""); Ui.Settings.Set("BgOpacity", 40); Ui.Settings.Set("BgBlur", 0);
                bool colors = Ui.Settings.GetString("BgColor").Length > 0 || (Ui.Settings.GetString("Background").Length > 0 && Ui.Settings.GetString("Background") != "Default");
                Ui.Settings.Set("BgColor", ""); Ui.Settings.Set("Background", "Default"); Ui.Settings.SaveNow();
                BgFx.Apply(); Sync();
                if (colors) ApplyThemeChange();
            };
        }

        // ---------------- taskbar pin / start with Windows ----------------
        private void WireTaskbar()
        {
            CheckBox cb = Ui.Find<CheckBox>("chkTaskbar");
            cb.IsChecked = AppLife.PinnedLinks().Count > 0;
            cb.ToolTip = "Windows has no official way to pin from a program: Explorer restarts so the pin appears. You can also right-click the taskbar icon > Pin to taskbar.";
            cb.Click += delegate
            {
                try
                {
                    if (cb.IsChecked == true) { AppLife.PinTaskbar(true); Ui.Log.Info("Rock Toolbox was pinned to the taskbar (Explorer restarted).", "Settings"); }
                    else { AppLife.UnpinTaskbar(true); Ui.Log.Info("Rock Toolbox was unpinned from the taskbar.", "Settings"); }
                }
                catch (Exception ex) { Ui.Log.Error("Could not change the taskbar pin: " + ex.Message, "Settings"); cb.IsChecked = AppLife.PinnedLinks().Count > 0; }
            };

            CheckBox rs = Ui.Find<CheckBox>("chkRunStartup");
            rs.Click += delegate
            {
                if (_busy) return;
                try
                {
                    AppLife.SetStartup(rs.IsChecked == true);
                    Ui.Log.Info(rs.IsChecked == true ? "Rock Toolbox will start in the background when you sign in to Windows." : "Rock Toolbox will no longer start with Windows.", "Settings");
                }
                catch (Exception ex) { Ui.Log.Error("Could not change the startup setting: " + ex.Message, "Settings"); rs.IsChecked = AppLife.StartupEnabled(); }
            };
        }

        private void Bind(string name, string key, Action extra)
        {
            CheckBox cb = Ui.Find<CheckBox>(name);
            cb.Click += delegate
            {
                if (_busy) return;
                Ui.Settings.Set(key, cb.IsChecked == true); Ui.Settings.SaveSoon();
                if (extra != null) extra();
            };
        }

        private void BuildSwatches()
        {
            _swatches.Children.Clear();
            string cur = Ui.Settings.GetString("Accent");
            for (int i = 0; i < Theme.AccentNames.Length; i++)
            {
                Button b = new Button(); b.Width = 34; b.Height = 34; b.Margin = new Thickness(4); b.Padding = new Thickness(0);
                b.Tag = Theme.AccentNames[i]; b.ToolTip = Theme.AccentNames[i];
                b.Background = (Brush)new BrushConverter().ConvertFromString(Theme.AccentHex[i]);
                bool on = Theme.AccentNames[i] == cur || (cur.Length == 0 && i == 0);
                b.BorderThickness = new Thickness(on ? 3 : 1);
                b.BorderBrush = Ui.Brush(on ? "#E6E8F2" : "#35506A");
                b.Click += delegate(object s, RoutedEventArgs e)
                {
                    string n = (string)((Button)s).Tag; int idx = Array.IndexOf(Theme.AccentNames, n);
                    Ui.Settings.Set("Accent", n); Ui.Settings.Set("AccentHex", Theme.AccentHex[idx]); ApplyThemeChange();
                };
                _swatches.Children.Add(b);
            }
        }

        private void Sync()
        {
            _busy = true;
            try
            {
                bool light = Ui.Settings.GetString("Theme") != "Dark";
                _light.IsChecked = !light; _gearLight.IsChecked = !light;
                string lbl = Lang.L("Dark mode"); _light.Content = lbl; _gearLight.Content = lbl;
                Ui.Find<CheckBox>("chkClassicUi").IsChecked = Ui.Settings.GetString("UiStyle") == "Classic";
                _bgStyle.IsEnabled = !light;
                string bg = Ui.Settings.GetString("Background"); _bgStyle.SelectedItem = _bgStyle.Items.Contains(bg) ? bg : "Default";
                int fs = 13; try { fs = Convert.ToInt32(Ui.Settings.GetString("FontSize")); } catch { }
                _font.SelectedItem = Array.IndexOf(FontSizes, fs) >= 0 ? fs : 13;
                int sc = 100; try { sc = Convert.ToInt32(Ui.Settings.GetString("Scale")); } catch { }
                string sct = sc + "%"; _scale.SelectedItem = _scale.Items.Contains(sct) ? sct : "100%";
                SetCheck("chkTopmost", Ui.Settings.GetBool("Topmost")); SetCheck("chkCloseTray", Ui.Settings.GetBool("CloseToTray", true)); Ui.Find<CheckBox>("chkRunStartup").IsChecked = AppLife.StartupEnabled(); SetCheck("chkMaxStart", Ui.Settings.GetBool("StartMaximized"));
                SetCheck("chkStatusBar", Ui.Settings.GetBool("StatusBar", true)); SetCheck("chkRemember", Ui.Settings.GetBool("RememberTab"));
                SetCheck("chkAutoRestore", Ui.Settings.GetBool("AutoRestore", true)); SetCheck("chkAutoUpd", Ui.Settings.GetBool("AutoUpdateCheck", true));
                SetCheck("chkAutoPkg", Ui.Settings.GetBool("AutoInstallPkgMgrs", true)); SetCheck("chkInstant", Ui.Settings.GetBool("InstantApply"));
                string sa = Ui.Settings.GetString("ScrollAnim"); Ui.Find<ComboBox>("cmbScrollAnim").SelectedItem = sa.Length > 0 && Ui.Find<ComboBox>("cmbScrollAnim").Items.Contains(sa) ? sa : "Smooth";
                string ss = Ui.Settings.GetString("ScrollSpeed"); Ui.Find<ComboBox>("cmbScrollSpeed").SelectedItem = ss.Length > 0 && Ui.Find<ComboBox>("cmbScrollSpeed").Items.Contains(ss) ? ss : "Normal";
                string pa = Ui.Settings.GetString("PageAnim"); Ui.Find<ComboBox>("cmbPageAnim").SelectedItem = pa.Length > 0 && Ui.Find<ComboBox>("cmbPageAnim").Items.Contains(pa) ? pa : "Slide up";
                SetCheck("chkCardAnim", Ui.Settings.GetBool("CardAnim", true));
                bool hasImg = BgFx.HasImage;
                Ui.Find<Slider>("sldBgOpacity").Value = BgFx.Opacity; Ui.Find<TextBlock>("txtBgOpacity").Text = BgFx.Opacity + "%";
                Ui.Find<Slider>("sldBgBlur").Value = BgFx.Blur; Ui.Find<TextBlock>("txtBgBlur").Text = "" + BgFx.Blur;
                Ui.Find<Slider>("sldBgOpacity").IsEnabled = hasImg; Ui.Find<Slider>("sldBgBlur").IsEnabled = hasImg;
                Ui.Find<TextBlock>("txtBgImgName").Text = hasImg ? Path.GetFileName(Ui.Settings.GetString("BgImage")) : "";
                string st = Ui.Settings.GetString("StartTab"); _startTab.SelectedItem = _startTab.Items.Contains(st) ? st : "Install";
                _startTab.IsEnabled = !Ui.Settings.GetBool("RememberTab");
                _accentHex.Text = Theme.CurrentAccentHex(Ui.Settings); _bgHex.Text = Ui.Settings.GetString("BgColor");
            }
            finally { _busy = false; }
        }

        private static void SetCheck(string name, bool v) { Ui.Find<CheckBox>(name).IsChecked = v; }

        public static void ApplyLook()
        {
            int fs = 13; try { fs = Convert.ToInt32(Ui.Settings.GetString("FontSize")); } catch { }
            int sc = 100; try { sc = Convert.ToInt32(Ui.Settings.GetString("Scale")); } catch { }
            if (fs < 10 || fs > 24) fs = 13; if (sc < 50 || sc > 250) sc = 100;
            Ui.Win.FontSize = fs;
            double f = sc / 100.0;
            Ui.Find<Grid>("rootGrid").LayoutTransform = new ScaleTransform(f, f);
            Ui.Win.Topmost = Ui.Settings.GetBool("Topmost");
            Ui.Find<FrameworkElement>("statusBar").Visibility = Ui.Settings.GetBool("StatusBar", true) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------- gear menu ----------------
        private static int _gearClosedAt;

        private void WireGear()
        {
            System.Windows.Controls.Primitives.Popup pop = Ui.Find<System.Windows.Controls.Primitives.Popup>("popGear");
            FrameworkElement menu = Ui.Find<FrameworkElement>("menuGear");
            FrameworkElement lang = Ui.Find<FrameworkElement>("menuLang");
            FrameworkElement popRoot = Ui.Find<FrameworkElement>("popGearRoot");
            Button gear = Ui.Find<Button>("btnGear");
            // the menu is a real Popup (StaysOpen = false): WPF closes it on any click outside, on the title bar and when the window loses focus.
            // hide() fades it out first; Closed resets it whatever way it was closed.
            Action hide = delegate
            {
                if (!pop.IsOpen) return;
                lang.Visibility = Visibility.Collapsed;
                Anim.PopOut(menu);
                System.Windows.Threading.DispatcherTimer t = new System.Windows.Threading.DispatcherTimer();
                t.Interval = TimeSpan.FromMilliseconds(150);
                t.Tick += delegate { t.Stop(); pop.IsOpen = false; };
                t.Start();
            };
            pop.Closed += delegate
            {
                _gearClosedAt = Environment.TickCount;
                menu.BeginAnimation(UIElement.OpacityProperty, null); menu.Opacity = 1.0;
                menu.Visibility = Visibility.Collapsed; lang.Visibility = Visibility.Collapsed;
            };
            gear.Click += delegate
            {
                // the press that closed the popup arrives first: do not open it again with the same click
                if (Environment.TickCount - _gearClosedAt < 250) return;
                if (pop.IsOpen) { hide(); return; }
                double sc = 1.0; try { sc = Convert.ToInt32(Ui.Settings.GetString("Scale")) / 100.0; } catch { }
                if (sc < 0.5 || sc > 2.5) sc = 1.0;
                popRoot.LayoutTransform = new ScaleTransform(sc, sc);
                // the menu sits right under the gear, its right edge on the right edge of the window, and it never leaves the window
                try
                {
                    FrameworkElement rg = Ui.Find<FrameworkElement>("rootGrid");
                    double gl = gear.TransformToAncestor(rg).Transform(new Point(0, 0)).X;
                    double toRight = rg.ActualWidth - (gl + gear.ActualWidth);
                    pop.Placement = System.Windows.Controls.Primitives.PlacementMode.Custom;
                    pop.CustomPopupPlacementCallback = delegate(Size popupSize, Size targetSize, Point offset)
                    {
                        double x = targetSize.Width + toRight - popupSize.Width + 12;
                        if (x < -gl) x = -gl;
                        return new System.Windows.Controls.Primitives.CustomPopupPlacement[] { new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point(x, targetSize.Height + 2), System.Windows.Controls.Primitives.PopupPrimaryAxis.None) };
                    };
                }
                catch { }
                lang.Visibility = Visibility.Collapsed;
                ResetItems(menu);
                menu.Visibility = Visibility.Collapsed;
                pop.IsOpen = true;
                Anim.PopIn(menu, 1.0, 0.0);
            };
            Ui.Find<Button>("btnGearSettings").Click += delegate { hide(); if (MainShell.Current != null) MainShell.Current.Show("Settings"); };
            Ui.Find<Button>("btnGearAbout").Click += delegate { hide(); Welcome.ShowAbout(); };
            Ui.Find<Button>("btnGearUpdate").Click += delegate { hide(); UpdateCheck(false); };
            Ui.Find<Button>("btnProfExport").Click += delegate { hide(); ProfileUi.Export(); };
            Ui.Find<Button>("btnProfImport").Click += delegate { hide(); ProfileUi.Import(); };
            Ui.Find<FrameworkElement>("cardProfile").MouseLeftButtonUp += delegate { hide(); ProfileUi.EditProfile(); };
            TextBlock cur = Ui.Win.FindName("txtGearLangCur") as TextBlock;
            if (cur != null) { string nat = Lang.Name; foreach (string[] l in Lang.List) if (l[0] == Lang.Name) nat = l[2]; cur.Text = nat + "  \u203A"; }
            Ui.Find<Button>("btnGearLang").Click += delegate
            {
                if (lang.Visibility == Visibility.Visible) { lang.Visibility = Visibility.Collapsed; return; }
                StackPanel list = Ui.Find<StackPanel>("spLangList"); list.Children.Clear();
                int idx = 0;
                foreach (string[] l in Lang.List)
                {
                    Button b = new Button(); b.Tag = l[0]; b.Padding = new Thickness(12, 7, 12, 7); b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    try { b.Style = (Style)Ui.Win.FindResource("GearItemBtn"); } catch { }
                    DockPanel dp = new DockPanel();
                    FrameworkElement flag = Flags.Make(l[1], 26, 19, 0); flag.Margin = new Thickness(0, 0, 10, 0); flag.VerticalAlignment = VerticalAlignment.Center;
                    DockPanel.SetDock(flag, Dock.Left);
                    TextBlock mark = new TextBlock(); mark.Text = l[0] == Lang.Name ? "\u2713" : ""; mark.Foreground = Ui.Brush("#4FD1FF"); DockPanel.SetDock(mark, Dock.Right);
                    TextBlock t = new TextBlock(); t.Text = l[2];
                    dp.Children.Add(flag); dp.Children.Add(mark); dp.Children.Add(t); b.Content = dp;
                    b.Click += delegate(object x, RoutedEventArgs e) { hide(); SetLanguage((string)((Button)x).Tag); };
                    b.Opacity = 0;
                    DoubleAnimation fa = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(220))); fa.BeginTime = TimeSpan.FromMilliseconds(70 + idx * 32);
                    b.BeginAnimation(UIElement.OpacityProperty, fa);
                    list.Children.Add(b); idx++;
                }
                Anim.PopIn(lang, 1.0, 0.0);
            };
            Ui.Win.StateChanged += delegate { if (pop.IsOpen) hide(); };
            Ui.Win.LocationChanged += delegate { if (pop.IsOpen) hide(); };
            Ui.Win.SizeChanged += delegate { if (pop.IsOpen) hide(); };
            // clicks on the title bar / window frame never reach WPF: they come as non-client messages
            Action hook = delegate
            {
                try
                {
                    System.Windows.Interop.HwndSource hs = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(Ui.Win).Handle);
                    if (hs == null) return;
                    hs.AddHook(delegate(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
                    {
                        // WM_NCLBUTTONDOWN, WM_NCRBUTTONDOWN, WM_NCLBUTTONDBLCLK, WM_ENTERSIZEMOVE
                        if ((msg == 0xA1 || msg == 0xA4 || msg == 0xA3 || msg == 0x231) && pop.IsOpen) hide();
                        return IntPtr.Zero;
                    });
                }
                catch { }
            };
            if (new System.Windows.Interop.WindowInteropHelper(Ui.Win).Handle != IntPtr.Zero) hook();
            else Ui.Win.SourceInitialized += delegate { hook(); };
            Ui.Win.PreviewKeyDown += delegate(object x, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Escape && pop.IsOpen) hide(); };
        }

        // A menu item that was clicked keeps its "mouse is over" look (lighter box, label pushed right) because the menu is hidden
        // while the pointer is still on it, so WPF never runs the leave animation. Every item is put back to its resting look
        // when the menu closes and again before it opens.
        private static void ResetItems(DependencyObject root)
        {
            if (root == null) return;
            int n = 0; try { n = VisualTreeHelper.GetChildrenCount(root); } catch { return; }
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                Button b = c as Button;
                if (b != null && b.Template != null)
                {
                    try
                    {
                        FrameworkElement hl = b.Template.FindName("hl", b) as FrameworkElement;
                        ContentPresenter cp = b.Template.FindName("cp", b) as ContentPresenter;
                        if (hl != null) { hl.BeginAnimation(UIElement.OpacityProperty, null); hl.Opacity = 0; }
                        if (cp != null)
                        {
                            TranslateTransform tt = cp.RenderTransform as TranslateTransform;
                            if (tt != null) { tt.BeginAnimation(TranslateTransform.XProperty, null); tt.X = 0; }
                        }
                    }
                    catch { }
                }
                ResetItems(c);
            }
        }

        private static bool InsideOf(DependencyObject d, DependencyObject root)
        {
            while (d != null)
            {
                if (object.ReferenceEquals(d, root)) return true;
                DependencyObject p = null;
                try { if (d is Visual || d is System.Windows.Media.Media3D.Visual3D) p = VisualTreeHelper.GetParent(d); } catch { }
                if (p == null) { try { p = LogicalTreeHelper.GetParent(d); } catch { } }
                d = p;
            }
            return false;
        }

        public static void SetLanguage(string name)
        {
            if (name == Lang.Name) return;
            Work.Run(name == "English" || Lang.HasPack(name) ? "Switching language..." : "Downloading the " + name + " language pack...", delegate(WorkCtx c)
            {
                if (!Lang.HasPack(name))
                {
                    string err = Lang.Download(name);
                    if (err.Length > 0) { Ui.Warn("Could not download the " + name + " language pack.\n\n" + err + "\n\nCheck your internet connection and try again."); return; }
                }
                Ui.Settings.Set("Language", name); Ui.Settings.SaveNow();
                Ui.Post(delegate { ApplyThemeChange(); });
            });
        }

        // ---------------- update check (GitHub latest release) ----------------
        private static Version Parse(string v)
        {
            Match m = Regex.Match(v ?? "", @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return new Version(0, 0, 0);
            return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
        }

        // one update source: 0 = GitHub API, 1 = redirect of /releases/latest, 2 = raw VERSION file, 3 = releases.atom. Returns the tag or throws.
        private static string UpdSource(int i, ref string url)
        {
            string repo = "https://github.com/OwnerRock/RockToolbox";
            if (i == 0)
            {
                object r = Net.GetJson("https://api.github.com/repos/OwnerRock/RockToolbox/releases/latest", 15000);
                string tag = Net.Str(r, "tag_name"); string h = Net.Str(r, "html_url");
                if (tag.Length == 0) throw new InvalidOperationException("The GitHub API returned no release.");
                if (h.Length > 0) url = h;
                _assetUrl = "";
                try
                {
                    foreach (object a in Net.Arr(Net.Get(r, "assets")))
                    {
                        string n = Net.Str(a, "name"), du = Net.Str(a, "browser_download_url");
                        if (du.Length > 0 && n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { _assetUrl = du; break; }
                    }
                }
                catch { }
                return tag;
            }
            if (i == 1)
            {
                string loc = Net.GetRedirectLocation(repo + "/releases/latest", 15000);
                Match m = Regex.Match(loc, @"/releases/tag/([^/?#]+)");
                if (!m.Success) throw new InvalidOperationException("No release was found on GitHub.");
                string tag = Uri.UnescapeDataString(m.Groups[1].Value);
                url = repo + "/releases/tag/" + m.Groups[1].Value;
                return tag;
            }
            if (i == 2)
            {
                string txt = Net.GetString("https://raw.githubusercontent.com/OwnerRock/RockToolbox/main/VERSION", 15000);
                Match m = Regex.Match(txt ?? "", @"\d+\.\d+(?:\.\d+)?");
                if (!m.Success) throw new InvalidOperationException("The VERSION file is empty.");
                return m.Value;
            }
            else
            {
                string atom = Net.GetString("https://github.com/OwnerRock/RockToolbox/releases.atom", 15000);
                Match e = Regex.Match(atom ?? "", @"<entry>(.*?)</entry>", RegexOptions.Singleline);
                if (!e.Success) throw new InvalidOperationException("No release was found on GitHub.");
                string body = e.Groups[1].Value;
                Match l = Regex.Match(body, @"releases/tag/([^""'<>\s?#]+)");
                Match t = Regex.Match(body, @"<title>\s*([^<]*?)\s*</title>");
                string tag = "";
                if (l.Success) { tag = Uri.UnescapeDataString(l.Groups[1].Value); url = repo + "/releases/tag/" + l.Groups[1].Value; }
                else if (t.Success) tag = t.Groups[1].Value;
                else
                {
                    Match d = Regex.Match(body, @"<id>[^<]*?/(v?\d+\.\d+(?:\.\d+)?)\s*</id>");
                    if (d.Success) tag = d.Groups[1].Value;
                }
                if (!Regex.IsMatch(tag, @"\d+\.\d+")) throw new InvalidOperationException("No release was found on GitHub.");
                return tag;
            }
        }

        private static string _assetUrl = "";

        // silent = the check at start-up (only the red dot + a log line); otherwise the Check for updates window opens
        public static void UpdateCheck(bool silent)
        {
            if (!silent) { ShowUpdateWindow(); return; }
            CheckAsync(false, delegate(UpdResult r)
            {
                if (r.Tag.Length == 0) { Ui.Log.Warn("Could not check for updates: " + r.Err, "Update"); return; }
                if (ApplyResult(r)) Ui.Log.Info("A new version is available: " + r.Tag, "Update");
            });
        }

        // a newer version exists: download the installer inside the program (with progress) and start it, or open the page
        private static void OfferUpdate(string tag, string url)
        {
            string asset = _assetUrl;
            if (asset.Length == 0)
            {
                if (Dlg.Show(DlgKind.Info, "New version " + tag + "\n\nA new version of Rock Toolbox is available.\n\nOpen the download page?", null, "Open page", "Later")) { try { Process.Start(url); } catch { } }
                return;
            }
            if (!Dlg.Show(DlgKind.Info, "New version " + tag + "\n\nA new version of Rock Toolbox is available.\n\nDownload and install it now? Rock Toolbox will close and the installer will open.", null, "Update now", "Later")) return;
            string file = Path.Combine(Path.GetTempPath(), "RockToolbox-Update-" + tag.Trim('v', 'V') + ".exe");
            bool ok = false;
            Work.Run("Downloading the update...", delegate(WorkCtx c)
            {
                try
                {
                    Net.Download(asset, file, delegate(long done, long total) { if (total > 0) c.Progress(done * 100.0 / total, "Downloading the update..."); }, delegate { return Work.CancelRequested; });
                    ok = File.Exists(file) && new FileInfo(file).Length > 100000;
                    if (!ok) c.Error("The downloaded file is incomplete.", "Update");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { c.Error("Could not download the update: " + Net.FriendlyError(ex), "Update"); }
                if (!ok) return;
                Ui.Post(delegate
                {
                    try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); AppLife.Exit(); }
                    catch (Exception ex) { Ui.Log.Error("Could not start the installer: " + ex.Message, "Update"); Dlg.Error("Could not start the installer\n\n" + ex.Message + "\n\nIt was saved here: " + file); }
                });
            });
        }
    }
}
