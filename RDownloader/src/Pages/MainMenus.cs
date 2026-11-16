using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RDownloader.Native
{
    // builds the five drop-down menus of the menu bar (Tasks / File / Downloads / View / Help)
    internal static class MainMenus
    {
        private static DownloadsPage _dl;

        public static void Init()
        {
            _dl = MainShell.Current.PageOf("Downloads") as DownloadsPage;
            Ui.Win.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                System.Windows.Input.ModifierKeys m = System.Windows.Input.Keyboard.Modifiers;
                bool ctrl = (m & System.Windows.Input.ModifierKeys.Control) != 0, shift = (m & System.Windows.Input.ModifierKeys.Shift) != 0;
                if (e.Key == System.Windows.Input.Key.F3) { _dl.FindNext(); e.Handled = true; }
                else if (ctrl && e.Key == System.Windows.Input.Key.N) { if (shift) _dl.AddMany(); else _dl.AddNew(); e.Handled = true; }
                else if (ctrl && shift && e.Key == System.Windows.Input.Key.V) { _dl.AddFromClipboard(); e.Handled = true; }
                else if (ctrl && e.Key == System.Windows.Input.Key.T) { TorrentDlg.Show(_dl); e.Handled = true; }
                else if (ctrl && e.Key == System.Windows.Input.Key.O) { OptionsWin.Show(0); e.Handled = true; }
                else if (ctrl && e.Key == System.Windows.Input.Key.F) { _dl.FocusFind(); e.Handled = true; }
            };
            Hook("btnMnTasks", Tasks); Hook("btnMnFile", File); Hook("btnMnDl", Downloads); Hook("btnMnView", View); Hook("btnMnHelp", Help);
        }

        private static void Hook(string name, Func<List<MItem>> build)
        {
            Button b = Ui.Find<Button>(name);
            if (b == null) return;
            b.Click += delegate { try { AppMenu.Show(b, build()); } catch (Exception ex) { Ui.Log.Warn("Menu error: " + ex.Message, "UI"); } };
            AppMenu.Register(b, delegate { return build(); });
        }

        private static string T(string s) { return Lang.L(s); }

        private static List<MItem> Tasks()
        {
            List<MItem> m = new List<MItem>();
            m.Add(MItem.Of(T("Add a download..."), "Ctrl+N", delegate { _dl.AddNew(); }));
            m.Add(MItem.Of(T("Add several downloads..."), "Ctrl+Shift+N", delegate { _dl.AddMany(); }));
            m.Add(MItem.Of(T("Add from the clipboard"), "Ctrl+Shift+V", delegate { _dl.AddFromClipboard(); }));
            m.Add(MItem.Of(T("Site Grabber..."), delegate { DlWin.OpenGrabber(); }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Add a torrent / magnet link..."), "Ctrl+T", delegate { TorrentDlg.Show(_dl); }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Open a list (.Rlist)..."), delegate { _dl.OpenRlistFile(); }));
            m.Add(MItem.Of(T("Export the list (.Rlist)..."), delegate { _dl.ExportList(); }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Open the download folder"), delegate { try { Tool.Start("explorer.exe", "\"" + _dl.DlDir + "\""); } catch (Exception) { } }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Exit"), "Alt+F4", delegate { AppLife.Exit(); }));
            return m;
        }

        private static List<MItem> File()
        {
            bool sel = _dl.HasSelection, done = _dl.OneCompleted;
            List<MItem> m = new List<MItem>();
            m.Add(Gate(MItem.Of(T("Download now"), "Enter", delegate { _dl.ResumeSelected(); }), sel));
            m.Add(Gate(MItem.Of(T("Stop"), delegate { _dl.StopSelected(); }), sel));
            m.Add(Gate(MItem.Of(T("Remove"), "Del", delegate { _dl.RemoveSelected(); }), sel));
            m.Add(Gate(MItem.Of(T("Download again"), delegate { _dl.RedownloadSelected(); }), sel));
            m.Add(MItem.Line());
            m.Add(Gate(MItem.Of(T("Open"), delegate { _dl.OpenSelected(); }), done));
            m.Add(Gate(MItem.Of(T("Open with..."), delegate { _dl.OpenWithSelected(); }), done));
            m.Add(Gate(MItem.Of(T("Open the folder"), delegate { _dl.OpenFolderNow(); }), sel));
            m.Add(Gate(MItem.Of(T("Move / Rename..."), delegate { _dl.MoveRenameSelected(); }), done));
            m.Add(MItem.Line());
            m.Add(Gate(MItem.Of(T("Copy the address"), "Ctrl+C", delegate { _dl.CopyAddressSelected(); }), sel));
            m.Add(MItem.Of(T("Select all"), "Ctrl+A", delegate { _dl.SelectAllRows(); }));
            m.Add(Gate(MItem.Of(T("Properties"), "Alt+Enter", delegate { _dl.PropertiesSelected(); }), sel));
            return m;
        }

        private static MItem Gate(MItem m, bool on) { m.Disabled = !on; return m; }

        private static List<MItem> Downloads()
        {
            List<MItem> m = new List<MItem>();
            m.Add(MItem.Of(T("Resume all"), delegate { _dl.ResumeAll(); }));
            m.Add(MItem.Of(T("Stop all"), delegate { _dl.StopAll(); }));
            m.Add(MItem.Of(T("Delete all finished"), delegate { _dl.ClearFinished(); }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Find..."), "Ctrl+F", delegate { _dl.FocusFind(); }));
            m.Add(MItem.Of(T("Find next"), "F3", delegate { _dl.FindNext(); }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Start the queue"), delegate { _dl.StartQueueNow(); }));
            m.Add(MItem.Of(T("Stop the queue"), delegate { _dl.StopQueueNow(); }));
            List<MItem> lim = new List<MItem>();
            for (int i = 0; i < DownloadsPage.LimitList.Length; i++)
            {
                int k = i;
                lim.Add(MItem.Choice(T(DownloadsPage.LimitList[i]), _dl.LimitIndex == i, delegate { _dl.LimitIndex = k; }));
            }
            m.Add(MItem.Menu(T("Speed limiter"), lim));
            List<MItem> aft = new List<MItem>();
            string[] names = DownloadsPage.AfterActions;
            // the menu shows the order  nothing / shut down / sleep / hibernate / exit  (the stored index keeps the old numbering)
            int[] order = new int[] { 0, 3, 2, 4, 1 };
            for (int j = 0; j < order.Length; j++)
            {
                int idx = order[j];
                aft.Add(MItem.Choice(T(names[idx]), _dl.AfterIndex == idx, delegate { _dl.SetAfter(idx); }));
            }
            m.Add(MItem.Menu(T("When all downloads finish"), aft));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Scheduler..."), delegate { _dl.OpenScheduler(); }));
            m.Add(MItem.Of(T("Options..."), "Ctrl+O", delegate { OptionsWin.Show(0); }));
            return m;
        }

        private static List<MItem> View()
        {
            List<MItem> m = new List<MItem>();
            m.Add(MItem.Toggle(T("Show the categories"), _dl.ShowCategories, delegate { _dl.ShowCategories = !_dl.ShowCategories; }));
            List<MItem> srt = new List<MItem>();
            foreach (string[] k in DownloadsPage.SortKeys)
            {
                string key = k[0];
                srt.Add(MItem.Choice(T(k[1]), _dl.SortBy == key, delegate { _dl.SetSort(key); }));
            }
            m.Add(MItem.Menu(T("Arrange the files"), srt));
            m.Add(MItem.Line());
            List<MItem> lg = new List<MItem>();
            foreach (string[] l in Lang.List)
            {
                string nm = l[0];
                lg.Add(MItem.Choice(l[2], Lang.Name == nm, delegate { SettingsPage.SetLanguage(nm); }));
            }
            m.Add(MItem.Menu(T("Language"), lg));
            List<MItem> th = new List<MItem>();
            th.Add(MItem.Choice(T("Dark"), !Theme.Light, delegate { Ui.Settings.Set("Theme", "Dark"); SettingsPage.ApplyThemeChange(); }));
            th.Add(MItem.Choice(T("Light"), Theme.Light, delegate { Ui.Settings.Set("Theme", "Light"); SettingsPage.ApplyThemeChange(); }));
            th.Add(MItem.Line());
            th.Add(MItem.Choice(T("Modern style"), Theme.Modern, delegate { Ui.Settings.Set("UiStyle", "Modern"); SettingsPage.ApplyThemeChange(); }));
            th.Add(MItem.Choice(T("Classic style"), !Theme.Modern, delegate { Ui.Settings.Set("UiStyle", "Classic"); SettingsPage.ApplyThemeChange(); }));
            m.Add(MItem.Menu(T("Theme"), th));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("Appearance options..."), delegate { OptionsWin.Show(9); }));
            return m;
        }

        private static List<MItem> Help()
        {
            List<MItem> m = new List<MItem>();
            m.Add(MItem.Of(T("Add R-Downloader to your browser..."), delegate { ExtGuide.Show(true); }));
            m.Add(MItem.Of(T("Sounds..."), delegate { OptionsWin.Show(7); }));
            m.Add(MItem.Line());
            m.Add(MItem.Of(T("About R-Downloader"), delegate { OptionsWin.Show(11); }));
            return m;
        }
    }
}
