using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace RockToolbox.Native
{
    // The "Check for updates" window (ui/Update.xaml) and the check itself.
    // Source: the latest release of github.com/OwnerRock/RockToolbox. The installer (.exe) attached to that release is downloaded here.
    internal sealed partial class SettingsPage
    {
        private sealed class UpdResult { public string Tag = "", Url = "", Err = "", Asset = ""; }

        // runs the check on a worker thread, tries the four GitHub sources one after another, then calls done on the UI thread
        private static void CheckAsync(bool probeOnline, Action<UpdResult> done)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                UpdResult r = new UpdResult();
                string tag = "", url = RepoUrl + "/releases/latest";
                Exception lastEx = null;
                _assetUrl = "";
                for (int src = 0; src < 4 && tag.Length == 0; src++)
                {
                    for (int attempt = 0; attempt < 2 && tag.Length == 0; attempt++)
                    {
                        try { tag = UpdSource(src, ref url); }
                        catch (Exception ex)
                        {
                            lastEx = ex; tag = "";
                            if (attempt == 0) System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                // sources 1-3 give only the version: look for the installer (.exe) in the release page itself
                if (tag.Length > 0 && string.IsNullOrEmpty(_assetUrl))
                {
                    try
                    {
                        string html = Net.GetString(RepoUrl + "/releases/expanded_assets/" + Uri.EscapeDataString(tag), 15000);
                        System.Text.RegularExpressions.Match am = System.Text.RegularExpressions.Regex.Match(html ?? "", "href=\"(/OwnerRock/RockToolbox/releases/download/[^\"]+?\\.exe)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (am.Success) _assetUrl = "https://github.com" + am.Groups[1].Value.Replace("&amp;", "&");
                    }
                    catch { }
                }
                r.Tag = tag; r.Url = url; r.Asset = _assetUrl ?? "";
                if (tag.Length == 0)
                {
                    r.Err = lastEx == null ? "Unknown error." : Net.FriendlyError(lastEx);
                    if (probeOnline && !Net.IsOnline()) r.Err = "No internet connection or DNS cannot reach GitHub.";
                }
                Ui.Post(delegate { done(r); });
            });
        }

        // remembers the result, shows / hides the red dots; returns true when a newer version exists
        private static bool ApplyResult(UpdResult r)
        {
            bool newer = Parse(r.Tag) > Parse(Version);
            Ui.Settings.Set("UpdLatest", r.Tag); Ui.Settings.Set("UpdCheckedAt", DateTime.Now.ToString("s")); Ui.Settings.SaveSoon();
            try { Ui.Find<System.Windows.Shapes.Ellipse>("dotGearUpdate").Visibility = newer ? Visibility.Visible : Visibility.Collapsed; } catch { }
            try { Ui.Find<System.Windows.Shapes.Ellipse>("dotGear").Visibility = newer ? Visibility.Visible : Visibility.Collapsed; } catch { }
            return newer;
        }

        private static Window _updWin;

        private static void ShowUpdateWindow()
        {
            if (_updWin != null) { try { _updWin.Activate(); } catch { } return; }
            Window w;
            try { w = XamlLoader.LoadWindow(Ui.Root, "ui/Update.xaml"); }
            catch (Exception ex) { Ui.Log.Error("Could not open the update window: " + ex.Message, "Update"); return; }
            _updWin = w;
            if (Ui.Win != null && Ui.Win.IsVisible) w.Owner = Ui.Win; else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (Lang.Rtl) w.FlowDirection = FlowDirection.RightToLeft;

            TextBlock cur = (TextBlock)w.FindName("updCurrent"), latest = (TextBlock)w.FindName("updLatest"), src = (TextBlock)w.FindName("updSource");
            TextBlock st = (TextBlock)w.FindName("updStatus"), chk = (TextBlock)w.FindName("updChecked"), pct = (TextBlock)w.FindName("updPct");
            Button bCheck = (Button)w.FindName("btnUpdCheck"), bNow = (Button)w.FindName("btnUpdNow"), bDns = (Button)w.FindName("btnUpdDns"), bClose = (Button)w.FindName("btnUpdClose");
            ProgressBar pb = (ProgressBar)w.FindName("updPb");
            FrameworkElement prog = (FrameworkElement)w.FindName("updProgress");

            cur.Text = "V" + Version;
            src.Text = "GitHub Releases  -  " + RepoUrl.Replace("https://", "");
            LinkTip.Set(src, RepoUrl + "/releases", "Release page");
            src.MouseLeftButtonUp += delegate { try { Process.Start(RepoUrl + "/releases"); } catch { } };
            string was = Ui.Settings.GetString("UpdCheckedAt");
            DateTime wasAt; if (was.Length > 0 && DateTime.TryParse(was, out wasAt)) chk.Text = Lang.L("Last checked") + ": " + wasAt.ToString("dd-MM-yyyy hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            string was2 = Ui.Settings.GetString("UpdLatest"); if (was2.Length > 0) latest.Text = "V" + was2.TrimStart('v', 'V');

            Action<string, string> setStatus = delegate(string text, string color)
            {
                st.Text = Lang.L(text); st.Foreground = Ui.Brush(color);
                st.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(260))));
            };

            UpdResult last = null;
            bool busy = false, downloading = false, cancel = false;

            Action check = null;
            check = delegate
            {
                if (busy || downloading) return;
                busy = true;
                bCheck.IsEnabled = false; bNow.Visibility = Visibility.Collapsed; bDns.Visibility = Visibility.Collapsed;
                setStatus("Checking for updates...", "#C9CDE0");
                CheckAsync(true, delegate(UpdResult r)
                {
                    busy = false;
                    if (!w.IsVisible) return;
                    bCheck.IsEnabled = true; last = r;
                    if (r.Tag.Length == 0)
                    {
                        latest.Text = "-";
                        setStatus("Could not check for updates:\n" + r.Err, "#E5484D");
                        Ui.Log.Warn("Could not check for updates: " + r.Err, "Update");
                        if (r.Err.IndexOf("DNS", StringComparison.OrdinalIgnoreCase) >= 0) { bDns.Visibility = Visibility.Visible; Anim.PopIn(bDns, 0.5, 0.5); }
                        return;
                    }
                    bool newer = ApplyResult(r);
                    latest.Text = "V" + r.Tag.TrimStart('v', 'V');
                    chk.Text = Lang.L("Last checked") + ": " + DateTime.Now.ToString("dd-MM-yyyy hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);
                    if (r.Asset.Length > 0) { src.Text = "GitHub Releases  -  " + new Uri(r.Asset).Host; LinkTip.Set(src, r.Asset, "Download link"); }
                    if (newer)
                    {
                        setStatus("A new version is available.", "#FFB454");
                        bNow.Content = Lang.L(r.Asset.Length > 0 ? "Update now" : "Open download page");
                        bNow.Visibility = Visibility.Visible; bNow.IsEnabled = true; Anim.PopIn(bNow, 0.5, 0.5);
                    }
                    else setStatus("You are up to date.", "#3DDC84");
                });
            };

            bCheck.Click += delegate { check(); };
            bDns.Click += delegate
            {
                bDns.Visibility = Visibility.Collapsed;
                TweaksPage.UseCloudflareDns(delegate { if (w.IsVisible) check(); });
            };
            Action close = delegate { cancel = true; w.Close(); };
            bClose.Click += delegate { close(); };
            ((Button)w.FindName("btnTitleClose")).Click += delegate { close(); };
            w.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Escape) { close(); e.Handled = true; } };
            w.Closed += delegate { cancel = true; _updWin = null; };

            bNow.Click += delegate
            {
                if (last == null || last.Tag.Length == 0 || downloading) return;
                if (last.Asset.Length == 0) { try { Process.Start(last.Url); } catch { } return; }
                downloading = true; cancel = false;
                string asset = last.Asset;
                string file = Path.Combine(Path.GetTempPath(), "RockToolbox-Update-" + last.Tag.TrimStart('v', 'V') + ".exe");
                bNow.IsEnabled = false; bCheck.IsEnabled = false;
                prog.Visibility = Visibility.Visible; Anim.PopIn(prog, 0.5, 0.0);
                pb.BeginAnimation(ProgressBar.ValueProperty, null); pb.Value = 0; pct.Text = "0%";
                setStatus("Downloading the update...", "#C9CDE0");
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    bool ok = false; string err = "";
                    try
                    {
                        Net.Download(asset, file, delegate(long done, long total)
                        {
                            if (total <= 0) return;
                            double p = done * 100.0 / total;
                            string txt = (done / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " / " + (total / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB   (" + p.ToString("0") + "%)";
                            Ui.Post(delegate
                            {
                                try { pb.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(p, new Duration(TimeSpan.FromMilliseconds(300)))); pct.Text = txt; } catch { }
                            });
                        }, delegate { return cancel; });
                        ok = File.Exists(file) && new FileInfo(file).Length > 100000;
                        if (!ok) err = "The downloaded file is incomplete.";
                    }
                    catch (OperationCanceledException) { err = ""; }
                    catch (Exception ex) { err = Net.FriendlyError(ex); }
                    Ui.Post(delegate
                    {
                        downloading = false;
                        if (!w.IsVisible) return;
                        if (!ok)
                        {
                            prog.Visibility = Visibility.Collapsed; bNow.IsEnabled = true; bCheck.IsEnabled = true;
                            setStatus("Could not download the update:\n" + (err.Length > 0 ? err : "cancelled"), "#E5484D");
                            return;
                        }
                        pb.BeginAnimation(ProgressBar.ValueProperty, null); pb.Value = 100; pct.Text = "100%";
                        setStatus("Starting the installer...", "#3DDC84");
                        try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); AppLife.Exit(); }
                        catch (Exception ex)
                        {
                            bNow.IsEnabled = true; bCheck.IsEnabled = true;
                            setStatus("Could not start the installer:\n" + ex.Message + "\nIt was saved here: " + file, "#E5484D");
                        }
                    });
                });
            };

            w.Loaded += delegate
            {
                try
                {
                    Panel sp = ((Border)w.FindName("updRoot")).Child as Panel;
                    int i = 0;
                    if (sp != null) foreach (UIElement c in sp.Children) { Fx.FadeUp(c, 40 + i * 80, 16); i++; }
                    Fx.Pop((UIElement)w.FindName("updLogo"), 40);
                }
                catch { }
                check();
            };
            w.Show();
        }
    }
}
