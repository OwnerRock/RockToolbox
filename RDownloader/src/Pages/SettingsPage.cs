using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RDownloader.Native
{
    // Settings screen: look, language, tray / startup, .Rlist association, browser extension.
    internal sealed class SettingsPage : IPage
    {
        private bool _busy;
        private CheckBox _dark, _classic, _startup, _tray, _top, _rlist;
        private ComboBox _lang, _after;
        private CheckBox _sound, _clip;

        public string Key { get { return "Settings"; } }

        public void OnShown() { Sync(); }

        internal static void ApplyThemeChange()
        {
            Ui.Settings.SaveNow();
            Ui.Win.Dispatcher.BeginInvoke(new Action(UiHost.Reapply), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void Init(Window w, Logger log)
        {
            Current = this;
            _dark = Ui.Find<CheckBox>("chkDark"); _classic = Ui.Find<CheckBox>("chkClassic");
            _startup = Ui.Find<CheckBox>("chkStartup"); _tray = Ui.Find<CheckBox>("chkTray");
            _top = Ui.Find<CheckBox>("chkTop"); _rlist = Ui.Find<CheckBox>("chkRlist");
            _lang = Ui.Find<ComboBox>("cmbLang");
            _sound = Ui.Find<CheckBox>("chkSound"); _clip = Ui.Find<CheckBox>("chkClip"); _after = Ui.Find<ComboBox>("cmbAfter");
            foreach (string a in DownloadsPage.AfterActions) _after.Items.Add(Lang.L(a));
            _sound.Click += delegate { if (_busy) return; Ui.Settings.Set("SoundsOn", _sound.IsChecked == true); Ui.Settings.SaveSoon(); if (_sound.IsChecked == true) RSound.Play("Complete", true); };
            _clip.Click += delegate { if (_busy) return; Ui.Settings.Set("ClipWatch", _clip.IsChecked == true); Ui.Settings.SaveSoon(); };
            _after.SelectionChanged += delegate { if (_busy || _after.SelectedIndex < 0) return; Ui.Settings.Set("AfterAll", _after.SelectedIndex.ToString()); Ui.Settings.SaveSoon(); };
            foreach (string[] l in Lang.List) _lang.Items.Add(l[2]);

            _dark.Click += delegate
            {
                if (_busy) return;
                Ui.Settings.Set("Theme", _dark.IsChecked == true ? "Dark" : "Light");
                ApplyThemeChange();
            };
            _classic.Click += delegate
            {
                if (_busy) return;
                Ui.Settings.Set("UiStyle", _classic.IsChecked == true ? "Classic" : "Modern");
                ApplyThemeChange();
            };
            _lang.SelectionChanged += delegate
            {
                if (_busy || _lang.SelectedIndex < 0) return;
                SetLanguage(Lang.List[_lang.SelectedIndex][0]);
            };
            _startup.Click += delegate
            {
                if (_busy) return;
                try { AppLife.SetStartup(_startup.IsChecked == true); }
                catch (Exception ex) { Ui.Log.Error("Could not change the startup setting: " + ex.Message, "Settings"); _startup.IsChecked = AppLife.StartupEnabled(); }
            };
            _tray.Click += delegate { if (_busy) return; Ui.Settings.Set("CloseToTray", _tray.IsChecked == true); Ui.Settings.SaveSoon(); };
            _top.Click += delegate { if (_busy) return; Ui.Settings.Set("Topmost", _top.IsChecked == true); Ui.Settings.SaveSoon(); Ui.Win.Topmost = _top.IsChecked == true; };
            _rlist.Click += delegate
            {
                if (_busy) return;
                try { RlistAssoc.Set(_rlist.IsChecked == true); }
                catch (Exception ex) { Ui.Log.Error("Could not change the .Rlist association: " + ex.Message, "Settings"); _rlist.IsChecked = RlistAssoc.IsOn(); }
            };
            try
            {
                Button bLogs = new Button(); bLogs.Content = Lang.L("Show logs"); bLogs.Padding = new Thickness(18, 6, 18, 6); bLogs.HorizontalAlignment = HorizontalAlignment.Left; bLogs.Margin = new Thickness(0, 0, 0, 12);
                bLogs.Click += delegate { LogView.Show(false); };
                StackPanel spn = Ui.Find<ScrollViewer>("pnlSettings").Content as StackPanel;
                if (spn != null) spn.Children.Insert(2, bLogs);
            }
            catch (Exception) { }
            Ui.Find<Button>("btnSetExt").Click += delegate { ExtGuide.Show(true); };
            Ui.Find<Button>("btnSetData").Click += delegate
            {
                try { Tool.Start("explorer.exe", "\"" + UiHost.DataDir + "\""); } catch (Exception ex) { Ui.Log.Warn("Could not open the folder: " + ex.Message, "Settings"); }
            };
            Ui.Find<TextBlock>("txtAbout").Text = "R-Downloader " + Program.Version + "\n" + Lang.L("A fast download manager: multi-connection downloads, resume, queues, torrents and magnet links, Rlist playlists and a browser extension.");
            Sync();
        }

        internal static SettingsPage Current;
        internal void SyncNow() { Sync(); }

        private void Sync()
        {
            _busy = true;
            try
            {
                _dark.IsChecked = Ui.Settings.GetString("Theme") == "Dark";
                _classic.IsChecked = Ui.Settings.GetString("UiStyle") == "Classic";
                _tray.IsChecked = Ui.Settings.GetBool("CloseToTray", true);
                _top.IsChecked = Ui.Settings.GetBool("Topmost");
                _sound.IsChecked = Ui.Settings.GetBool("SoundsOn", true); _clip.IsChecked = Ui.Settings.GetBool("ClipWatch", false);
                int ai = 0; int.TryParse(Ui.Settings.GetString("AfterAll"), out ai); _after.SelectedIndex = ai >= 0 && ai < _after.Items.Count ? ai : 0;
                try { _startup.IsChecked = AppLife.StartupEnabled(); } catch (Exception) { _startup.IsChecked = false; }
                try { _rlist.IsChecked = RlistAssoc.IsOn(); } catch (Exception) { _rlist.IsChecked = false; }
                int i = 0; for (int k = 0; k < Lang.List.Length; k++) if (Lang.List[k][0] == Lang.Name) i = k;
                _lang.SelectedIndex = i;
            }
            finally { _busy = false; }
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
    }

    // .Rlist files open with R-Downloader (per user, no administrator rights and no UAC prompt)
    internal static class RlistAssoc
    {
        private const string Prog = "RDownloader.Rlist";

        public static bool IsOn()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + Prog + @"\shell\open\command"))
            {
                if (k == null) return false;
                string v = Convert.ToString(k.GetValue(""));
                return v.IndexOf(Path.GetFileName(AppLife.ExePath), StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static void Set(bool on)
        {
            if (!on)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + Prog, false); } catch (Exception) { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.Rlist", false); } catch (Exception) { }
                return;
            }
            string exe = AppLife.ExePath, dir = Path.GetDirectoryName(exe);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.Rlist")) { k.SetValue("", Prog); }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.Rlist\OpenWithProgids")) { k.SetValue(Prog, ""); }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog)) { k.SetValue("", "R-Downloader download list"); }
            string ico = Path.Combine(dir, "RRlist.ico"); if (!File.Exists(ico)) ico = exe;
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog + @"\DefaultIcon")) { k.SetValue("", ico); }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog + @"\shell\open\command")) { k.SetValue("", "\"" + exe + "\" \"%1\""); }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog + @"\shell\edit")) { k.SetValue("", "Edit in Notepad"); }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog + @"\shell\edit\command")) { k.SetValue("", "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe") + "\" \"%1\""); }
            try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch (Exception) { }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
    }
}
