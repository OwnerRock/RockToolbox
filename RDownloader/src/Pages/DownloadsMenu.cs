using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RDownloader.Native
{
    // the actions behind the menu bar (Tasks / File / Downloads / View) - they work on the list of this page
    internal sealed partial class DownloadsPage
    {
        // ---- arrange files ----
        public static readonly string[][] SortKeys = new string[][]
        {
            new string[] { "added", "By order of addition" }, new string[] { "name", "File name" }, new string[] { "size", "Size" },
            new string[] { "status", "Status" }, new string[] { "left", "Time left" }, new string[] { "rate", "Transfer rate" },
            new string[] { "last", "Last try date" }, new string[] { "dir", "Save to" }
        };
        internal string SortBy { get { string s = Ui.Settings.GetString("DlSort"); return s.Length == 0 ? "added" : s; } }
        internal void SetSort(string key) { Ui.Settings.Set("DlSort", key); Ui.Settings.SaveSoon(); ApplyFilter(); }

        private void SortList(List<RockDownload> list)
        {
            string k = SortBy;
            Comparison<RockDownload> cmp = null;
            if (k == "name") cmp = delegate(RockDownload a, RockDownload b) { return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase); };
            else if (k == "size") cmp = delegate(RockDownload a, RockDownload b) { return b.Total.CompareTo(a.Total); };
            else if (k == "status") cmp = delegate(RockDownload a, RockDownload b) { return string.Compare(a.Status, b.Status, StringComparison.Ordinal); };
            else if (k == "left") cmp = delegate(RockDownload a, RockDownload b) { return (b.Total - b.Done).CompareTo(a.Total - a.Done); };
            else if (k == "rate") cmp = delegate(RockDownload a, RockDownload b) { return b.SmSpeed.CompareTo(a.SmSpeed); };
            else if (k == "last") cmp = delegate(RockDownload a, RockDownload b) { return string.Compare(b.Added ?? "", a.Added ?? "", StringComparison.Ordinal); };
            else if (k == "dir") cmp = delegate(RockDownload a, RockDownload b) { return string.Compare(a.Dir, b.Dir, StringComparison.OrdinalIgnoreCase); };
            if (cmp == null) return;
            // stable sort (List.Sort is not): decorate with the original position
            List<KeyValuePair<int, RockDownload>> tmp = new List<KeyValuePair<int, RockDownload>>();
            for (int i = 0; i < list.Count; i++) tmp.Add(new KeyValuePair<int, RockDownload>(i, list[i]));
            tmp.Sort(delegate(KeyValuePair<int, RockDownload> x, KeyValuePair<int, RockDownload> y) { int c = cmp(x.Value, y.Value); return c != 0 ? c : x.Key.CompareTo(y.Key); });
            for (int i = 0; i < tmp.Count; i++) list[i] = tmp[i].Value;
        }

        // ---- when all downloads finish ----
        internal int AfterIndex { get { int a = 0; int.TryParse(Ui.Settings.GetString("AfterAll"), out a); return a; } }
        internal void SetAfter(int i) { Ui.Settings.Set("AfterAll", i.ToString()); Ui.Settings.SaveSoon(); }

        // ---- Tasks ----
        internal void AddNew() { OpenInfoFromClipboard(); }
        internal void AddMany() { AddDialog(); }
        internal void OpenRlistFile() { OpenRlistPicker(); }

        internal void AddFromClipboard()
        {
            List<string> found = new List<string>();
            try
            {
                string clip = Clipboard.GetText();
                if (!string.IsNullOrEmpty(clip))
                    foreach (string l in clip.Split(new char[] { '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)) if (Regex.IsMatch(l, @"^(https?://|magnet:\?)\S+$")) found.Add(l);
            }
            catch (Exception) { }
            if (found.Count == 0) { Ui.Warn("There are no download links in the clipboard.\n\nCopy some links first (one per line), then try again."); return; }
            AddItems(found.ToArray(), DlDir, "", true, true, false, false);
            Ui.Log.Info("Added " + found.Count + " link(s) from the clipboard.", "Downloads");
        }

        internal void ExportList()
        {
            try
            {
                Microsoft.Win32.SaveFileDialog fd = new Microsoft.Win32.SaveFileDialog();
                fd.Title = "Export the download list"; fd.Filter = "Rlist files (*.Rlist)|*.Rlist"; fd.FileName = "downloads.Rlist";
                if (fd.ShowDialog() != true) return;
                Rlist rl = new Rlist(); rl.Path = fd.FileName; rl.Name = "R-Downloader list"; rl.Dir = DlDir;
                foreach (RockDownload it in _items) if (it.Status != "Cancelled") rl.Entries.Add(new RlistEntry(it.Url, it.FileName));
                rl.Save();
                Ui.Log.Info("Exported " + rl.Entries.Count + " link(s) to " + fd.FileName, "Downloads");
            }
            catch (Exception ex) { Ui.Log.Warn("Could not export the list: " + ex.Message, "Downloads"); }
        }

        // ---- File ----
        private RockDownload One() { return _lv.SelectedItem as RockDownload; }
        internal bool HasSelection { get { return _lv.SelectedItems.Count > 0; } }
        internal bool OneCompleted { get { RockDownload it = One(); return it != null && it.Status == "Completed" && File.Exists(it.FullPath); } }
        internal void SelectAllRows() { _lv.Focus(); _lv.SelectAll(); }
        internal void RemoveSelected() { Cancel(); }
        internal void ResumeSelected() { Resume(); }
        internal void StopSelected() { Pause(); }
        internal void OpenFolderNow() { OpenFolder(); }

        internal void OpenSelected()
        {
            RockDownload it = One(); if (it == null) return;
            try { if (File.Exists(it.FullPath)) Tool.Start(it.FullPath, ""); else Ui.Warn("File not found\n\n" + it.FullPath); } catch (Exception ex) { Ui.Log.Warn("Could not open the file: " + ex.Message, "Downloads"); }
        }

        internal void OpenWithSelected()
        {
            RockDownload it = One(); if (it == null || !File.Exists(it.FullPath)) return;
            try { Tool.Start("rundll32.exe", "shell32.dll,OpenAs_RunDLL \"" + it.FullPath + "\""); } catch (Exception ex) { Ui.Log.Warn("Could not open the dialog: " + ex.Message, "Downloads"); }
        }

        internal void CopyAddressSelected()
        {
            StringBuilder sb = new StringBuilder();
            foreach (RockDownload it in Targets(false)) { if (sb.Length > 0) sb.Append("\r\n"); sb.Append(it.Url); }
            if (sb.Length == 0) return;
            try { Clipboard.SetText(sb.ToString()); Ui.Log.Info("Link copied.", "Downloads"); } catch (Exception) { }
        }

        internal void RedownloadSelected()
        {
            List<RockDownload> t = Targets(false);
            if (t.Count == 0) return;
            if (!Ui.Confirm("Download " + t.Count + " file(s) again from the start?\n\nThe files already downloaded will be replaced.")) return;
            foreach (RockDownload it in t)
            {
                IDlJob eng;
                if (_jobs.TryGetValue(it.Id, out eng)) { try { eng.Stop("cancel"); } catch (Exception) { } }
                _engines.Remove(it.Id);
                try { File.Delete(it.FullPath + ".part"); } catch (Exception) { }
                if (it.Status == "Completed") { try { File.Delete(it.FullPath); } catch (Exception) { } }
                it.Done = 0; it.Seg = ""; it.Progress = 0; it.PctText = "0%"; it.Retries = 0; it.SmInit = false;
                if (!_jobs.ContainsKey(it.Id)) SetStatus(it, "Queued");
            }
            Tick(); SaveSoon();
        }

        internal void MoveRenameSelected()
        {
            RockDownload it = One(); if (it == null) return;
            if (it.Status != "Completed" || !File.Exists(it.FullPath)) { Ui.Warn("Only finished downloads can be moved or renamed."); return; }
            try
            {
                Microsoft.Win32.SaveFileDialog fd = new Microsoft.Win32.SaveFileDialog();
                fd.Title = "Move / Rename"; fd.FileName = it.FileName; fd.InitialDirectory = it.Dir; fd.OverwritePrompt = true;
                if (fd.ShowDialog() != true) return;
                if (string.Equals(fd.FileName, it.FullPath, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(fd.FileName)) File.Delete(fd.FileName);
                File.Move(it.FullPath, fd.FileName);
                it.FullPath = fd.FileName; it.Dir = Path.GetDirectoryName(fd.FileName); it.FileName = Path.GetFileName(fd.FileName);
                SaveSoon(); ApplyFilter();
            }
            catch (Exception ex) { Ui.Log.Warn("Could not move the file: " + ex.Message, "Downloads"); }
        }

        internal void PropertiesSelected()
        {
            RockDownload it = One(); if (it == null) return;
            StringBuilder sb = new StringBuilder();
            sb.Append(it.FileName).Append("\n\n");
            sb.Append("Status:  ").Append(it.StatusText).Append('\n');
            sb.Append("Size:  ").Append(it.Total > 0 ? FormatSize(it.Total) + " (" + it.Total.ToString("N0") + " bytes)" : "unknown").Append('\n');
            sb.Append("Saved to:  ").Append(it.Dir).Append('\n');
            sb.Append("Address:  ").Append(it.Url).Append('\n');
            sb.Append("Added:  ").Append(FormatAdded(it.Added)).Append('\n');
            if (!string.IsNullOrEmpty(it.LastTry)) sb.Append("Last try:  ").Append(it.LastTry);
            Ui.Info(sb.ToString());
        }

        private static string FormatAdded(string iso)
        {
            DateTime d; if (DateTime.TryParse(iso ?? "", null, DateTimeStyles.RoundtripKind, out d)) return d.ToString("yyyy-MM-dd HH:mm");
            return "-";
        }

        // ---- Downloads ----
        internal void ResumeAll()
        {
            foreach (RockDownload it in new List<RockDownload>(_items))
                if (!_jobs.ContainsKey(it.Id) && (it.Status == "Paused" || it.Status == "Failed")) { it.Retries = 0; SetStatus(it, "Queued"); }
            _queueStopped = false; Tick(); SaveSoon();
        }
        internal void StopAll() { foreach (RockDownload it in new List<RockDownload>(_items)) PauseItem(it); SaveSoon(); }
        internal void ClearFinished()
        {
            int n = _items.RemoveAll(delegate(RockDownload d) { return d.Status == "Completed" || d.Status == "Failed" || d.Status == "Cancelled"; });
            ApplyFilter(); Ui.Log.Info("Cleared " + n + " finished download(s).", "Downloads"); SaveSoon();
        }
        internal void StartQueueNow() { RSound.Play("QueueStart"); ResumeAll(); _info.Text = ""; }
        internal void StopQueueNow() { _queueStopped = true; _info.Text = Lang.L("Queue stopped - waiting downloads will not start."); }
        internal void FocusFind() { _search.Focus(); _search.SelectAll(); }

        internal void FindNext()
        {
            string q = (_search.Text ?? "").Trim();
            if (q.Length == 0) { FocusFind(); return; }
            List<RockDownload> src = _lv.ItemsSource as List<RockDownload>;
            if (src == null || src.Count == 0) return;
            int start = _lv.SelectedIndex;
            for (int n = 1; n <= src.Count; n++)
            {
                RockDownload it = src[(start + n + src.Count) % src.Count];
                if (it.FileName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || it.Url.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                { _lv.SelectedItem = it; _lv.ScrollIntoView(it); return; }
            }
        }

        // limit choices of the toolbar combo (same list)
        internal static string[] LimitList { get { return LimitNames; } }
        internal int LimitIndex { get { return _limit.SelectedIndex; } set { _limit.SelectedIndex = value; } }

        // ---- View ----
        internal bool ShowCategories
        {
            get { return Ui.Settings.GetBool("ShowCats", true); }
            set
            {
                Ui.Settings.Set("ShowCats", value); Ui.Settings.SaveSoon();
                DependencyObject box = _tree;
                while (box != null && !(box is Border)) box = LogicalTreeHelper.GetParent(box);
                if (box != null) ((UIElement)box).Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        internal void ApplyViewState() { ShowCategories = ShowCategories; }

        // ---- Scheduler: start the queue / stop everything at a time of day ----
        private DateTime _schedDay = DateTime.MinValue;
        private int _schedPhase;
        private void RunScheduler()
        {
            if (!Ui.Settings.GetBool("SchedOn", false)) return;
            DateTime now = DateTime.Now;
            if (_schedDay.Date != now.Date) { _schedDay = now.Date; _schedPhase = 0; }
            TimeSpan st, sp;
            bool hasStart = TimeSpan.TryParse(Ui.Settings.GetString("SchedStart"), out st), hasStop = TimeSpan.TryParse(Ui.Settings.GetString("SchedStop"), out sp);
            if (hasStart && _schedPhase < 1 && now.TimeOfDay >= st && (!hasStop || sp <= st || now.TimeOfDay < sp)) { _schedPhase = 1; Ui.Log.Info("Scheduler: starting the queue.", "Downloads"); StartQueueNow(); }
            if (hasStop && _schedPhase < 2 && now.TimeOfDay >= sp && (!hasStart || sp > st || _schedPhase == 1)) { if (_schedPhase == 1 || !hasStart) { Ui.Log.Info("Scheduler: stopping the downloads.", "Downloads"); StopAll(); } _schedPhase = 2; }
        }

        internal void OpenScheduler()
        {
            Window w = new Window(); w.Title = Lang.L("Scheduler"); w.Width = 400; w.SizeToContent = SizeToContent.Height; w.ResizeMode = ResizeMode.NoResize; w.Owner = Ui.Win;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner; w.ShowInTaskbar = false; w.Background = Ui.Brush("#1F2329"); w.Foreground = Ui.Brush("#E6E8F2"); w.FontSize = 13;
            StackPanel sp = new StackPanel(); sp.Margin = new Thickness(20);
            CheckBox on = new CheckBox(); on.Content = Lang.L("Run the scheduler every day"); on.IsChecked = Ui.Settings.GetBool("SchedOn", false); on.Foreground = w.Foreground; on.Margin = new Thickness(0, 0, 0, 12);
            Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) }); g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            TextBlock l1 = new TextBlock(); l1.Text = Lang.L("Start downloading at"); l1.VerticalAlignment = VerticalAlignment.Center;
            TextBlock l2 = new TextBlock(); l2.Text = Lang.L("Stop downloading at"); l2.VerticalAlignment = VerticalAlignment.Center; Grid.SetRow(l2, 1);
            TextBox t1 = new TextBox(); t1.Text = Ui.Settings.GetString("SchedStart"); t1.Width = 90; t1.HorizontalAlignment = HorizontalAlignment.Left; t1.Height = 26; t1.VerticalContentAlignment = VerticalAlignment.Center; Grid.SetColumn(t1, 1);
            TextBox t2 = new TextBox(); t2.Text = Ui.Settings.GetString("SchedStop"); t2.Width = 90; t2.HorizontalAlignment = HorizontalAlignment.Left; t2.Height = 26; t2.VerticalContentAlignment = VerticalAlignment.Center; Grid.SetColumn(t2, 1); Grid.SetRow(t2, 1);
            g.Children.Add(l1); g.Children.Add(l2); g.Children.Add(t1); g.Children.Add(t2);
            TextBlock hint = new TextBlock(); hint.Text = Lang.L("Use 24-hour time, like 02:30. Leave a box empty to skip that step."); hint.Foreground = Ui.Brush("#9AA0B8"); hint.FontSize = 11.5; hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(0, 6, 0, 14);
            StackPanel btns = new StackPanel(); btns.Orientation = Orientation.Horizontal; btns.HorizontalAlignment = HorizontalAlignment.Right;
            Button ok = new Button(); ok.Content = Lang.L("OK"); ok.Padding = new Thickness(22, 6, 22, 6); ok.IsDefault = true; ok.Margin = new Thickness(0, 0, 8, 0);
            Button cancel = new Button(); cancel.Content = Lang.L("Cancel"); cancel.Padding = new Thickness(18, 6, 18, 6); cancel.IsCancel = true;
            btns.Children.Add(ok); btns.Children.Add(cancel);
            sp.Children.Add(on); sp.Children.Add(g); sp.Children.Add(hint); sp.Children.Add(btns); w.Content = sp;
            ok.Click += delegate
            {
                TimeSpan x;
                if (t1.Text.Trim().Length > 0 && !TimeSpan.TryParse(t1.Text.Trim(), out x)) { Ui.Warn("The start time is not valid. Use 24-hour time like 02:30."); return; }
                if (t2.Text.Trim().Length > 0 && !TimeSpan.TryParse(t2.Text.Trim(), out x)) { Ui.Warn("The stop time is not valid. Use 24-hour time like 02:30."); return; }
                Ui.Settings.Set("SchedOn", on.IsChecked == true); Ui.Settings.Set("SchedStart", t1.Text.Trim()); Ui.Settings.Set("SchedStop", t2.Text.Trim()); Ui.Settings.SaveSoon();
                _schedDay = DateTime.MinValue; w.DialogResult = true;
            };
            w.ShowDialog();
        }
    }
}
