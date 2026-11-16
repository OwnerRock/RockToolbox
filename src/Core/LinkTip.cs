using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace RockToolbox.Native
{
    // A link-like text (for example "GitHub Releases"): hover it and a small card shows the real address
    // (with a link icon and a "click to open" hint); the text is underlined while the mouse is over it.
    internal static class LinkTip
    {
        private sealed class State { public TextBlock Url; public TextBlock Title; }

        public static void Set(TextBlock tb, string url, string title)
        {
            State st = tb.Tag as State;
            if (st != null) { st.Url.Text = url; st.Title.Text = Lang.L(title); return; }
            st = new State();
            StackPanel sp = new StackPanel();
            StackPanel head = new StackPanel(); head.Orientation = Orientation.Horizontal;
            TextBlock ico = new TextBlock(); ico.Text = ""; ico.FontFamily = new FontFamily("Segoe MDL2 Assets");
            ico.Foreground = Ui.Brush("#4FD1FF"); ico.Margin = new Thickness(0, 1, 7, 0); ico.VerticalAlignment = VerticalAlignment.Center;
            st.Title = new TextBlock(); st.Title.Text = Lang.L(title); st.Title.FontWeight = FontWeights.SemiBold;
            head.Children.Add(ico); head.Children.Add(st.Title);
            st.Url = new TextBlock(); st.Url.Text = url; st.Url.Foreground = Ui.Brush("#4FD1FF");
            st.Url.FontFamily = new FontFamily("Consolas"); st.Url.FontSize = 11.5; st.Url.TextWrapping = TextWrapping.Wrap; st.Url.MaxWidth = 380; st.Url.Margin = new Thickness(0, 5, 0, 0);
            TextBlock hint = new TextBlock(); hint.Text = Lang.L("Click to open in your browser"); hint.Foreground = Ui.Brush("#9AA0B8");
            hint.FontSize = 11; hint.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(head); sp.Children.Add(st.Url); sp.Children.Add(hint);
            ToolTip tip = new ToolTip(); tip.Content = sp; tip.Padding = new Thickness(12, 9, 12, 9); tip.MaxWidth = 440;
            tb.ToolTip = tip; tb.Tag = st; tb.Cursor = System.Windows.Input.Cursors.Hand;
            ToolTipService.SetInitialShowDelay(tb, 80);
            ToolTipService.SetShowDuration(tb, 30000);
            ToolTipService.SetPlacement(tb, PlacementMode.Bottom);
            tb.MouseEnter += delegate { tb.TextDecorations = TextDecorations.Underline; };
            tb.MouseLeave += delegate { tb.TextDecorations = null; };
        }
    }
}
