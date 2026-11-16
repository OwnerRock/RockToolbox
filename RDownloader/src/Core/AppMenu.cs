using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace RDownloader.Native
{
    // one line of a drop-down menu
    internal sealed class MItem
    {
        public string Text = "", Key = "";
        public Action Do;
        public bool Check, Radio, Sep, Disabled;
        public List<MItem> Sub;

        public static MItem Line() { MItem m = new MItem(); m.Sep = true; return m; }
        public static MItem Of(string text, Action a) { MItem m = new MItem(); m.Text = text; m.Do = a; return m; }
        public static MItem Of(string text, string key, Action a) { MItem m = Of(text, a); m.Key = key; return m; }
        public static MItem Off(string text, string key) { MItem m = Of(text, key, null); m.Disabled = true; return m; }
        public static MItem Toggle(string text, bool on, Action a) { MItem m = Of(text, a); m.Check = on; return m; }
        public static MItem Choice(string text, bool on, Action a) { MItem m = Of(text, a); m.Check = on; m.Radio = true; return m; }
        public static MItem Menu(string text, List<MItem> sub) { MItem m = new MItem(); m.Text = text; m.Sub = sub; return m; }
    }

    // The menu bar's drop-down menus: a rounded dark (or light) card that fades in, with sub-menus that open at the side.
    internal static class AppMenu
    {
        private static Popup _open;
        private static FrameworkElement _lastAnchor;
        private static DateTime _lastClosed = DateTime.MinValue;

        private static Border Box()
        {
            Border b = new Border();
            b.Background = Ui.Brush("#23272D"); b.BorderBrush = Ui.Brush("#35506A"); b.BorderThickness = new Thickness(1);
            b.CornerRadius = new CornerRadius(10); b.Padding = new Thickness(5); b.MinWidth = 230;
            DropShadowEffect fx = new DropShadowEffect(); fx.BlurRadius = 18; fx.ShadowDepth = 3; fx.Opacity = 0.45; fx.Color = Colors.Black; b.Effect = fx;
            return b;
        }

        // menu-bar buttons: while a menu is open, moving the mouse over another button opens that button's menu (no click needed).
        // A popup captures the mouse, so the position is polled instead of waiting for MouseEnter.
        private static readonly List<KeyValuePair<FrameworkElement, Func<List<MItem>>>> Bar = new List<KeyValuePair<FrameworkElement, Func<List<MItem>>>>();
        private static System.Windows.Threading.DispatcherTimer _poll;

        public static void Register(FrameworkElement b, Func<List<MItem>> build)
        {
            Bar.Add(new KeyValuePair<FrameworkElement, Func<List<MItem>>>(b, build));
            if (_poll != null) return;
            _poll = new System.Windows.Threading.DispatcherTimer(); _poll.Interval = TimeSpan.FromMilliseconds(70);
            _poll.Tick += delegate
            {
                try
                {
                    if (!IsOpen) return;
                    foreach (KeyValuePair<FrameworkElement, Func<List<MItem>>> kv in Bar)
                    {
                        FrameworkElement el = kv.Key;
                        if (!el.IsVisible || IsFor(el)) continue;
                        Point p = Mouse.GetPosition(el);
                        if (p.X >= 0 && p.Y >= 0 && p.X <= el.ActualWidth && p.Y <= el.ActualHeight) { Show(el, kv.Value()); break; }
                    }
                }
                catch (Exception) { }
            };
            _poll.Start();
        }

        public static bool IsOpen { get { return _open != null && _open.IsOpen; } }
        public static bool IsFor(FrameworkElement a) { return _open != null && _open.IsOpen && object.ReferenceEquals(_open.PlacementTarget, a); }

        public static void Show(FrameworkElement anchor, List<MItem> items)
        {
            // pressing the same menu button again closes it (the click that closes a popup must not open it again at once)
            if (object.ReferenceEquals(_lastAnchor, anchor) && (DateTime.UtcNow - _lastClosed).TotalMilliseconds < 250) return;
            if (_open != null) { try { _open.IsOpen = false; } catch (Exception) { } }
            Popup p = new Popup();
            p.PlacementTarget = anchor; p.Placement = PlacementMode.Bottom; p.StaysOpen = false; p.AllowsTransparency = true; p.PopupAnimation = PopupAnimation.None;
            Grid host = new Grid(); host.Margin = new Thickness(8, 1, 14, 16);
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border main = Box(); StackPanel sp = new StackPanel(); main.Child = sp; host.Children.Add(main);
            Border sub = Box(); sub.Visibility = Visibility.Hidden; sub.VerticalAlignment = VerticalAlignment.Top; sub.Margin = new Thickness(1, 0, 0, 0);
            Grid.SetColumn(sub, 1);
            bool anySub = false; foreach (MItem q in items) if (q.Sub != null) anySub = true;
            if (anySub) host.Children.Add(sub);
            Action close = delegate { p.IsOpen = false; };
            Action<MItem, Border> openSub = delegate(MItem it, Border row)
            {
                StackPanel ss = new StackPanel();
                foreach (MItem s in it.Sub) ss.Children.Add(Row(s, close, null));
                sub.Child = ss;
                double y = 0; try { y = row.TranslatePoint(new Point(0, 0), main).Y - main.Padding.Top; } catch (Exception) { }
                sub.Margin = new Thickness(1, Math.Max(0, y), 0, 0);
                if (sub.Visibility != Visibility.Visible)
                {
                    sub.Visibility = Visibility.Visible;
                    sub.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(130))));
                }
            };
            Action hideSub = delegate { sub.Visibility = Visibility.Hidden; };
            foreach (MItem it in items) sp.Children.Add(Row(it, close, new Action<MItem, Border>(delegate(MItem m, Border r) { if (m.Sub != null) openSub(m, r); else hideSub(); })));
            p.Child = host;
            p.Closed += delegate { _lastAnchor = anchor; _lastClosed = DateTime.UtcNow; if (object.ReferenceEquals(_open, p)) _open = null; };
            _open = p; p.IsOpen = true;
            TranslateTransform tt = new TranslateTransform(0, -6); main.RenderTransform = tt;
            main.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(140))));
            DoubleAnimation sl = new DoubleAnimation(-6, 0, new Duration(TimeSpan.FromMilliseconds(180))); CubicEase ce = new CubicEase(); ce.EasingMode = EasingMode.EaseOut; sl.EasingFunction = ce;
            tt.BeginAnimation(TranslateTransform.YProperty, sl);
        }

        private static FrameworkElement Row(MItem it, Action close, Action<MItem, Border> hover)
        {
            if (it.Sep)
            {
                Border sep = new Border(); sep.Height = 1; sep.Margin = new Thickness(10, 4, 10, 4); sep.Background = Ui.Brush("#35506A"); sep.Opacity = 0.6; return sep;
            }
            Border row = new Border(); row.CornerRadius = new CornerRadius(7); row.Padding = new Thickness(6, 6, 8, 6); row.Background = Brushes.Transparent;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            TextBlock mark = new TextBlock(); mark.HorizontalAlignment = HorizontalAlignment.Center; mark.VerticalAlignment = VerticalAlignment.Center; mark.Foreground = Ui.Brush("#4FD1FF");
            if (it.Check)
            {
                if (it.Radio) { mark.Text = "●"; mark.FontSize = 9; }
                else { mark.Text = ""; mark.FontFamily = new FontFamily("Segoe MDL2 Assets"); mark.FontSize = 12; }
            }
            TextBlock t = new TextBlock(); t.Text = Lang.L(it.Text); t.VerticalAlignment = VerticalAlignment.Center; t.Foreground = Ui.Brush(it.Disabled ? "#6B7186" : "#E6E8F2"); t.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(t, 1);
            TextBlock k = new TextBlock(); k.Text = it.Key; k.Margin = new Thickness(24, 0, 0, 0); k.VerticalAlignment = VerticalAlignment.Center; k.FontSize = 11.5; k.Foreground = Ui.Brush("#8D93AD"); Grid.SetColumn(k, 2);
            TextBlock ch = new TextBlock(); ch.Text = it.Sub != null ? "" : ""; ch.FontFamily = new FontFamily("Segoe MDL2 Assets"); ch.FontSize = 10; ch.HorizontalAlignment = HorizontalAlignment.Right; ch.VerticalAlignment = VerticalAlignment.Center; ch.Foreground = Ui.Brush("#8D93AD"); Grid.SetColumn(ch, 3);
            g.Children.Add(mark); g.Children.Add(t); g.Children.Add(k); g.Children.Add(ch);
            row.Child = g;
            if (!it.Disabled)
            {
                row.Cursor = Cursors.Hand;
                row.MouseEnter += delegate
                {
                    row.Background = Ui.Brush("#2B3038");
                    if (hover != null) hover(it, row);
                };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                if (it.Sub == null)
                    row.MouseLeftButtonUp += delegate { close(); if (it.Do != null) { try { it.Do(); } catch (Exception ex) { Ui.Log.Warn(ex.Message, "Menu"); } } };
            }
            else if (hover != null) row.MouseEnter += delegate { hover(it, row); };
            return row;
        }
    }
}
