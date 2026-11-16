using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RDownloader.Native
{
    // Downloads > Options: eleven tabs. Every control saves at once (no OK button needed).
    internal static class OptionsWin
    {
        private static Window _w;
        public static readonly string[] Tabs = { "General", "File Types", "Save To", "Downloads", "Connection", "Proxy", "Site Logins", "Sounds", "Speed Limiter", "Appearance", "Torrents", "App Settings" };
        private static readonly string[] Glyphs = { "", "", "", "", "", "", "", "", "", "", "", "\uE713" };

        private static ListBox _list;
        private static Border _body;
        private static Window _win;

        public static void Show(int tab)
        {
            if (_w != null) { try { _w.Activate(); Select(tab); return; } catch (Exception) { _w = null; } }
            Border host; Window w = DlgKit.Make("Options", 760, 560, out host); _w = w; _win = w;
            w.Closed += delegate { _w = null; Ui.Settings.SaveSoon(); };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _list = new ListBox(); _list.Background = Brushes.Transparent; _list.BorderThickness = new Thickness(0); _list.Margin = new Thickness(8, 10, 4, 10); _list.Foreground = w.Foreground;
            for (int i = 0; i < Tabs.Length; i++)
            {
                StackPanel row = new StackPanel(); row.Orientation = Orientation.Horizontal; row.Margin = new Thickness(6, 5, 6, 5);
                TextBlock ic = new TextBlock(); ic.Text = Glyphs[i]; ic.FontFamily = new FontFamily("Segoe MDL2 Assets"); ic.Width = 24; ic.Foreground = Ui.Brush("#4DA3C4"); ic.VerticalAlignment = VerticalAlignment.Center;
                TextBlock tx = new TextBlock(); tx.Text = Lang.L(Tabs[i]); tx.VerticalAlignment = VerticalAlignment.Center; tx.FontSize = 13;
                row.Children.Add(ic); row.Children.Add(tx); _list.Items.Add(row);
            }
            Border side = new Border(); side.Background = Ui.Brush("#171C22"); side.BorderBrush = Ui.Brush("#2A323C"); side.BorderThickness = new Thickness(0, 0, 1, 0); side.Child = _list;
            g.Children.Add(side);
            _body = new Border(); _body.Padding = new Thickness(22, 16, 22, 14); Grid.SetColumn(_body, 1); g.Children.Add(_body);
            _list.SelectionChanged += delegate { if (_list.SelectedIndex >= 0) Build(_list.SelectedIndex); };
            host.Child = g;
            _list.SelectedIndex = tab < 0 || tab >= Tabs.Length ? 0 : tab;
            w.ShowDialog();
        }

        public static bool IsOpen { get { return _w != null; } }
        public static int CurTab { get { return _list != null && _list.SelectedIndex >= 0 ? _list.SelectedIndex : 0; } }
        // the look / language changed: the window is closed (the settings page belongs to the old main window) and opened again on the new one
        public static void CloseForRebuild()
        {
            _setEl = null;
            if (_w != null) { try { _w.Close(); } catch (Exception) { } _w = null; }
        }

        private static void Select(int t) { if (t >= 0 && t < Tabs.Length && _list != null) _list.SelectedIndex = t; }

        private static void Build(int t)
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(DlgKit.Head(Tabs[t]));
            switch (t)
            {
                case 0: General(sp); break; case 1: FileTypes(sp); break; case 2: SaveTo(sp); break; case 3: Downloads(sp); break;
                case 4: Connection(sp); break; case 5: Proxy(sp); break; case 6: Logins(sp); break; case 7: Sounds(sp); break;
                case 8: Limiter(sp); break; case 9: Appearance(sp); break; case 10: Torrents(sp); break;
                case 11: AppSettings(sp); break;
            }
            ScrollViewer sv = new ScrollViewer(); sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; sv.Content = sp; sv.Padding = new Thickness(0, 0, 8, 0);
            _body.Child = sv;
            TranslateTransform tr = new TranslateTransform(0, 10); sv.RenderTransform = tr; sv.Opacity = 0;
            sv.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        // ---- small bound controls ----
        private static CheckBox Chk(StackPanel sp, string text, string key, bool def, Action<bool> after)
        {
            CheckBox c = DlgKit.Check(_win, text, Ui.Settings.GetBool(key, def));
            c.Click += delegate { Ui.Settings.Set(key, c.IsChecked == true); Ui.Settings.SaveSoon(); if (after != null) after(c.IsChecked == true); };
            sp.Children.Add(c); return c;
        }
        private static TextBox Txt(StackPanel sp, string label, string key, string def, double lw)
        {
            string v = Ui.Settings.GetString(key); TextBox t = DlgKit.Box(_win, string.IsNullOrEmpty(v) ? def : v);
            t.TextChanged += delegate { Ui.Settings.Set(key, t.Text); Ui.Settings.SaveSoon(); };
            sp.Children.Add(DlgKit.Row(label, t, lw)); return t;
        }
        private static void Note(StackPanel sp, string text) { TextBlock b = DlgKit.Label(text, true); b.Margin = new Thickness(0, 6, 0, 6); sp.Children.Add(b); }
        private static int IntSet(string key, int def) { int n; return int.TryParse(Ui.Settings.GetString(key), out n) ? n : def; }

        // the former Settings page (look, language, tray, startup, .Rlist, extension, logs) lives here now
        private static FrameworkElement _setEl;
        private static void AppSettings(StackPanel sp)
        {
            try
            {
                if (_setEl == null)
                {
                    ScrollViewer pv = Ui.Find<ScrollViewer>("pnlSettings");
                    _setEl = pv.Content as FrameworkElement; pv.Content = null;
                }
                Panel par = _setEl.Parent as Panel; if (par != null) par.Children.Remove(_setEl);
                // the page was made for the main window: bring its styles along so buttons, switches and lists look the same here
                try { if (Ui.Win != null && !_setEl.Resources.MergedDictionaries.Contains(Ui.Win.Resources)) _setEl.Resources.MergedDictionaries.Add(Ui.Win.Resources); } catch (Exception) { }
                if (SettingsPage.Current != null) SettingsPage.Current.SyncNow();
                sp.Children.Clear();
                sp.Children.Add(_setEl);
                _setEl.Margin = new Thickness(0);
                Panel pn = _setEl as Panel; int di = 0;
                if (pn != null) foreach (UIElement ch in pn.Children) { Fx.FadeUp(ch, 50 * di, 12); di++; }
            }
            catch (Exception ex) { Ui.Log.Error("Could not show the settings: " + ex.Message, "Settings"); }
        }

        // ---- tabs ----
        private static void General(StackPanel sp)
        {
            CheckBox st = DlgKit.Check(_win, "Start R-Downloader with Windows", AppLife.StartupEnabled());
            st.Click += delegate { try { AppLife.SetStartup(st.IsChecked == true); } catch (Exception ex) { Ui.Warn("Could not change the startup setting: " + ex.Message); st.IsChecked = AppLife.StartupEnabled(); } };
            sp.Children.Add(st);
            Chk(sp, "Keep running in the tray when the window is closed", "CloseToTray", true, null);
            Chk(sp, "Keep the window above other windows", "Topmost", false, delegate(bool on) { Ui.Win.Topmost = on; });
            Chk(sp, "Start maximized", "StartMaximized", false, null);
            Chk(sp, "Offer links that are copied (clipboard watch)", "ClipWatch", false, null);
            CheckBox rl = DlgKit.Check(_win, "Open .Rlist files with R-Downloader", RlistAssoc.IsOn());
            rl.Click += delegate { try { RlistAssoc.Set(rl.IsChecked == true); } catch (Exception ex) { Ui.Warn("Could not change the file association: " + ex.Message); } };
            sp.Children.Add(rl);
        }

        private static void FileTypes(StackPanel sp)
        {
            Note(sp, "Links copied to the clipboard are offered only when they end with one of these file types. Separate them with commas.");
            Txt(sp, "File types", "ClipExt", DownloadsPage.DefaultClipExt, 100);
            Button reset = DlgKit.Btn(_win, "Reset to the default list", false); reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Margin = new Thickness(0, 8, 0, 0);
            reset.Click += delegate { Ui.Settings.Set("ClipExt", ""); Ui.Settings.SaveSoon(); Build(1); };
            sp.Children.Add(reset);
            Note(sp, "Magnet links and .torrent files are always offered.");
        }

        private static void SaveTo(StackPanel sp)
        {
            DownloadsPage dp = DownloadsPage.Instance;
            TextBox d = DlgKit.Box(_win, dp != null ? dp.DlDir : "");
            d.TextChanged += delegate { if (d.Text.Trim().Length > 0) { Ui.Settings.Set("DlDir", d.Text.Trim()); Ui.Settings.SaveSoon(); } };
            sp.Children.Add(DlgKit.Label("Save new downloads to", false));
            Grid b = DlgKit.Browse(_win, d, delegate { return DlgKit.PickFolder(d.Text); }); b.Margin = new Thickness(0, 6, 0, 10); sp.Children.Add(b);
            Note(sp, "New downloads are saved here unless you choose another folder in the Add window.");
        }

        private static void Downloads(StackPanel sp)
        {
            sp.Children.Add(DlgKit.Label("If a file with the same name already exists", false));
            ComboBox dup = DlgKit.Combo(new string[] { "Keep both (add a number)", "Replace the old file" }, Ui.Settings.GetString("DupMode") == "overwrite" ? 1 : 0);
            dup.Margin = new Thickness(0, 6, 0, 12);
            dup.SelectionChanged += delegate { Ui.Settings.Set("DupMode", dup.SelectedIndex == 1 ? "overwrite" : "rename"); Ui.Settings.SaveSoon(); };
            sp.Children.Add(dup);
            sp.Children.Add(DlgKit.Label("When all downloads finish", false));
            List<string> af = new List<string>(); foreach (string a in DownloadsPage.AfterActions) af.Add(a);
            ComboBox after = DlgKit.Combo(af, IntSet("AfterAll", 0)); after.Margin = new Thickness(0, 6, 0, 12);
            after.SelectionChanged += delegate { Ui.Settings.Set("AfterAll", after.SelectedIndex.ToString()); Ui.Settings.SaveSoon(); };
            sp.Children.Add(after);
            sp.Children.Add(DlgKit.Label("Virus scan (a program that is started on every finished file)", false));
            Txt(sp, "Program", "ScanCmd", "", 100);
            Txt(sp, "Arguments", "ScanArgs", "", 100);
            Note(sp, "Use {file} for the finished file. Empty arguments = the file path. Leave the program empty to turn this off.");
        }

        private static void Connection(StackPanel sp)
        {
            int[] conns = { 1, 2, 4, 6, 8, 12, 16, 24, 32 };
            List<string> cl = new List<string>(); int sel = 6; int cur = IntSet("DlConn", 16);
            for (int i = 0; i < conns.Length; i++) { cl.Add(conns[i].ToString()); if (conns[i] == cur) sel = i; }
            ComboBox c = DlgKit.Combo(cl, sel);
            c.SelectionChanged += delegate { Ui.Settings.Set("DlConn", conns[c.SelectedIndex].ToString()); Ui.Settings.SaveSoon(); };
            sp.Children.Add(DlgKit.Row("Connections per download", c, 220));
            List<string> pl = new List<string> { "No limit", "1", "2", "4", "8", "16" }; int[] pv = { 0, 1, 2, 4, 8, 16 };
            int ps = Array.IndexOf(pv, IntSet("PerServer", 0)); ComboBox per = DlgKit.Combo(pl, ps < 0 ? 0 : ps);
            per.SelectionChanged += delegate { Ui.Settings.Set("PerServer", pv[per.SelectedIndex].ToString()); Ui.Settings.SaveSoon(); };
            sp.Children.Add(DlgKit.Row("Most connections to one server", per, 220));
            Note(sp, "Some servers block you when you open too many connections. Choose a lower number for them.");
            Txt(sp, "User-Agent", "UserAgent", DlOpts.DefaultUa, 220);
            Note(sp, "The name R-Downloader gives to web servers. Changes apply to the next download.");
        }

        private static void Proxy(StackPanel sp)
        {
            string mode = Ui.Settings.GetString("ProxyMode"); int sel = mode == "none" ? 1 : mode == "manual" ? 2 : 0;
            ComboBox m = DlgKit.Combo(new string[] { "Use the Windows proxy settings", "No proxy", "Use this proxy" }, sel);
            StackPanel manual = new StackPanel();
            m.SelectionChanged += delegate
            {
                Ui.Settings.Set("ProxyMode", m.SelectedIndex == 1 ? "none" : m.SelectedIndex == 2 ? "manual" : "system"); Ui.Settings.SaveSoon();
                manual.IsEnabled = m.SelectedIndex == 2; manual.Opacity = m.SelectedIndex == 2 ? 1 : 0.45;
            };
            sp.Children.Add(m);
            manual.Margin = new Thickness(0, 12, 0, 0);
            Txt(manual, "Address : port", "ProxyHost", "", 120);
            Txt(manual, "User name", "ProxyUser", "", 120);
            TextBox pass = DlgKit.Box(_win, Ui.Settings.GetString("ProxyPass"));
            pass.TextChanged += delegate { Ui.Settings.Set("ProxyPass", pass.Text); Ui.Settings.SaveSoon(); };
            manual.Children.Add(DlgKit.Row("Password", pass, 120));
            sp.Children.Add(manual);
            manual.IsEnabled = sel == 2; manual.Opacity = sel == 2 ? 1 : 0.45;
            Note(sp, "Example address: 127.0.0.1:8080");
        }

        private static void Logins(StackPanel sp)
        {
            Note(sp, "R-Downloader signs in automatically to these sites (Basic / Windows sign-in).");
            ListBox lb = new ListBox(); lb.Height = 150; lb.Background = Ui.Brush("#1F252D"); lb.BorderBrush = Ui.Brush("#313B47"); lb.Foreground = _win.Foreground;
            Action fill = delegate { lb.Items.Clear(); foreach (string[] l in DlOpts.Logins()) lb.Items.Add(l[0] + "     (" + l[1] + ")"); };
            fill(); sp.Children.Add(lb);
            TextBox h = DlgKit.Box(_win, ""), u = DlgKit.Box(_win, ""), p = DlgKit.Box(_win, "");
            h.Margin = u.Margin = p.Margin = new Thickness(0, 0, 0, 0);
            StackPanel f = new StackPanel(); f.Margin = new Thickness(0, 10, 0, 0);
            f.Children.Add(DlgKit.Row("Site (host name)", h, 140)); f.Children.Add(DlgKit.Row("User name", u, 140)); f.Children.Add(DlgKit.Row("Password", p, 140));
            sp.Children.Add(f);
            StackPanel bt = new StackPanel(); bt.Orientation = Orientation.Horizontal; bt.Margin = new Thickness(0, 8, 0, 0);
            Button add = DlgKit.Btn(_win, "Add", true), del = DlgKit.Btn(_win, "Remove", false); add.Margin = new Thickness(0, 0, 4, 0);
            add.Click += delegate
            {
                string host = h.Text.Trim().ToLowerInvariant(); if (host.StartsWith("http")) { try { host = new Uri(host).Host; } catch (Exception) { } }
                if (host.Length == 0 || u.Text.Length == 0) return;
                List<string[]> l = DlOpts.Logins(); l.RemoveAll(delegate(string[] x) { return x[0] == host; });
                l.Add(new string[] { host, u.Text, p.Text }); DlOpts.SaveLogins(l); h.Text = u.Text = p.Text = ""; fill();
            };
            del.Click += delegate
            {
                int i = lb.SelectedIndex; if (i < 0) return;
                List<string[]> l = DlOpts.Logins(); if (i < l.Count) { l.RemoveAt(i); DlOpts.SaveLogins(l); fill(); }
            };
            bt.Children.Add(add); bt.Children.Add(del); sp.Children.Add(bt);
        }

        private static void Sounds(StackPanel sp)
        {
            Chk(sp, "Play sounds", "SoundsOn", true, null);
            Note(sp, "R-Downloader plays its own sounds. Choose a different .wav file for any event, or leave it empty to use the built-in sound.");
            foreach (string[] ev in RSound.Events)
            {
                string key = ev[0], keyName = "Snd." + ev[0];
                TextBox t = DlgKit.Box(_win, Ui.Settings.GetString(keyName)); t.ToolTip = Lang.L("Built-in sound");
                t.TextChanged += delegate { Ui.Settings.Set(keyName, t.Text.Trim()); Ui.Settings.SaveSoon(); };
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(t);
                Button pick = DlgKit.Btn(_win, "...", false); pick.MinWidth = 36; pick.Padding = new Thickness(8, 4, 8, 4); Grid.SetColumn(pick, 1);
                pick.Click += delegate
                {
                    Microsoft.Win32.OpenFileDialog fd = new Microsoft.Win32.OpenFileDialog(); fd.Filter = "Sound files (*.wav)|*.wav";
                    if (fd.ShowDialog() == true) t.Text = fd.FileName;
                };
                Button play = DlgKit.Btn(_win, "Play", false); play.MinWidth = 60; play.Padding = new Thickness(10, 4, 10, 4); Grid.SetColumn(play, 2);
                play.Click += delegate { RSound.Play(key, true); };
                g.Children.Add(pick); g.Children.Add(play);
                sp.Children.Add(DlgKit.Row(ev[1], g, 160));
            }
        }

        private static void Limiter(StackPanel sp)
        {
            DownloadsPage dp = DownloadsPage.Instance;
            Note(sp, "The most speed all downloads can use together.");
            int cur = dp != null ? dp.LimitIndex : 0;
            ComboBox c = DlgKit.Combo(DownloadsPage.LimitList, cur);
            c.SelectionChanged += delegate { if (DownloadsPage.Instance != null) DownloadsPage.Instance.LimitIndex = c.SelectedIndex; };
            sp.Children.Add(DlgKit.Row("Speed limit", c, 160));
        }

        private static void Appearance(StackPanel sp)
        {
            Note(sp, "The window is rebuilt when you change these.");
            CheckBox dark = DlgKit.Check(_win, "Dark theme", !Theme.Light);
            dark.Click += delegate { Ui.Settings.Set("Theme", dark.IsChecked == true ? "Dark" : "Light"); Close(); SettingsPage.ApplyThemeChange(); };
            sp.Children.Add(dark);
            CheckBox modern = DlgKit.Check(_win, "Modern style (gradient buttons, rounder shapes)", Theme.Modern);
            modern.Click += delegate { Ui.Settings.Set("UiStyle", modern.IsChecked == true ? "Modern" : "Classic"); Close(); SettingsPage.ApplyThemeChange(); };
            sp.Children.Add(modern);
            List<string> names = new List<string>(); int sel = 0;
            for (int i = 0; i < Lang.List.Length; i++) { names.Add(Lang.List[i][2]); if (Lang.List[i][0] == Lang.Name) sel = i; }
            ComboBox lg = DlgKit.Combo(names, sel); lg.Margin = new Thickness(0, 4, 0, 0);
            bool init = true;
            lg.SelectionChanged += delegate { if (init) return; Close(); SettingsPage.SetLanguage(Lang.List[lg.SelectedIndex][0]); };
            sp.Children.Add(DlgKit.Row("Language", lg, 120));
            init = false;
        }

        private static void Torrents(StackPanel sp)
        {
            Note(sp, "Torrents and magnet links are downloaded with the aria2 engine.");
            CheckBox mg = DlgKit.Check(_win, "Open magnet links and .torrent files with R-Downloader", TorrentAssoc.IsOn());
            mg.Click += delegate { try { TorrentAssoc.Set(mg.IsChecked == true); } catch (Exception ex) { Ui.Warn("Could not change the file association: " + ex.Message); } };
            sp.Children.Add(mg);
            Button fw = DlgKit.Btn(_win, "Allow torrents through the firewall", false); fw.HorizontalAlignment = HorizontalAlignment.Left; fw.Margin = new Thickness(0, 6, 0, 12);
            fw.Click += delegate { TorrentAssoc.AllowFirewall(); };
            sp.Children.Add(fw);
            sp.Children.Add(DlgKit.Label("After a torrent is downloaded", false));
            Txt(sp, "Keep sharing (minutes)", "TorSeedMin", "0", 200);
            Txt(sp, "Stop at share ratio", "TorRatio", "0", 200);
            Txt(sp, "Upload limit (KB/s, 0 = none)", "TorUpKb", "0", 200);
            Note(sp, "0 minutes = stop right after the download. Sharing helps other people finish the same file.");
        }

        private static void Close() { try { if (_w != null) _w.Close(); } catch (Exception) { } }
    }
}
