using System;
using System.Windows;
using System.Windows.Controls;

namespace RDownloader.Native
{
    // Builds the main window and can build it AGAIN while the program runs, so a change of look (light / dark, Classic / Modern)
    // or language is shown at once without restarting the program.
    internal static class UiHost
    {
        public static string Root, DataDir;
        private static bool _pending, _busy;

        public static Window BuildMain(AppSettings settings, bool first)
        {
            Theme.Build(settings);
            Lang.Init(settings);
            Window window = XamlLoader.LoadMainWindow(Root);
            Logger log = new Logger(window.FindName("txtStatus") as TextBlock, DataDir);
            Ui.Win = window; Ui.Settings = settings; Ui.Log = log;
            TextBlock stx = window.FindName("txtStatus") as TextBlock;
            if (stx != null)
            {
                stx.Cursor = System.Windows.Input.Cursors.Hand;
                stx.MouseLeftButtonUp += delegate { LogView.Show(Logger.LastLevel == "ERROR" || Logger.LastLevel == "WARN"); };
            }
            Work.Init();
            IPage[] pages = new IPage[] { new DownloadsPage(), new SettingsPage() };
            MainShell shell = new MainShell(window, settings, log, pages);
            shell.Start();
            MainMenus.Init();
            if (Lang.Rtl) window.FlowDirection = FlowDirection.RightToLeft;
            RockScroll.Attach(window);
            Anim.ApplyScrollSettings();
            ListView lv = window.FindName("lvDownloads") as ListView;
            if (lv != null) VirtualizingPanel.SetScrollUnit(lv, ScrollUnit.Pixel);
            Anim.WindowIntro(Ui.Find<FrameworkElement>("rootGrid"));
            AppLife.AttachWindow(window);
            return window;
        }

        // something that must not be interrupted is running: the new look waits until it ends
        private static bool Blocked()
        {
            if (Work.Busy) return true;
            DownloadsPage d = DownloadsPage.Instance;
            return d != null && d.HasActive;
        }

        // call after a setting that changes the whole look (or language) was saved
        public static void Reapply()
        {
            if (_busy) return;
            if (Blocked())
            {
                _pending = true;
                Ui.Log.Info("The new look will be applied as soon as the running downloads end.", "Settings");
                return;
            }
            _pending = false;
            _busy = true;
            try { Rebuild(); }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(DataDir, "error.log"), DateTime.Now.ToString("s") + "  Reapply failed: " + ex + "\r\n"); } catch { }
                Ui.Log.Error("Could not apply the new look at once: " + ex.Message, "Settings");
            }
            finally { _busy = false; }
        }

        public static void RunPending()
        {
            if (_pending && !Blocked()) Reapply();
        }

        private static void Rebuild()
        {
            Window old = Ui.Win;
            bool reopen = OptionsWin.IsOpen; int optTab = OptionsWin.CurTab;
            if (reopen) OptionsWin.CloseForRebuild();
            AppSettings s = Ui.Settings;
            s.SaveNow();
            string tab = MainShell.Current != null ? MainShell.Current.CurTab : "Downloads";
            double l = old.Left, t = old.Top, w = old.Width, h = old.Height;
            WindowState st = old.WindowState;
            bool wasVisible = old.IsVisible;

            MainShell.StartOverride = tab;
            Window nw = BuildMain(s, false);
            MainShell.StartOverride = null;
            if (st == WindowState.Normal)
            {
                nw.WindowStartupLocation = WindowStartupLocation.Manual;
                nw.Left = l; nw.Top = t; nw.Width = w; nw.Height = h;
            }
            else nw.WindowState = st;
            if (Application.Current != null) Application.Current.MainWindow = nw;
            if (wasVisible) nw.Show();
            nw.Activate();
            AppLife.ReplaceWindow(old);
            AppLife.RefreshTray();
            if (reopen) nw.Dispatcher.BeginInvoke(new Action(delegate { OptionsWin.Show(optTab); }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
