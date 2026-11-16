using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RockToolbox.Native
{
    // Small animation helpers used by the welcome screen, the profile window and the About window.
    internal static class Fx
    {
        public static Duration Dur(int ms) { return new Duration(TimeSpan.FromMilliseconds(ms)); }

        private static CubicEase Out() { CubicEase e = new CubicEase(); e.EasingMode = EasingMode.EaseOut; return e; }

        // rises a little while fading in
        public static void FadeUp(UIElement el, int delayMs, double fromY)
        {
            TranslateTransform tt = new TranslateTransform(0, fromY);
            el.RenderTransform = tt;
            TimeSpan bt = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation ay = new DoubleAnimation(fromY, 0.0, Dur(520)); ay.EasingFunction = Out(); ay.BeginTime = bt;
            DoubleAnimation af = new DoubleAnimation(0.0, 1.0, Dur(520)); af.BeginTime = bt;
            el.Opacity = 0;
            tt.BeginAnimation(TranslateTransform.YProperty, ay);
            el.BeginAnimation(UIElement.OpacityProperty, af);
        }

        // springs in from small
        public static void Pop(UIElement el, int delayMs)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform sc = new ScaleTransform(0.4, 0.4);
            el.RenderTransform = sc;
            BackEase ease = new BackEase(); ease.EasingMode = EasingMode.EaseOut; ease.Amplitude = 0.7;
            TimeSpan bt = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation a = new DoubleAnimation(0.4, 1.0, Dur(650)); a.EasingFunction = ease; a.BeginTime = bt;
            DoubleAnimation f = new DoubleAnimation(0.0, 1.0, Dur(320)); f.BeginTime = bt;
            el.Opacity = 0;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            el.BeginAnimation(UIElement.OpacityProperty, f);
        }

        // an expanding, fading ring (times <= 0 = forever)
        public static void Ring(UIElement el, int times, double from, double to, double op, int delayMs)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform sc = new ScaleTransform(from, from);
            el.RenderTransform = sc;
            RepeatBehavior rep = times <= 0 ? RepeatBehavior.Forever : new RepeatBehavior((double)times);
            TimeSpan bt = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation a = new DoubleAnimation(from, to, Dur(1300)); a.RepeatBehavior = rep; a.BeginTime = bt;
            DoubleAnimation o = new DoubleAnimation(op, 0.0, Dur(1300)); o.RepeatBehavior = rep; o.BeginTime = bt;
            el.Opacity = 0;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            el.BeginAnimation(UIElement.OpacityProperty, o);
        }

        public static void Bounce(UIElement el)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform sc = new ScaleTransform(0.78, 0.78);
            el.RenderTransform = sc;
            BackEase ease = new BackEase(); ease.EasingMode = EasingMode.EaseOut; ease.Amplitude = 1.1;
            DoubleAnimation a = new DoubleAnimation(0.78, 1.0, Dur(480)); a.EasingFunction = ease;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        public static void Spin(UIElement el)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            RotateTransform rt = new RotateTransform(0);
            el.RenderTransform = rt;
            DoubleAnimation a = new DoubleAnimation(0.0, 360.0, Dur(1100)); a.RepeatBehavior = RepeatBehavior.Forever;
            rt.BeginAnimation(RotateTransform.AngleProperty, a);
        }

        // a slow back-and-forth drift (background glows)
        public static void Drift(FrameworkElement el, double dx, double dy, int ms)
        {
            TranslateTransform tt = new TranslateTransform(0, 0);
            el.RenderTransform = tt;
            SineEase ease = new SineEase(); ease.EasingMode = EasingMode.EaseInOut;
            DoubleAnimation ax = new DoubleAnimation(0.0, dx, Dur(ms)); ax.AutoReverse = true; ax.RepeatBehavior = RepeatBehavior.Forever; ax.EasingFunction = ease;
            DoubleAnimation ay = new DoubleAnimation(0.0, dy, Dur((int)(ms * 1.3))); ay.AutoReverse = true; ay.RepeatBehavior = RepeatBehavior.Forever; ay.EasingFunction = ease;
            tt.BeginAnimation(TranslateTransform.XProperty, ax);
            tt.BeginAnimation(TranslateTransform.YProperty, ay);
        }

        // photo -> brush for the round avatar
        public static BitmapSource LoadBitmap(string path)
        {
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache; bi.DecodePixelWidth = 256;
                bi.UriSource = new Uri(path); bi.EndInit(); bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public static void PaintAvatar(System.Windows.Shapes.Shape el, UIElement letter, BitmapSource bmp)
        {
            if (bmp != null)
            {
                ImageBrush ib = new ImageBrush(bmp); ib.Stretch = Stretch.UniformToFill;
                el.Fill = ib; letter.Visibility = Visibility.Collapsed;
            }
            else
            {
                el.Fill = Ui.Brush("#B8C0CC"); letter.Visibility = Visibility.Visible;
            }
        }

        public static string SaveAvatar(BitmapSource bmp, string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            string png = Path.Combine(dataDir, "avatar.png");
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (FileStream fs = File.Create(png)) enc.Save(fs);
            return png;
        }

        public static BitmapSource PickPhoto(Window owner)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"; dlg.Title = "Choose your photo";
                if (dlg.ShowDialog(owner) == true) return LoadBitmap(dlg.FileName);
            }
            catch { }
            return null;
        }
    }

    // First-run welcome screen (own window, shown before the main window): greeting, language, look, profile.
    // Also holds the profile window and the About window (same design).
    internal sealed class Welcome
    {
        public const string Version = "4.1.0";
        private static readonly string[] Pages = { "Welcome", "Lang", "Look", "Profile", "Done", "Restart" };
        private static readonly string[] Greets =
        {
            "Hello", "مرحباً", "Bonjour", "Hola", "你好", "こんにちは",
            "Привет", "नमस्ते", "Hallo", "Merhaba"
        };

        private Window _w;
        private AppSettings _s;
        private string _dataDir;
        private string _cur = "";
        private string _lang = "English", _look = "Modern";
        private bool _dark, _busy, _finished, _greetDone, _greetFlip;
        private BitmapSource _avatar;
        private int _greetIdx;
        private string _greetLast = "";
        private DispatcherTimer _greetTimer;
        private readonly List<Button> _langButtons = new List<Button>();

        private T F<T>(string name) where T : class
        {
            T t = _w.FindName(name) as T;
            if (t == null) throw new InvalidOperationException("Welcome UI control not found: " + name);
            return t;
        }

        // returns true when the user went through all the steps (settings are saved)
        public static bool Run(AppSettings s, string root, string dataDir)
        {
            Welcome w = new Welcome();
            try { return w.Execute(s, root, dataDir); }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(dataDir, "error.log"), DateTime.Now.ToString("s") + "  Welcome screen: " + ex + "\r\n"); } catch { }
                return false;
            }
        }

        private bool Execute(AppSettings s, string root, string dataDir)
        {
            _s = s; _dataDir = dataDir;
            _w = XamlLoader.LoadWindow(root, "ui/Welcome.xaml");
            string l = s.GetString("Language"); if (l.Length > 0) _lang = l;
            _look = s.GetString("UiStyle") == "Classic" ? "Classic" : "Modern";
            _dark = s.GetString("Theme") == "Dark";
            string ap = s.GetString("Avatar");
            if (ap.Length > 0 && File.Exists(ap)) _avatar = Fx.LoadBitmap(ap);
            F<TextBlock>("wVer").Text = "V" + Version;
            string un = s.GetString("UserName").Trim();
            F<TextBox>("wName").Text = un.Length > 0 ? un : Environment.UserName;

            BuildLanguageIcons();
            UpdateLangChips();
            UpdateLook();
            UpdateAvatars();
            Wire();

            _w.Loaded += delegate
            {
                // wait until WPF has finished laying out and drawing, then start the animations (smooth from the first frame)
                _w.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate
                {
                    Fx.Drift(F<System.Windows.Shapes.Ellipse>("glowA"), 70, 50, 9000);
                    Fx.Drift(F<System.Windows.Shapes.Ellipse>("glowB"), -80, -60, 11000);
                    Enter("Welcome");
                }));
            };
            _w.ShowDialog();
            try { if (_greetTimer != null) _greetTimer.Stop(); } catch { }
            return _finished;
        }

        // ---------------- building / wiring ----------------
        private void BuildLanguageIcons()
        {
            Panel grid = F<Panel>("wLangGrid");
            Style st = (Style)_w.FindResource("WizIcon");
            foreach (string[] lg in Lang.List)
            {
                Button b = new Button();
                b.Style = st; b.Tag = lg[0];
                StackPanel sp = new StackPanel();
                Grid g = new Grid(); g.Width = 62; g.Height = 46;
                FrameworkElement flag = Flags.Make(lg[1], 62, 46, 3);
                Border ck = new Border();
                ck.Width = 22; ck.Height = 22; ck.CornerRadius = new CornerRadius(11); ck.Background = Brushes.White;
                ck.HorizontalAlignment = HorizontalAlignment.Right; ck.VerticalAlignment = VerticalAlignment.Top;
                ck.Margin = new Thickness(0, -5, -5, 0); ck.Visibility = Visibility.Collapsed;
                TextBlock ct = new TextBlock();
                ct.Text = "✓"; ct.FontSize = 13; ct.FontWeight = FontWeights.Bold; ct.Foreground = Ui.Brush("#FF375F");
                ct.HorizontalAlignment = HorizontalAlignment.Center; ct.VerticalAlignment = VerticalAlignment.Center;
                ck.Child = ct;
                g.Children.Add(flag); g.Children.Add(ck);
                TextBlock lb = new TextBlock();
                lb.Text = lg[2]; lb.FontSize = 12; lb.Margin = new Thickness(0, 7, 0, 0);
                lb.HorizontalAlignment = HorizontalAlignment.Center; lb.TextTrimming = TextTrimming.CharacterEllipsis; lb.MaxWidth = 84;
                sp.Children.Add(g); sp.Children.Add(lb);
                b.Content = sp;
                b.Click += delegate(object s, RoutedEventArgs e)
                {
                    if (_busy) return;
                    Button bb = (Button)s;
                    _lang = (string)bb.Tag; UpdateLangChips();
                    Fx.Bounce(((StackPanel)bb.Content).Children[0]);
                    F<TextBlock>("wLangStatus").Text = "";
                };
                _langButtons.Add(b);
                grid.Children.Add(b);
            }
        }

        private void UpdateLangChips()
        {
            foreach (Button b in _langButtons)
            {
                StackPanel sp = (StackPanel)b.Content;
                Grid g = (Grid)sp.Children[0];
                Border ic = (Border)g.Children[0]; Border ck = (Border)g.Children[1]; TextBlock lb = (TextBlock)sp.Children[1];
                bool on = (string)b.Tag == _lang;
                ic.BorderBrush = on ? Ui.Brush("#F2F4FF") : Brushes.Transparent;
                ck.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                lb.Foreground = on ? Ui.Brush("#FF375F") : Ui.Brush("#9AA0B8");
            }
        }

        private void UpdateLook()
        {
            Selector.SetIsSelected(F<Button>("wCardClassic"), _look == "Classic");
            Selector.SetIsSelected(F<Button>("wCardModern"), _look == "Modern");
            Selector.SetIsSelected(F<Button>("wChipLight"), !_dark);
            Selector.SetIsSelected(F<Button>("wChipDark"), _dark);
        }

        private void UpdateAvatars()
        {
            Fx.PaintAvatar(F<System.Windows.Shapes.Shape>("wAvEl"), F<UIElement>("wAvLetter"), _avatar);
            Fx.PaintAvatar(F<System.Windows.Shapes.Shape>("wDoneAv"), F<UIElement>("wDoneLetter"), _avatar);
        }

        private void Wire()
        {
            F<Button>("wBtnClose").Click += delegate { _w.Close(); };
            F<Button>("wBtnStart").Click += delegate { Go("Lang"); };
            F<FrameworkElement>("wGreetHost").MouseLeftButtonUp += delegate { if (!_greetDone) FinishGreet(); };
            F<Button>("wBtnLangNext").Click += delegate { LangNext(); };
            RoutedEventHandler pick = delegate(object s, RoutedEventArgs e)
            {
                _look = (string)((Button)s).Tag; UpdateLook();
            };
            F<Button>("wCardClassic").Click += pick; F<Button>("wCardModern").Click += pick;
            RoutedEventHandler mode = delegate(object s, RoutedEventArgs e)
            {
                _dark = (string)((Button)s).Tag == "Dark"; UpdateLook();
            };
            F<Button>("wChipLight").Click += mode; F<Button>("wChipDark").Click += mode;
            F<Button>("wBtnLookNext").Click += delegate { Go("Profile"); };

            F<FrameworkElement>("wAvatarGrid").MouseLeftButtonUp += delegate
            {
                BitmapSource bmp = Fx.PickPhoto(_w);
                if (bmp != null) { _avatar = bmp; UpdateAvatars(); Fx.Pop(F<UIElement>("wAvatarGrid"), 0); }
            };
            F<TextBox>("wName").TextChanged += delegate { F<TextBlock>("wNameErr").Text = ""; };
            F<TextBox>("wName").KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Return) { e.Handled = true; ProfileNext(); } };
            F<Button>("wBtnProfNext").Click += delegate { ProfileNext(); };
            F<Button>("wBtnFinish").Click += delegate { Finish(); };
        }

        private void LangNext()
        {
            if (_busy) return;
            string lang = _lang;
            if (lang != "English" && !Lang.HasPack(lang))
            {
                _busy = true;
                TextBlock status = F<TextBlock>("wLangStatus");
                ProgressBar pb = F<ProgressBar>("wLangPb");
                status.Text = "Downloading the " + lang + " language pack from the official source...";
                pb.Visibility = Visibility.Visible;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string err = "";
                    try { err = Lang.Download(lang); } catch (Exception ex) { err = ex.Message; }
                    _w.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _busy = false; pb.Visibility = Visibility.Collapsed;
                        if (err.Length > 0)
                        {
                            if (err.Length > 110) err = err.Substring(0, 110) + "...";
                            status.Text = "Could not download the " + lang + " pack. Check your internet connection and try again, or choose another language.\n(" + err + ")";
                            return;
                        }
                        status.Text = "";
                        Go("Look");
                    }));
                });
                return;
            }
            Go("Look");
        }

        private void ProfileNext()
        {
            string nm = F<TextBox>("wName").Text.Trim();
            if (nm.Length == 0) { F<TextBlock>("wNameErr").Text = "Enter your name first"; return; }
            F<TextBlock>("wDoneTitle").Text = "Welcome, " + nm;
            UpdateAvatars();
            Go("Done");
        }

        private void Finish()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                _s.Set("UserName", F<TextBox>("wName").Text.Trim());
                if (_avatar != null) { try { _s.Set("Avatar", Fx.SaveAvatar(_avatar, _dataDir)); } catch { } }
                _s.Set("Language", _lang);
                _s.Set("UiStyle", _look);
                _s.Set("Theme", _dark ? "Dark" : "Light");
                _s.Set("WelcomeSeen", Version);
                _s.SaveNow();
            }
            catch { }
            Go("Restart");
            ProgressBar pb = F<ProgressBar>("wRestartPb");
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(40);
            t.Tick += delegate
            {
                pb.Value = Math.Min(100, pb.Value + 4);
                if (pb.Value >= 100) { t.Stop(); _finished = true; _w.Close(); }
            };
            t.Start();
        }

        // ---------------- pages ----------------
        private void Go(string name)
        {
            if (_cur.Length > 0 && _cur != name)
            {
                FrameworkElement old = F<FrameworkElement>("pg" + _cur);
                if (old.Visibility == Visibility.Visible)
                {
                    DoubleAnimation fo = new DoubleAnimation(1.0, 0.0, Fx.Dur(150));
                    fo.Completed += delegate
                    {
                        old.BeginAnimation(UIElement.OpacityProperty, null);
                        old.Opacity = 1.0; old.Visibility = Visibility.Collapsed;
                        Enter(name);
                    };
                    old.BeginAnimation(UIElement.OpacityProperty, fo);
                    return;
                }
            }
            Enter(name);
        }

        private void Enter(string name)
        {
            foreach (string p in Pages) F<FrameworkElement>("pg" + p).Visibility = Visibility.Collapsed;
            Panel pg = F<Panel>("pg" + name);
            pg.BeginAnimation(UIElement.OpacityProperty, null);
            pg.Opacity = 1.0;
            pg.Visibility = Visibility.Visible;
            _cur = name;
            if (name == "Welcome")
            {
                for (int k = 1; k < pg.Children.Count; k++) pg.Children[k].Opacity = 0;
                StartGreet();
            }
            else
            {
                int i = 0;
                foreach (UIElement c in pg.Children)
                {
                    FrameworkElement fe = c as FrameworkElement;
                    if (fe != null && "pop".Equals(fe.Tag as string)) Fx.Pop(c, 80 + i * 110); else Fx.FadeUp(c, 80 + i * 90, 18);
                    i++;
                }
            }
            try { F<UIElement>("wStartGlow").BeginAnimation(UIElement.OpacityProperty, null); F<UIElement>("wStartGlow").Opacity = 0; } catch { }
            if (name == "Lang")
            {
                int ci = 0;
                foreach (UIElement b in F<Panel>("wLangGrid").Children) { Fx.Pop(b, 260 + ci * 55); ci++; }
            }
            if (name == "Look")
            {
                int ci = 0;
                foreach (FrameworkElement ug in pg.Children)
                {
                    Panel inner = ug as UniformGrid;
                    if (inner == null) continue;
                    foreach (UIElement card in inner.Children) { Fx.Pop(card, 220 + ci * 140); ci++; }
                }
            }
            if (name == "Done") Fx.Ring(F<UIElement>("wDoneRing"), 3, 0.92, 1.3, 0.8, 0);
            if (name == "Restart") Fx.Spin(F<UIElement>("wSpin"));
            UpdateDots(name);
        }

        private void UpdateDots(string name)
        {
            Panel sp = F<Panel>("spDots");
            int idx = Array.IndexOf(Pages, name);
            if (idx < 0 || idx > 4) { sp.Visibility = Visibility.Collapsed; return; }
            sp.Visibility = Visibility.Visible;
            if (sp.Children.Count == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    Border d = new Border();
                    d.Height = 8; d.Width = 8; d.Margin = new Thickness(4, 0, 4, 0); d.CornerRadius = new CornerRadius(4);
                    sp.Children.Add(d);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                Border d = (Border)sp.Children[i];
                if (i == idx)
                {
                    d.Background = Ui.Brush("#FF375F");
                    DoubleAnimation wa = new DoubleAnimation(8.0, 24.0, Fx.Dur(280));
                    d.BeginAnimation(FrameworkElement.WidthProperty, wa);
                }
                else
                {
                    d.BeginAnimation(FrameworkElement.WidthProperty, null);
                    d.Width = 8; d.Background = Ui.Brush("#2B3038");
                }
            }
        }

        // ---------------- the iPhone-style greeting ----------------
        private void GreetPrep(TextBlock tb)
        {
            tb.RenderTransformOrigin = new Point(0.5, 0.5);
            TransformGroup g = new TransformGroup();
            g.Children.Add(new ScaleTransform(1, 1));
            g.Children.Add(new TranslateTransform(0, 0));
            tb.RenderTransform = g;
            tb.BeginAnimation(UIElement.OpacityProperty, null);
            tb.Opacity = 0;
        }

        private void GreetShow(string text)
        {
            TextBlock a = F<TextBlock>("wGreetA"), b = F<TextBlock>("wGreetB");
            TextBlock inTb = _greetFlip ? b : a, outTb = _greetFlip ? a : b;
            bool first = _greetLast.Length == 0;
            _greetFlip = !_greetFlip;
            _greetLast = text;
            inTb.Text = text;
            CubicEase ease = new CubicEase(); ease.EasingMode = EasingMode.EaseOut;
            TransformGroup gi = (TransformGroup)inTb.RenderTransform;
            DoubleAnimation sa = new DoubleAnimation(0.86, 1.0, Fx.Dur(440)); sa.EasingFunction = ease;
            DoubleAnimation ya = new DoubleAnimation(18.0, 0.0, Fx.Dur(440)); ya.EasingFunction = ease;
            DoubleAnimation oa = new DoubleAnimation(0.0, 1.0, Fx.Dur(380));
            gi.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, sa);
            gi.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, sa);
            gi.Children[1].BeginAnimation(TranslateTransform.YProperty, ya);
            inTb.BeginAnimation(UIElement.OpacityProperty, oa);
            if (!first)
            {
                TransformGroup go = (TransformGroup)outTb.RenderTransform;
                DoubleAnimation sb = new DoubleAnimation(1.0, 1.14, Fx.Dur(340)); sb.EasingFunction = ease;
                DoubleAnimation yb = new DoubleAnimation(0.0, -12.0, Fx.Dur(340)); yb.EasingFunction = ease;
                DoubleAnimation ob = new DoubleAnimation(1.0, 0.0, Fx.Dur(300));
                go.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, sb);
                go.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, sb);
                go.Children[1].BeginAnimation(TranslateTransform.YProperty, yb);
                outTb.BeginAnimation(UIElement.OpacityProperty, ob);
            }
        }

        private void StartGreet()
        {
            _greetIdx = 0; _greetFlip = false; _greetDone = false; _greetLast = "";
            GreetPrep(F<TextBlock>("wGreetA")); GreetPrep(F<TextBlock>("wGreetB"));
            GreetShow(Greets[0]); _greetIdx = 1;
            if (_greetTimer != null) _greetTimer.Stop();
            _greetTimer = new DispatcherTimer();
            _greetTimer.Interval = TimeSpan.FromMilliseconds(430);
            _greetTimer.Tick += delegate
            {
                if (_greetIdx >= Greets.Length) { FinishGreet(); return; }
                GreetShow(Greets[_greetIdx]);
                _greetIdx++;
            };
            _greetTimer.Start();
        }

        private void FinishGreet()
        {
            if (_greetDone) return;
            _greetDone = true;
            try { if (_greetTimer != null) _greetTimer.Stop(); } catch { }
            if (_greetLast != "Welcome") GreetShow("Welcome");
            Fx.FadeUp(F<UIElement>("wSub"), 120, 18);
            Fx.Pop(F<UIElement>("wVerPill"), 260);
            Fx.FadeUp(F<UIElement>("wStartHost"), 420, 18);
            Fx.Ring(F<UIElement>("wStartGlow"), 0, 1.0, 1.3, 0.55, 1400);
        }

        // ================= profile window (gear menu > profile card) =================
        public static void ShowProfile()
        {
            Window w;
            try { w = XamlLoader.LoadWindow(Ui.Root, "ui/Profile.xaml"); }
            catch (Exception ex) { Ui.Log.Error("Could not open the profile window: " + ex.Message, "Profile"); return; }
            w.Owner = Ui.Win;
            if (Lang.Rtl) w.FlowDirection = FlowDirection.RightToLeft;
            TextBox name = (TextBox)w.FindName("pName");
            TextBlock err = (TextBlock)w.FindName("pErr");
            System.Windows.Shapes.Shape av = (System.Windows.Shapes.Shape)w.FindName("pAvEl");
            UIElement letter = (UIElement)w.FindName("pAvLetter");
            FrameworkElement grid = (FrameworkElement)w.FindName("pAvatarGrid");
            BitmapSource bmp = null;
            string ap = Ui.Settings.GetString("Avatar");
            if (ap.Length > 0 && File.Exists(ap)) bmp = Fx.LoadBitmap(ap);
            name.Text = ProfileUi.UserName();
            Fx.PaintAvatar(av, letter, bmp);
            name.TextChanged += delegate { err.Text = ""; };
            ((Button)w.FindName("pBtnClose")).Click += delegate { w.Close(); };
            grid.MouseLeftButtonUp += delegate
            {
                BitmapSource b = Fx.PickPhoto(w);
                if (b != null) { bmp = b; Fx.PaintAvatar(av, letter, bmp); Fx.Pop(grid, 0); }
            };
            Action save = delegate
            {
                string nm = name.Text.Trim();
                if (nm.Length == 0) { err.Text = "Enter your name first"; return; }
                Ui.Settings.Set("UserName", nm);
                if (bmp != null)
                {
                    try
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RockToolbox");
                        Ui.Settings.Set("Avatar", Fx.SaveAvatar(bmp, dir));
                    }
                    catch (Exception ex) { Ui.Log.Warn("Could not save the profile photo: " + ex.Message, "Profile"); }
                }
                Ui.Settings.SaveNow();
                ProfileUi.UpdateCard();
                w.Close();
            };
            ((Button)w.FindName("pSave")).Click += delegate { save(); };
            name.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Return) { e.Handled = true; save(); } };
            w.Loaded += delegate
            {
                Panel root = (Panel)w.FindName("pRoot");
                int i = 0;
                foreach (UIElement c in root.Children)
                {
                    FrameworkElement fe = c as FrameworkElement;
                    if (fe != null && "pop".Equals(fe.Tag as string)) Fx.Pop(c, 60 + i * 100); else Fx.FadeUp(c, 60 + i * 80, 16);
                    i++;
                }
            };
            w.ShowDialog();
        }

        // ================= About window =================
        private static string WindowsText()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        string product = Convert.ToString(k.GetValue("ProductName"));
                        string build = Convert.ToString(k.GetValue("CurrentBuildNumber"));
                        string dv = Convert.ToString(k.GetValue("DisplayVersion"));
                        int b; int.TryParse(build, out b);
                        if (b >= 22000 && product.StartsWith("Windows 10")) product = "Windows 11" + product.Substring(10);
                        string t = "Microsoft " + product;
                        if (dv.Length > 0) t += " " + dv;
                        return t + " (build " + build + ", " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") + ")";
                    }
                }
            }
            catch { }
            return "Windows";
        }

        public static void ShowAbout()
        {
            Window w;
            try { w = XamlLoader.LoadWindow(Ui.Root, "ui/About.xaml"); }
            catch (Exception ex) { Ui.Log.Error("Could not open the About window: " + ex.Message, "About"); return; }
            w.Owner = Ui.Win;
            if (Lang.Rtl) w.FlowDirection = FlowDirection.RightToLeft;
            ((TextBlock)w.FindName("aboutVersion")).Text = "V" + SettingsPage.Version;
            ((TextBlock)w.FindName("aboutWindows")).Text = WindowsText();
            TextBlock cred = (TextBlock)w.FindName("aboutCredits");
            cred.MouseEnter += delegate { cred.TextDecorations = TextDecorations.Underline; };
            cred.MouseLeave += delegate { cred.TextDecorations = null; };
            cred.MouseLeftButtonUp += delegate
            {
                try { Process.Start("https://t.me/OwnerRock"); }
                catch (Exception ex) { Ui.Log.Error("Could not open the link: " + ex.Message, "About"); }
            };
            ((Button)w.FindName("btnAboutClose")).Click += delegate { w.Close(); };
            ((Button)w.FindName("btnTitleClose")).Click += delegate { w.Close(); };
            w.Loaded += delegate
            {
                Border root = (Border)w.FindName("aboutRoot");
                Panel sp = root.Child as Panel;
                if (sp == null) return;
                int i = 0;
                foreach (UIElement c in sp.Children) { Fx.FadeUp(c, 40 + i * 90, 16); i++; }
                Fx.Pop((UIElement)w.FindName("aboutLogo"), 40);
            };
            w.ShowDialog();
        }
    }
}
