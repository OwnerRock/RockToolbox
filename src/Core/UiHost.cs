using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace RockToolbox.Native
{
    // Builds the main window and can build it AGAIN while the program runs, so a change of look (light / dark, colours, background
    // style, Classic / Modern) or language is shown at once without restarting the program.
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
            IPage[] pages = new IPage[]
            {
                new WindowsUpdatePage(), new DriversPage(), new UninstallAppsPage(), new CleanupPage(),
                new StartupPage(), new RepairPage(), new SystemInfoPage(), new GodModePage(),
                new TweaksPage(), new DebloatPage(), new UpdatesPage(), new ConfigPage(), new InstallPage(), new WallpapersPage(), new SettingsPage()
            };
            MainShell shell = new MainShell(window, settings, log, pages);
            shell.Start();
            if (Lang.Rtl) window.FlowDirection = FlowDirection.RightToLeft;
            RockScroll.Attach(window);
            Anim.ApplyScrollSettings();
            foreach (string lvn in new string[] { "lvClean", "lvDrvInstall", "lvDrvUn", "lvFind", "lvGod", "lvStartup", "lvUnApps", "lvUpg", "lvWU" })
            {
                ListView lvo = window.FindName(lvn) as ListView;
                if (lvo != null) VirtualizingPanel.SetScrollUnit(lvo, ScrollUnit.Pixel);
            }
            BgFx.Apply();
            ProfileUi.UpdateCard();
            Anim.WindowIntro(Ui.Find<FrameworkElement>("rootGrid"));
            AppLife.AttachWindow(window);
            return window;
        }

        // something that must not be interrupted is running: the new look waits until it ends
        private static bool Blocked()
        {
            if (Work.Busy) return true;
            return false;
        }

        // call after a setting that changes the whole look (or language) was saved
        public static void Reapply()
        {
            if (_busy) return;
            if (Blocked())
            {
                _pending = true;
                Ui.Log.Info("The new look will be applied as soon as the running task ends.", "Settings");
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
            AppSettings s = Ui.Settings;
            s.SaveNow();
            string tab = MainShell.Current != null ? MainShell.Current.CurTab : "Settings";
            List<string> sel = null;
            try { if (InstallPage.Instance != null) sel = InstallPage.Instance.SelectedIds(); } catch { }
            string cust = ""; try { if (InstallPage.Instance != null) cust = InstallPage.Instance.CustomBox.Text; } catch { }

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

            // keep what the user picked in the app list
            if (sel != null && sel.Count > 0) { try { InstallPage.Instance.SetSelected(sel); } catch { } }
            if (cust.Length > 0) { try { InstallPage.Instance.CustomBox.Text = cust; } catch { } }

            if (wasVisible) nw.Show();
            nw.Activate();
            AppLife.ReplaceWindow(old);
            AppLife.RefreshTray();
        }
    }
}
