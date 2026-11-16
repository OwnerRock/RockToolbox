using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RDownloader.Native
{
    // Shared UI context (main window, logger, settings) + small helpers every page uses.
    internal static class Ui
    {
        public static Window Win;
        public static Logger Log;
        public static AppSettings Settings;
        public static string Root;      // folder of the program (ui, lang, assets live next to it)

        private static readonly BrushConverter Conv = new BrushConverter();

        public static T Find<T>(string name) where T : class
        {
            object o = Win.FindName(name);
            T t = o as T;
            if (t == null) throw new InvalidOperationException("UI control not found: " + name);
            return t;
        }

        public static Brush Brush(string hex)
        {
            if (Theme.IsGrad(hex)) return Theme.Grad();
            return (Brush)Conv.ConvertFromString(Theme.Map(DlTheme.Map(hex)));
        }

        // an action row (icon + text) in the card style - used for buttons that are made in code
        public static Button Row(string text, string glyph)
        {
            Button b = new Button();
            try { b.Style = (Style)Win.FindResource("ActRow"); } catch { }
            DockPanel dp = new DockPanel();
            TextBlock g = new TextBlock(); g.Text = glyph; g.FontFamily = new FontFamily("Segoe MDL2 Assets"); g.FontSize = 15; g.Width = 26; g.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(g, Dock.Left);
            TextBlock t = new TextBlock(); t.Text = Lang.L(text); t.VerticalAlignment = VerticalAlignment.Center; t.TextWrapping = TextWrapping.Wrap;
            dp.Children.Add(g); dp.Children.Add(t); b.Content = dp;
            return b;
        }

        public static void Post(Action a)
        {
            if (Win.Dispatcher.CheckAccess()) a(); else Win.Dispatcher.BeginInvoke(a);
        }

        public static void Invoke(Action a)
        {
            if (Win.Dispatcher.CheckAccess()) a(); else Win.Dispatcher.Invoke(a);
        }

        public static T Invoke<T>(Func<T> f)
        {
            if (Win.Dispatcher.CheckAccess()) return f();
            return (T)Win.Dispatcher.Invoke(f);
        }

        public static bool Confirm(string text)
        {
            return Invoke<bool>(delegate { return Dlg.Ask(text); });
        }

        public static void Info(string text)
        {
            Invoke(delegate { Dlg.Info(text); });
        }

        public static void Warn(string text)
        {
            Invoke(delegate { Dlg.Warn(text); });
        }

        public static void Error(string text)
        {
            Invoke(delegate { Dlg.Error(text); });
        }
    }
}
