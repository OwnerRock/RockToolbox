using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RDownloader.Native
{
    // small helpers to build the Options / Torrent windows in code with the same calm look as the other download windows
    internal static class DlgKit
    {
        public static Window Make(string title, double w, double h, out Border host)
        {
            Window win = DlTheme.Load("ui/Dialog.xaml");
            win.Width = w; win.Height = h; if (Ui.Win != null && Ui.Win.IsLoaded) win.Owner = Ui.Win;
            ((TextBlock)win.FindName("txtTitle")).Text = Lang.L(title);
            ((Button)win.FindName("btnClose")).Click += delegate { win.Close(); };
            host = (Border)win.FindName("host");
            win.Opacity = 0;
            win.Loaded += delegate { win.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); };
            return win;
        }

        public static Brush Dim { get { return Ui.Brush("#8895A6"); } }

        public static TextBlock Label(string t, bool dim)
        {
            TextBlock b = new TextBlock(); b.Text = Lang.L(t); b.TextWrapping = TextWrapping.Wrap; b.VerticalAlignment = VerticalAlignment.Center;
            if (dim) { b.Foreground = Dim; b.FontSize = 11.5; }
            return b;
        }

        public static TextBlock Head(string t)
        {
            TextBlock b = new TextBlock(); b.Text = Lang.L(t); b.FontSize = 15; b.FontWeight = FontWeights.SemiBold; b.Margin = new Thickness(0, 0, 0, 12);
            return b;
        }

        public static TextBox Box(Window w, string text)
        {
            TextBox t = new TextBox(); t.Text = text ?? ""; t.Height = 28; t.VerticalContentAlignment = VerticalAlignment.Center;
            t.Style = (Style)w.FindResource("CalmBox"); return t;
        }

        public static Button Btn(Window w, string text, bool primary)
        {
            Button b = new Button(); b.Content = Lang.L(text); b.Style = (Style)w.FindResource(primary ? "CalmBtn" : "CalmGhost");
            b.Padding = new Thickness(16, 6, 16, 6); b.MinWidth = 90; b.Margin = new Thickness(4, 0, 0, 0); return b;
        }

        public static CheckBox Check(Window w, string text, bool on)
        {
            CheckBox c = new CheckBox(); c.Content = Lang.L(text); c.IsChecked = on; c.Margin = new Thickness(0, 4, 0, 4);
            try { c.Style = (Style)w.FindResource("CalmCheck"); } catch (Exception) { }
            return c;
        }

        // label on the left, control on the right
        public static Grid Row(string label, FrameworkElement ctl, double labelW)
        {
            Grid g = new Grid(); g.Margin = new Thickness(0, 4, 0, 4);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelW) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(Label(label, false)); Grid.SetColumn(ctl, 1); g.Children.Add(ctl);
            return g;
        }

        public static ComboBox Combo(IList<string> items, int sel)
        {
            ComboBox c = new ComboBox(); c.Height = 28; c.VerticalContentAlignment = VerticalAlignment.Center;
            foreach (string s in items) c.Items.Add(Lang.L(s));
            c.SelectedIndex = sel < 0 || sel >= items.Count ? 0 : sel; return c;
        }

        public static string PickFolder(string start)
        {
            try
            {
                using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
                { fd.SelectedPath = start ?? ""; if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK) return fd.SelectedPath; }
            }
            catch (Exception) { }
            return null;
        }

        // text box + "..." button
        public static Grid Browse(Window w, TextBox box, Func<string> pick)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(box);
            Button b = Btn(w, "...", false); b.MinWidth = 36; b.Padding = new Thickness(8, 4, 8, 4); Grid.SetColumn(b, 1);
            b.Click += delegate { string p = pick(); if (!string.IsNullOrEmpty(p)) box.Text = p; };
            g.Children.Add(b); return g;
        }
    }
}
