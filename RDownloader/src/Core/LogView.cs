using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RDownloader.Native
{
    // The log window: shows native.log with colours, "Errors only", Copy, Clear, Refresh. Opened by the "Show logs" button
    // and by a click on an error message in the status bar (then it jumps to the last error).
    internal static class LogView
    {
        private static Window _win;
        private static RichTextBox _rtb;
        private static CheckBox _only;

        public static string LogFile { get { return Path.Combine(UiHost.DataDir, "native.log"); } }

        public static void Show(bool jumpToError)
        {
            if (_win == null)
            {
                Border host;
                _win = DlgKit.Make("Logs", 900, 560, out host);
                Window w = _win;
                DockPanel dp = new DockPanel(); dp.Margin = new Thickness(16, 14, 16, 14);
                WrapPanel bar = new WrapPanel(); bar.Margin = new Thickness(0, 0, 0, 10); DockPanel.SetDock(bar, Dock.Top);
                Button bCopy = DlgKit.Btn(w, "Copy", false), bClear = DlgKit.Btn(w, "Clear", false), bRef = DlgKit.Btn(w, "Refresh", false), bOpen = DlgKit.Btn(w, "Open log file", false);
                bCopy.Margin = new Thickness(0, 0, 8, 0); bClear.Margin = new Thickness(0, 0, 8, 0); bRef.Margin = new Thickness(0, 0, 8, 0); bOpen.Margin = new Thickness(0, 0, 12, 0);
                _only = DlgKit.Check(w, "Errors only", false); _only.VerticalAlignment = VerticalAlignment.Center;
                bar.Children.Add(bCopy); bar.Children.Add(bClear); bar.Children.Add(bRef); bar.Children.Add(bOpen); bar.Children.Add(_only);
                _rtb = new RichTextBox(); _rtb.IsReadOnly = true; _rtb.Background = Ui.Brush("#171C22"); _rtb.Foreground = Ui.Brush("#C9D2DD");
                _rtb.BorderBrush = Ui.Brush("#2A323C"); _rtb.FontFamily = new FontFamily("Consolas"); _rtb.FontSize = 12.5; _rtb.Padding = new Thickness(8);
                _rtb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; _rtb.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                dp.Children.Add(bar); dp.Children.Add(_rtb);
                host.Child = dp;
                bCopy.Click += delegate { try { Clipboard.SetText(new TextRange(_rtb.Document.ContentStart, _rtb.Document.ContentEnd).Text); } catch { } };
                bClear.Click += delegate { try { lock (typeof(LogView)) File.WriteAllText(LogFile, "", Encoding.UTF8); } catch { } Load(false); };
                bRef.Click += delegate { Load(false); };
                bOpen.Click += delegate { try { Tool.Start("notepad.exe", "\"" + LogFile + "\""); } catch { } };
                _only.Click += delegate { Load(false); };
                _win.Closed += delegate { _win = null; };
            }
            else if (jumpToError) _only.IsChecked = false;
            Load(jumpToError);
            if (!_win.IsVisible) _win.Show();
            if (_win.WindowState == WindowState.Minimized) _win.WindowState = WindowState.Normal;
            _win.Activate();
        }

        private static Button Btn(string t)
        {
            Button b = new Button(); b.Content = Lang.L(t); b.Padding = new Thickness(16, 5, 16, 5); b.Margin = new Thickness(0, 0, 8, 0);
            return b;
        }

        private static void Load(bool jump)
        {
            string[] lines = new string[0];
            try
            {
                using (FileStream fs = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    lines = sr.ReadToEnd().Replace("\r\n", "\n").Split('\n');
            }
            catch { }
            bool errOnly = _only.IsChecked == true;
            int start = Math.Max(0, lines.Length - 4000);
            Paragraph para = new Paragraph(); para.Margin = new Thickness(0);
            Run lastErr = null;
            for (int i = start; i < lines.Length; i++)
            {
                string l = lines[i]; if (l.Length == 0) continue;
                bool err = l.IndexOf("[ERROR]", StringComparison.Ordinal) >= 0, warn = l.IndexOf("[WARN]", StringComparison.Ordinal) >= 0;
                if (errOnly && !err && !warn) continue;
                Run r = new Run(l + "\n");
                r.Foreground = Ui.Brush(err ? "#FF6B6B" : (warn ? "#FFB454" : "#C9CDE0"));
                if (err) { r.FontWeight = FontWeights.SemiBold; lastErr = r; }
                para.Inlines.Add(r);
            }
            FlowDocument doc = new FlowDocument(para); doc.PageWidth = 4000;
            _rtb.Document = doc;
            if (jump && lastErr != null) _rtb.Dispatcher.BeginInvoke(new Action(delegate { try { lastErr.BringIntoView(); } catch { } }), System.Windows.Threading.DispatcherPriority.Background);
            else _rtb.Dispatcher.BeginInvoke(new Action(delegate { _rtb.ScrollToEnd(); }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
