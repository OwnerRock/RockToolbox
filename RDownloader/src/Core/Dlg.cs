using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RDownloader.Native
{
    internal enum DlgKind { Info, Warn, Error, Question, Success }

    // Every message the tool shows (information, warning, error, question) uses this one themed window with animation
    // instead of the plain Windows message box.
    internal static class Dlg
    {
        private static void Look(DlgKind k, out string glyph, out string color, out string title)
        {
            switch (k)
            {
                case DlgKind.Warn: glyph = ""; color = "#FFB020"; title = "Warning"; break;
                case DlgKind.Error: glyph = ""; color = "#E5484D"; title = "Something went wrong"; break;
                case DlgKind.Question: glyph = ""; color = "#BF5AF2"; title = "Please confirm"; break;
                case DlgKind.Success: glyph = ""; color = "#30B96B"; title = "Done"; break;
                default: glyph = ""; color = "#2F8BFF"; title = "Information"; break;
            }
        }

        // a short first paragraph without punctuation becomes the heading, the rest is the message
        private static void Split(string text, string fallback, out string head, out string body)
        {
            head = fallback; body = (text ?? "").Trim();
            int i = body.IndexOf("\n\n", StringComparison.Ordinal);
            if (i > 0 && i <= 42)
            {
                string first = body.Substring(0, i).Trim();
                char last = first[first.Length - 1];
                if (last != '.' && last != '!' && last != '?' && last != ':' && first.IndexOf('\n') < 0)
                {
                    head = first; body = body.Substring(i).Trim();
                }
            }
        }

        public static bool Show(DlgKind kind, string text, string title, string yes, string no)
        {
            string glyph, color, defTitle; Look(kind, out glyph, out color, out defTitle);
            string head, body; Split(text, title ?? Lang.L(defTitle), out head, out body);
            Window w;
            try { w = XamlLoader.LoadWindow(Ui.Root, "ui/Dialog.xaml"); }
            catch
            {
                // the themed window could not be built: fall back to the system box so the message is never lost
                MessageBoxButton b = no != null ? MessageBoxButton.YesNo : MessageBoxButton.OK;
                return MessageBox.Show(text, "R-Downloader", b) != MessageBoxResult.No;
            }
            bool owned = Ui.Win != null && Ui.Win.IsVisible;
            if (owned) w.Owner = Ui.Win; else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (Lang.Rtl) w.FlowDirection = FlowDirection.RightToLeft;

            Brush c = (Brush)new BrushConverter().ConvertFromString(color);
            ((Ellipse)w.FindName("dlgIconBg")).Fill = c;
            ((Ellipse)w.FindName("dlgRing")).Stroke = c;
            ((TextBlock)w.FindName("dlgIcon")).Text = glyph;
            TextBlock tt = (TextBlock)w.FindName("dlgTitle"); tt.Text = head;
            TextBlock tx = (TextBlock)w.FindName("dlgText"); tx.Text = body;
            if (body.Length == 0) ((FrameworkElement)w.FindName("dlgScroll")).Visibility = Visibility.Collapsed;
            Button bYes = (Button)w.FindName("dlgYes"), bNo = (Button)w.FindName("dlgNo");
            bYes.Content = Lang.L(yes ?? "OK");
            if (no == null) bNo.Visibility = Visibility.Collapsed; else bNo.Content = Lang.L(no);

            bool result = false;
            bool closing = false;
            FrameworkElement root = (FrameworkElement)w.FindName("dlgRoot");
            Action<bool> end = delegate(bool r)
            {
                if (closing) return; closing = true; result = r;
                try
                {
                    DoubleAnimation fo = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(130)));
                    fo.Completed += delegate { w.Close(); };
                    root.BeginAnimation(UIElement.OpacityProperty, fo);
                }
                catch { w.Close(); }
            };
            bYes.Click += delegate { end(true); };
            bNo.Click += delegate { end(false); };
            ((Button)w.FindName("dlgClose")).Click += delegate { end(false); };
            w.PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { end(false); e.Handled = true; }
                else if (e.Key == Key.Enter) { end(true); e.Handled = true; }
            };
            w.Loaded += delegate
            {
                try
                {
                    Anim.PopIn(root, 0.5, 0.5);
                    Fx.Pop((UIElement)w.FindName("dlgIconBg"), 60);
                    Fx.Pop((UIElement)w.FindName("dlgIcon"), 120);
                    Fx.Ring((UIElement)w.FindName("dlgRing"), 3, 1.0, 1.7, 0.8, 200);
                    Fx.FadeUp(tt, 120, 14);
                    Fx.FadeUp((UIElement)w.FindName("dlgScroll"), 190, 14);
                    Fx.FadeUp((UIElement)w.FindName("dlgButtons"), 260, 14);
                    bYes.Focus();
                }
                catch { }
            };
            w.ShowDialog();
            return result;
        }

        public static bool Ask(string text) { return Show(DlgKind.Question, text, null, "Yes", "No"); }
        public static bool Ask(string text, string yes, string no) { return Show(DlgKind.Question, text, null, yes, no); }
        public static void Info(string text) { Show(DlgKind.Info, text, null, "OK", null); }
        public static void Warn(string text) { Show(DlgKind.Warn, text, null, "OK", null); }
        public static void Error(string text) { Show(DlgKind.Error, text, null, "OK", null); }
        public static void Done(string text) { Show(DlgKind.Success, text, null, "OK", null); }
    }
}
