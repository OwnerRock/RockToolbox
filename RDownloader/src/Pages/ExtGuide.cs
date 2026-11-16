using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace RDownloader.Native
{
    // the window that shows how to add the R-Downloader extension to Edge / Chrome / Brave / Opera / Firefox
    internal static class ExtGuide
    {
        private static Window _w;

        public static string Folder(bool firefox)
        {
            return Path.Combine(Path.Combine(Ui.Root, "extension"), firefox ? "firefox" : "chromium");
        }

        // path of a browser exe (registry "App Paths", then the usual folders); "" if it is not installed
        private static string BrowserExe(string key)
        {
            string exe = key + ".exe";
            try
            {
                foreach (string root in new string[] { @"HKEY_LOCAL_MACHINE", @"HKEY_CURRENT_USER" })
                {
                    object v = Microsoft.Win32.Registry.GetValue(root + @"\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe, "", null);
                    string p = v as string;
                    if (!string.IsNullOrEmpty(p)) { p = p.Trim('"'); if (File.Exists(p)) return p; }
                }
            }
            catch (Exception) { }
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] guess;
            switch (key)
            {
                case "msedge": guess = new string[] { Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe") }; break;
                case "chrome": guess = new string[] { Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"), Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"), Path.Combine(la, @"Google\Chrome\Application\chrome.exe") }; break;
                case "brave": guess = new string[] { Path.Combine(pf, @"BraveSoftware\Brave-Browser\Application\brave.exe"), Path.Combine(la, @"BraveSoftware\Brave-Browser\Application\brave.exe") }; break;
                case "opera": guess = new string[] { Path.Combine(la, @"Programs\Opera\opera.exe"), Path.Combine(la, @"Programs\Opera\launcher.exe"), Path.Combine(pf, @"Opera\opera.exe") }; break;
                default: guess = new string[] { Path.Combine(pf, @"Mozilla Firefox\firefox.exe"), Path.Combine(pf86, @"Mozilla Firefox\firefox.exe") }; break;
            }
            foreach (string g in guess) if (File.Exists(g)) return g;
            return "";
        }

        // opens a browser page without any console window; started as a normal (not administrator) user so it joins the browser you already have open
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        private static ImageSource BrowserIcon(string exe)
        {
            try
            {
                using (System.Drawing.Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    if (ic == null) return null;
                    System.Windows.Media.Imaging.BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze(); return bs;
                }
            }
            catch (Exception) { return null; }
        }

        private static readonly string[] IconNames = { "edge", "chrome", "firefox", "opera", "brave" };

        // the browser's official icon for a browser that is not installed: from the cache, or downloaded once in the background
        private static void OfficialIcon(string key, Grid badge)
        {
            string dn = key == "msedge" ? "edge" : key;
            string file = Path.Combine(Path.Combine(UiHost.DataDir, "icons"), dn + ".png");
            Action<string> show = delegate(string f)
            {
                try
                {
                    System.Windows.Media.Imaging.BitmapImage bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit(); bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bi.UriSource = new Uri(f); bi.DecodePixelWidth = 96; bi.EndInit(); bi.Freeze();
                    Image im = new Image(); im.Source = bi; im.Width = 40; im.Height = 40; im.Stretch = Stretch.Uniform; im.Opacity = 0.9;
                    RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                    badge.Children.Clear(); badge.Children.Add(im);
                }
                catch (Exception) { }
            };
            if (File.Exists(file) && new FileInfo(file).Length > 200) { show(file); return; }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    string[] urls = {
                        "https://cdn.jsdelivr.net/gh/alrra/browser-logos@main/src/" + dn + "/" + dn + "_128x128.png",
                        "https://raw.githubusercontent.com/alrra/browser-logos/main/src/" + dn + "/" + dn + "_128x128.png",
                        "https://cdnjs.cloudflare.com/ajax/libs/browser-logos/74.1.0/" + dn + "/" + dn + "_128x128.png" };
                    foreach (string u in urls)
                    {
                        try
                        {
                            byte[] b = Net.GetBytes(u, 10000);
                            if (b != null && b.Length > 500 && b[1] == 0x50 && b[2] == 0x4E) { File.WriteAllBytes(file, b); break; }
                        }
                        catch (Exception) { }
                    }
                    if (File.Exists(file)) Ui.Post(delegate { show(file); });
                }
                catch (Exception) { }
            });
        }

        private static System.Windows.Shapes.Path Pie(double a0, double a1, Brush fill, double r, double cx, double cy)
        {
            Func<double, Point> pt = delegate(double a) { double rad = a * Math.PI / 180; return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad)); };
            PathFigure f = new PathFigure(); f.StartPoint = new Point(cx, cy); f.IsClosed = true;
            f.Segments.Add(new LineSegment(pt(a0), true));
            f.Segments.Add(new ArcSegment(pt(a1), new Size(r, r), 0, a1 - a0 > 180, SweepDirection.Clockwise, true));
            PathGeometry g = new PathGeometry(); g.Figures.Add(f);
            System.Windows.Shapes.Path p = new System.Windows.Shapes.Path(); p.Data = g; p.Fill = fill; return p;
        }

        private static Brush Hex(string h) { return (Brush)new BrushConverter().ConvertFromString(h); }

        // simple drawn browser marks for browsers that are not installed (an installed browser shows its own icon)
        private static FrameworkElement VectorLogo(string key)
        {
            Canvas c = new Canvas(); c.Width = 100; c.Height = 100;
            Func<double, double, double, double, Brush, System.Windows.Shapes.Ellipse> el = delegate(double x, double y, double w, double h, Brush b)
            {
                System.Windows.Shapes.Ellipse e = new System.Windows.Shapes.Ellipse(); e.Width = w; e.Height = h; e.Fill = b; Canvas.SetLeft(e, x); Canvas.SetTop(e, y); return e;
            };
            switch (key)
            {
                case "chrome":
                    c.Children.Add(Pie(150, 270, Hex("#34A853"), 50, 50, 50)); c.Children.Add(Pie(270, 30, Hex("#EA4335"), 50, 50, 50)); c.Children.Add(Pie(30, 150, Hex("#FBBC05"), 50, 50, 50));
                    c.Children.Add(el(27, 27, 46, 46, Brushes.White)); c.Children.Add(el(34, 34, 32, 32, Hex("#4285F4")));
                    break;
                case "msedge":
                    {
                        LinearGradientBrush g = new LinearGradientBrush(Colors.RoyalBlue, Colors.MediumSeaGreen, 45);
                        g.GradientStops[0].Color = (Color)ColorConverter.ConvertFromString("#0C59A4"); g.GradientStops[1].Color = (Color)ColorConverter.ConvertFromString("#35D0A0");
                        c.Children.Add(el(2, 2, 96, 96, g)); c.Children.Add(el(30, 30, 52, 40, Hex("#1B6FC0"))); c.Children.Add(el(36, 38, 46, 28, Hex("#35D0A0")));
                        break;
                    }
                case "firefox":
                    {
                        LinearGradientBrush g = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#FFB32B"), (Color)ColorConverter.ConvertFromString("#7B3FE4"), 70);
                        c.Children.Add(el(4, 4, 92, 92, g)); c.Children.Add(el(26, 22, 56, 56, Hex("#FF7139"))); c.Children.Add(el(38, 12, 54, 54, Hex("#7B3FE4")));
                        c.Children.Add(el(30, 34, 36, 36, Hex("#FFB32B")));
                        break;
                    }
                case "opera":
                    c.Children.Add(el(2, 2, 96, 96, Hex("#FF1B2D"))); c.Children.Add(el(28, 12, 44, 76, Brushes.White)); c.Children.Add(el(36, 22, 28, 56, Hex("#FF1B2D")));
                    break;
                case "brave":
                    {
                        PathGeometry pg = new PathGeometry();
                        PathFigure f = new PathFigure(); f.StartPoint = new Point(30, 6); f.IsClosed = true;
                        foreach (double[] q in new double[][] { new double[] { 70, 6 }, new double[] { 92, 24 }, new double[] { 84, 68 }, new double[] { 50, 94 }, new double[] { 16, 68 }, new double[] { 8, 24 } }) f.Segments.Add(new LineSegment(new Point(q[0], q[1]), true));
                        pg.Figures.Add(f);
                        System.Windows.Shapes.Path sh = new System.Windows.Shapes.Path(); sh.Data = pg; sh.Fill = Hex("#FB542B"); c.Children.Add(sh);
                        TextBlock b = new TextBlock(); b.Text = "B"; b.FontSize = 46; b.FontWeight = FontWeights.Bold; b.Foreground = Brushes.White; Canvas.SetLeft(b, 31); Canvas.SetTop(b, 18); c.Children.Add(b);
                        break;
                    }
                default: return null;
            }
            Viewbox vb = new Viewbox(); vb.Width = 40; vb.Height = 40; vb.Child = c; return vb;
        }

        // opens the browser AND its extensions page. Browsers refuse edge:// / chrome:// addresses given on the command line
        // when they are already running, so after starting it the address is typed into the focused browser window.
        private static void OpenInBrowser(string key, string url)
        {
            string exe = BrowserExe(key);
            if (exe.Length == 0) { Dlg.Warn("This browser was not found on this PC\n\nInstall it, or open " + url + " in it by hand."); return; }
            string procName = System.IO.Path.GetFileNameWithoutExtension(exe);
            try
            {
                System.Diagnostics.ProcessStartInfo si = new System.Diagnostics.ProcessStartInfo(exe, key == "firefox" ? "-new-tab \"" + url + "\"" : "--new-window \"" + url + "\"");
                si.UseShellExecute = false;
                System.Diagnostics.Process.Start(si);
            }
            catch (Exception ex) { Ui.Log.Warn("Could not open the browser: " + ex.Message, "Extension"); return; }
            if (key == "firefox") return;       // about:debugging works from the command line
            DispatcherTimer t = new DispatcherTimer(); t.Interval = TimeSpan.FromMilliseconds(2200);
            t.Tick += delegate
            {
                t.Stop();
                try
                {
                    uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                    System.Diagnostics.Process fg = System.Diagnostics.Process.GetProcessById((int)pid);
                    string fn = fg.ProcessName;
                    bool ours = fn.Equals(procName, StringComparison.OrdinalIgnoreCase) || (key == "opera" && fn.IndexOf("opera", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!ours) return;
                    System.Windows.Forms.SendKeys.SendWait("^l");
                    System.Threading.Thread.Sleep(150);
                    System.Windows.Forms.SendKeys.SendWait(url + "{ENTER}");
                }
                catch (Exception ex) { Ui.Log.Warn("Could not open the extensions page by itself: " + ex.Message + " - type " + url + " in the address bar.", "Extension"); }
            };
            t.Start();
        }

        // the Downloads page calls this when it opens: it appears once until "Don't show this again" stays ticked
        public static void ShowFirstTime()
        {
            try { if (!Ui.Settings.GetBool("DlExtGuideSeen", false)) Show(false); }
            catch (Exception) { }
        }

        private static readonly string[] Keys = { "msedge", "chrome", "firefox", "opera", "brave" };
        private static readonly string[] Names = { "Microsoft Edge", "Google Chrome", "Mozilla Firefox", "Opera", "Brave" };
        private static readonly string[] Pages = { "edge://extensions", "chrome://extensions", "about:debugging#/runtime/this-firefox", "opera://extensions", "brave://extensions" };
        private static readonly string[] Tints = { "#2F9BD6", "#E8A33D", "#E8612D", "#D9344A", "#E8572B" };

        public static void Show(bool forced)
        {
            if (_w != null) { try { _w.Activate(); } catch (Exception) { } return; }
            Border host; Window w;
            try { w = DlgKit.Make("Add R-Downloader to your browser", 700, 590, out host); }
            catch (Exception ex) { Ui.Log.Error("Could not open the extension window: " + ex.Message, "Extension"); return; }
            _w = w;
            if (Lang.Rtl) w.FlowDirection = FlowDirection.RightToLeft;
            int cur = -1;
            string[] exe = new string[Keys.Length];
            for (int i = 0; i < Keys.Length; i++) exe[i] = BrowserExe(Keys[i]);

            StackPanel sp = new StackPanel(); sp.Margin = new Thickness(22, 16, 22, 14);
            sp.Children.Add(DlgKit.Head("Add R-Downloader to your browser"));
            TextBlock intro = DlgKit.Label("Choose your browser. R-Downloader will take the downloads you start in it and show them here.", true); intro.Margin = new Thickness(0, -6, 0, 12); sp.Children.Add(intro);

            // cards
            WrapPanel cards = new WrapPanel(); cards.HorizontalAlignment = HorizontalAlignment.Center;
            Border[] card = new Border[Keys.Length];
            TextBlock stepsHead = DlgKit.Label("", false); stepsHead.FontWeight = FontWeights.SemiBold; stepsHead.Margin = new Thickness(0, 14, 0, 8);
            StackPanel steps = new StackPanel();
            Action<int> select = null;
            for (int i = 0; i < Keys.Length; i++)
            {
                int idx = i; bool have = exe[i].Length > 0;
                Border c = new Border(); c.Width = 118; c.Height = 128; c.Margin = new Thickness(5); c.CornerRadius = new CornerRadius(12); c.Cursor = System.Windows.Input.Cursors.Hand;
                c.Background = Ui.Brush("#1F252D"); c.BorderBrush = Ui.Brush("#313B47"); c.BorderThickness = new Thickness(1.5);
                c.RenderTransformOrigin = new Point(0.5, 0.5); ScaleTransform sc = new ScaleTransform(1, 1); c.RenderTransform = sc;
                StackPanel inner = new StackPanel(); inner.VerticalAlignment = VerticalAlignment.Center; inner.Margin = new Thickness(6);
                Grid badge = new Grid(); badge.Width = 46; badge.Height = 46; badge.Margin = new Thickness(0, 0, 0, 8);
                System.Windows.Shapes.Ellipse el = new System.Windows.Shapes.Ellipse(); el.Fill = Ui.Brush(Tints[i]); el.Opacity = have ? 1 : 0.35; badge.Children.Add(el);
                TextBlock ini = new TextBlock(); ini.Text = Names[i].Substring(Names[i].IndexOf(' ') + 1, 1); ini.FontSize = 22; ini.FontWeight = FontWeights.Bold; ini.Foreground = Brushes.White;
                ini.HorizontalAlignment = HorizontalAlignment.Center; ini.VerticalAlignment = VerticalAlignment.Center; ini.Opacity = have ? 1 : 0.6; badge.Children.Add(ini);
                TextBlock nm = new TextBlock(); nm.Text = Names[i]; nm.TextAlignment = TextAlignment.Center; nm.FontWeight = FontWeights.SemiBold; nm.TextWrapping = TextWrapping.Wrap; nm.FontSize = 12;
                TextBlock st = new TextBlock(); st.Text = Lang.L(have ? "Click to add" : "Not installed on this computer"); st.TextAlignment = TextAlignment.Center; st.TextWrapping = TextWrapping.Wrap;
                st.FontSize = 10.5; st.Margin = new Thickness(0, 3, 0, 0); st.Foreground = have ? Ui.Brush("#4DB6A0") : DlgKit.Dim;
                ImageSource ico = have ? BrowserIcon(exe[i]) : null;
                if (ico != null)
                {
                    badge.Children.Clear();
                    Image im = new Image(); im.Source = ico; im.Width = 40; im.Height = 40; im.Stretch = Stretch.Uniform; im.Opacity = 1;
                    RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                    badge.Children.Add(im);
                }
                else
                {
                    FrameworkElement lg = VectorLogo(Keys[i]);     // not installed: the official icon (downloaded once and kept); a drawn logo until then
                    if (lg != null) { badge.Children.Clear(); lg.Opacity = 0.9; badge.Children.Add(lg); }
                    OfficialIcon(Keys[i], badge);
                }
                inner.Children.Add(badge); inner.Children.Add(nm); inner.Children.Add(st); c.Child = inner;
                if (!have) c.Opacity = 0.7;
                c.MouseEnter += delegate { sc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(120))); sc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(120))); };
                c.MouseLeave += delegate { sc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))); sc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))); };
                c.MouseLeftButtonUp += delegate { select(idx); };
                card[i] = c; cards.Children.Add(c);
                Fx.FadeUp(c, 70 * i, 14);
            }
            sp.Children.Add(cards); sp.Children.Add(stepsHead); sp.Children.Add(steps);

            Action fill = delegate
            {
                steps.Children.Clear(); if (cur < 0) { stepsHead.Text = Lang.L("Pick a browser above to see the steps."); return; }
                bool fox = cur == 2;
                stepsHead.Text = Lang.L("Steps for") + " " + Names[cur];
                string[] t = fox ? new string[] {
                    "Click \"Open the extensions page\" (it opens about:debugging).",
                    "Click \"Load Temporary Add-on...\" and choose manifest.json inside the extension folder (use \"Copy folder path\").",
                    "Firefox removes a temporary add-on when it closes - load it again next time." }
                    : new string[] {
                    "Click \"Open the extensions page\" (or type " + Pages[cur] + " in the address bar).",
                    "Turn on \"Developer mode\", click \"Load unpacked\" and choose the extension folder (use \"Copy folder path\").",
                    "Pin the R-Downloader icon. Downloads now open in R-Downloader - keep the app running." };
                for (int i = 0; i < t.Length; i++)
                {
                    DockPanel row = new DockPanel(); row.Margin = new Thickness(0, 0, 0, 7);
                    Border num = new Border(); num.Width = 22; num.Height = 22; num.CornerRadius = new CornerRadius(11); num.VerticalAlignment = VerticalAlignment.Top; num.Margin = new Thickness(0, 0, 10, 0);
                    num.Background = DlTheme.GradBrush();
                    TextBlock n = new TextBlock(); n.Text = (i + 1).ToString(); n.Foreground = Brushes.White; n.FontSize = 11.5; n.FontWeight = FontWeights.SemiBold; n.HorizontalAlignment = HorizontalAlignment.Center; n.VerticalAlignment = VerticalAlignment.Center;
                    num.Child = n; DockPanel.SetDock(num, Dock.Left);
                    TextBlock tx = new TextBlock(); tx.Text = Lang.L(t[i]); tx.TextWrapping = TextWrapping.Wrap; tx.VerticalAlignment = VerticalAlignment.Center; tx.FontSize = 12;
                    row.Children.Add(num); row.Children.Add(tx); steps.Children.Add(row);
                    Fx.FadeUp(row, 60 * i, 8);
                }
            };
            select = delegate(int i)
            {
                cur = i;
                for (int k = 0; k < card.Length; k++) card[k].BorderBrush = k == i ? DlTheme.GradBrush() : Ui.Brush("#313B47");
                fill();
            };
            fill();

            // buttons + link status
            WrapPanel bt = new WrapPanel(); bt.Margin = new Thickness(0, 12, 0, 6);
            Button bTest = DlgKit.Btn(w, "Test the link", true), bRep = DlgKit.Btn(w, "Repair the add-in", false), bCopy = DlgKit.Btn(w, "Copy folder path", false),
                   bFold = DlgKit.Btn(w, "Open folder", false), bPage = DlgKit.Btn(w, "Open the extensions page", false);
            foreach (Button b in new Button[] { bTest, bRep, bCopy, bFold, bPage }) { b.Margin = new Thickness(0, 0, 6, 6); bt.Children.Add(b); }
            sp.Children.Add(bt);
            StackPanel stat = new StackPanel(); stat.Orientation = Orientation.Horizontal; stat.Margin = new Thickness(0, 2, 0, 8);
            Ellipse dot = new Ellipse(); dot.Width = 9; dot.Height = 9; dot.Margin = new Thickness(0, 0, 8, 0); dot.VerticalAlignment = VerticalAlignment.Center; dot.Fill = Ui.Brush("#8895A6");
            TextBlock link = DlgKit.Label("", true); link.FontSize = 11.5; stat.Children.Add(dot); stat.Children.Add(link); sp.Children.Add(stat);
            Action refresh = delegate
            {
                bool on = BrowserBridge.Running;
                dot.Fill = Ui.Brush(on ? "#52B788" : "#E5827F");
                link.Text = Lang.L(on ? "R-Downloader is listening for the extension on port" : "The extension connection could not start (ports 53741-53750 are busy)") + (on ? " " + BrowserBridge.Port : "");
                dot.BeginAnimation(UIElement.OpacityProperty, null);
                if (on) { DoubleAnimation pl = new DoubleAnimation(1.0, 0.3, new Duration(TimeSpan.FromMilliseconds(900))); pl.AutoReverse = true; pl.RepeatBehavior = RepeatBehavior.Forever; dot.BeginAnimation(UIElement.OpacityProperty, pl); }
            };
            refresh();

            bTest.Click += delegate
            {
                link.Text = Lang.L("Testing...");
                int port = BrowserBridge.Port;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    string res;
                    try
                    {
                        if (!BrowserBridge.Running) throw new InvalidOperationException("not running");
                        System.Net.HttpWebRequest r = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://127.0.0.1:" + port + "/ping");
                        r.Timeout = 3000; r.Proxy = null;
                        using (System.Net.WebResponse rs = r.GetResponse()) res = "OK";
                    }
                    catch (Exception) { res = "FAIL"; }
                    Ui.Post(delegate
                    {
                        dot.BeginAnimation(UIElement.OpacityProperty, null);
                        dot.Fill = Ui.Brush(res == "OK" ? "#52B788" : "#E5827F");
                        link.Text = Lang.L(res == "OK" ? "The link works - the extension can reach R-Downloader." : "The link does not answer. Press \"Repair the add-in\".");
                    });
                });
            };
            bRep.Click += delegate
            {
                try { BrowserBridge.Stop(); } catch (Exception) { }
                try { BrowserBridge.Start(); } catch (Exception ex) { Ui.Log.Warn("Could not restart the extension connection: " + ex.Message, "Extension"); }
                bool okF = Directory.Exists(Folder(false)) && Directory.Exists(Folder(true));
                refresh();
                if (!okF) link.Text = Lang.L("The extension folder is missing - reinstall R-Downloader.");
                else if (BrowserBridge.Running) link.Text += "  -  " + Lang.L("Repaired. Reload the extension in your browser.");
            };
            bCopy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(Folder(cur == 2));
                    bCopy.Content = Lang.L("Copied");
                    DispatcherTimer t = new DispatcherTimer(); t.Interval = TimeSpan.FromMilliseconds(1400);
                    t.Tick += delegate { t.Stop(); bCopy.Content = Lang.L("Copy folder path"); };
                    t.Start();
                }
                catch (Exception) { }
            };
            bFold.Click += delegate
            {
                string f = Folder(cur == 2);
                try { if (Directory.Exists(f)) Tool.Start("explorer.exe", "\"" + f + "\""); else Dlg.Warn("The extension folder was not found\n\n" + f); }
                catch (Exception ex) { Ui.Log.Warn("Could not open the folder: " + ex.Message, "Extension"); }
            };
            bPage.Click += delegate
            {
                int i = cur >= 0 ? cur : 0;
                if (cur < 0) { for (int k = 0; k < exe.Length; k++) if (exe[k].Length > 0) { i = k; break; } select(i); }
                OpenInBrowser(Keys[i], Pages[i]);
            };

            CheckBox never = DlgKit.Check(w, "Don't show this again", forced ? Ui.Settings.GetBool("DlExtGuideSeen", false) : true);
            Button done = DlgKit.Btn(w, "Close", true); done.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel foot = new DockPanel(); foot.Margin = new Thickness(0, 4, 0, 0);
            DockPanel.SetDock(done, Dock.Right); foot.Children.Add(done); foot.Children.Add(never);
            sp.Children.Add(foot);
            done.Click += delegate { w.Close(); };
            w.Closing += delegate { try { Ui.Settings.Set("DlExtGuideSeen", never.IsChecked == true); Ui.Settings.SaveSoon(); } catch (Exception) { } };
            w.Closed += delegate { _w = null; };
            ScrollViewer sv = new ScrollViewer(); sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; sv.Content = sp; host.Child = sv;
            // the first browser that is installed is preselected
            for (int k = 0; k < exe.Length; k++) if (exe[k].Length > 0) { select(k); break; }
            if (Ui.Win != null && Ui.Win.IsVisible) { w.Owner = Ui.Win; }
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.Topmost = true; w.Show();
            try { w.Activate(); w.Focus(); } catch (Exception) { }
            // the main window finishes opening after this one: bring the guide to the front again, then let it behave normally
            DispatcherTimer ft = new DispatcherTimer(); ft.Interval = TimeSpan.FromMilliseconds(700);
            ft.Tick += delegate { ft.Stop(); try { if (_w == w) { w.Activate(); w.Topmost = false; } } catch (Exception) { } };
            ft.Start();
        }
    }
}
