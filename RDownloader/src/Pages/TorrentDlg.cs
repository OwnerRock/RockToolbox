using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RDownloader.Native
{
    // "Add a torrent / magnet link": paste magnet links or choose .torrent files
    internal static class TorrentDlg
    {
        public static void Show(DownloadsPage page)
        {
            Border host; Window w = DlgKit.Make("Add a torrent / magnet link", 560, 430, out host);
            StackPanel sp = new StackPanel(); sp.Margin = new Thickness(20, 16, 20, 16);
            sp.Children.Add(DlgKit.Head("Add a torrent"));
            sp.Children.Add(DlgKit.Label("Paste magnet links (one per line) or choose .torrent files.", true));
            TextBox urls = new TextBox(); urls.Style = (Style)w.FindResource("CalmBox"); urls.AcceptsReturn = true; urls.TextWrapping = TextWrapping.NoWrap;
            urls.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; urls.Height = 130; urls.Margin = new Thickness(0, 8, 0, 6); urls.VerticalContentAlignment = VerticalAlignment.Top; urls.Padding = new Thickness(4);
            sp.Children.Add(urls);
            try
            {
                string clip = Clipboard.GetText();
                if (!string.IsNullOrEmpty(clip) && clip.TrimStart().StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) urls.Text = clip.Trim();
            }
            catch (Exception) { }
            Button files = DlgKit.Btn(w, "Choose .torrent files...", false); files.HorizontalAlignment = HorizontalAlignment.Left; files.Margin = new Thickness(0, 0, 0, 10);
            files.Click += delegate
            {
                Microsoft.Win32.OpenFileDialog fd = new Microsoft.Win32.OpenFileDialog(); fd.Multiselect = true; fd.Filter = "Torrent files (*.torrent)|*.torrent";
                if (fd.ShowDialog() == true) foreach (string f in fd.FileNames) urls.AppendText((urls.Text.Length > 0 && !urls.Text.EndsWith("\n") ? "\r\n" : "") + f + "\r\n");
            };
            sp.Children.Add(files);
            TextBox dir = DlgKit.Box(w, page.DlDir);
            sp.Children.Add(DlgKit.Row("Save to", DlgKit.Browse(w, dir, delegate { return DlgKit.PickFolder(dir.Text); }), 80));
            CheckBox start = DlgKit.Check(w, "Start downloading now", true);
            sp.Children.Add(start);
            TextBlock err = DlgKit.Label("", false); err.Foreground = Ui.Brush("#E5604D"); err.Visibility = Visibility.Collapsed; sp.Children.Add(err);
            StackPanel btns = new StackPanel(); btns.Orientation = Orientation.Horizontal; btns.HorizontalAlignment = HorizontalAlignment.Right; btns.Margin = new Thickness(0, 12, 0, 0);
            Button ok = DlgKit.Btn(w, "Add", true), cancel = DlgKit.Btn(w, "Cancel", false);
            btns.Children.Add(ok); btns.Children.Add(cancel); sp.Children.Add(btns);
            cancel.Click += delegate { w.Close(); };
            ok.Click += delegate
            {
                List<string> l = new List<string>();
                foreach (string x in urls.Text.Split('\n'))
                {
                    string s = x.Trim().Trim('"'); if (s.Length == 0) continue;
                    if (Aria.IsTorrentLink(s) && (s.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || File.Exists(s) || s.StartsWith("http", StringComparison.OrdinalIgnoreCase))) l.Add(s);
                }
                if (l.Count == 0) { err.Text = Lang.L("Enter at least one magnet link or choose a .torrent file."); err.Visibility = Visibility.Visible; return; }
                if (dir.Text.Trim().Length == 0) { err.Text = Lang.L("Choose a folder to save the files in."); err.Visibility = Visibility.Visible; return; }
                page.AddItems(l.ToArray(), dir.Text.Trim(), "", start.IsChecked == true, true, false, false);
                w.Close();
            };
            host.Child = sp;
            w.ShowDialog();
        }

        // a magnet link or .torrent file that was opened from outside (browser, Explorer): added at once, to the default folder
        public static void AddLink(string link)
        {
            DownloadsPage p = DownloadsPage.Instance;
            if (p == null) return;
            p.AddItems(new string[] { link }, p.DlDir, "", true, true, false, false);
            Ui.Log.Info("Torrent added: " + (Aria.IsMagnet(link) ? Aria.NameOf(link) : Path.GetFileName(link)), "Downloads");
        }
    }

    // magnet: links and .torrent files open with R-Downloader (per user) + a Windows Firewall rule for the torrent engine
    internal static class TorrentAssoc
    {
        private const string Prog = "RDownloader.Torrent";

        private static string Cmd { get { return "\"" + AppLife.ExePath + "\" \"%1\""; } }

        public static bool IsOn()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\magnet\shell\open\command"))
                {
                    if (k == null) return false;
                    return Convert.ToString(k.GetValue("")).IndexOf(Path.GetFileName(AppLife.ExePath), StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception) { return false; }
        }

        public static void Set(bool on)
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\magnet", false);
            if (!on)
            {
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + Prog, false); } catch (Exception) { }
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.torrent", false); } catch (Exception) { }
                return;
            }
            string ico = AppLife.ExePath;
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\magnet")) { k.SetValue("", "URL:magnet"); k.SetValue("URL Protocol", ""); }
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\magnet\DefaultIcon")) { k.SetValue("", ico); }
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\magnet\shell\open\command")) { k.SetValue("", Cmd); }
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\.torrent")) { k.SetValue("", Prog); }
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog)) { k.SetValue("", "Torrent file"); }
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog + @"\DefaultIcon")) { k.SetValue("", ico); }
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Prog + @"\shell\open\command")) { k.SetValue("", Cmd); }
        }

        // allow aria2c through the firewall (asks for administrator rights once)
        public static void AllowFirewall()
        {
            string exe;
            try { exe = Aria.Ensure(null); } catch (Exception ex) { Ui.Warn("The torrent engine (aria2) is not ready yet.\n\nAdd a torrent first so it can be downloaded.\n\n" + ex.Message); return; }
            string args = "advfirewall firewall add rule name=\"R-Downloader torrents\" dir=in action=allow program=\"" + exe + "\" enable=yes profile=any";
            try
            {
                System.Diagnostics.ProcessStartInfo si = new System.Diagnostics.ProcessStartInfo("netsh.exe", args);
                si.Verb = "runas"; si.UseShellExecute = true; si.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(si); p.WaitForExit();
                Ui.Info(p.ExitCode == 0 ? "The firewall rule was added." : "The firewall rule could not be added.");
            }
            catch (Exception) { Ui.Log.Warn("The firewall request was cancelled.", "Torrents"); }
        }
    }
}
