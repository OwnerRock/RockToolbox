using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RockToolbox.Native
{
    // page transitions (Slide up / Fade / Zoom / Off) - same behaviour as the PowerShell version
    internal static class Anim
    {
        private static CubicEase Ease() { CubicEase e = new CubicEase(); e.EasingMode = EasingMode.EaseOut; return e; }

        public static void PageAnim(UIElement el, double fromY)
        {
            try
            {
                TranslateTransform tt = el.RenderTransform as TranslateTransform;
                if (tt == null || tt.IsFrozen) { tt = new TranslateTransform(); el.RenderTransform = tt; }
                Duration d = new Duration(TimeSpan.FromMilliseconds(280));
                DoubleAnimation fade = new DoubleAnimation(0.0, 1.0, d); fade.EasingFunction = Ease();
                el.BeginAnimation(UIElement.OpacityProperty, fade);
                DoubleAnimation slide = new DoubleAnimation(fromY, 0.0, d); slide.EasingFunction = Ease();
                tt.BeginAnimation(TranslateTransform.YProperty, slide);
            }
            catch { }
        }

        public static void PageTransition(UIElement el)
        {
            string mode = Ui.Settings.GetString("PageAnim");
            if (mode.Length == 0) mode = "Slide up";
            if (mode == "Off") return;
            if (mode == "Fade") { PageAnim(el, 0); return; }
            if (mode == "Zoom")
            {
                try
                {
                    ScaleTransform sc = new ScaleTransform(0.95, 0.95);
                    el.RenderTransformOrigin = new Point(0.5, 0.5); el.RenderTransform = sc;
                    Duration d = new Duration(TimeSpan.FromMilliseconds(300));
                    DoubleAnimation fade = new DoubleAnimation(0.0, 1.0, d); fade.EasingFunction = Ease();
                    el.BeginAnimation(UIElement.OpacityProperty, fade);
                    DoubleAnimation z = new DoubleAnimation(0.95, 1.0, d); z.EasingFunction = Ease();
                    sc.BeginAnimation(ScaleTransform.ScaleXProperty, z); sc.BeginAnimation(ScaleTransform.ScaleYProperty, z);
                }
                catch { }
                return;
            }
            PageAnim(el, 16);
        }

        // ---- pop-up menus: grow out of their corner while fading in, fade out when closed ----
        public static void PopIn(FrameworkElement el, double originX, double originY)
        {
            try
            {
                el.Visibility = Visibility.Visible;
                el.RenderTransformOrigin = new Point(originX, originY);
                ScaleTransform sc = new ScaleTransform(0.9, 0.9);
                TranslateTransform tt = new TranslateTransform(0, -8);
                TransformGroup g = new TransformGroup(); g.Children.Add(sc); g.Children.Add(tt);
                el.RenderTransform = g;
                Duration d = new Duration(TimeSpan.FromMilliseconds(240));
                BackEase back = new BackEase(); back.EasingMode = EasingMode.EaseOut; back.Amplitude = 0.35;
                DoubleAnimation z = new DoubleAnimation(0.9, 1.0, d); z.EasingFunction = back;
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, z); sc.BeginAnimation(ScaleTransform.ScaleYProperty, z);
                DoubleAnimation y = new DoubleAnimation(-8.0, 0.0, d); y.EasingFunction = Ease();
                tt.BeginAnimation(TranslateTransform.YProperty, y);
                DoubleAnimation f = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(180)));
                el.BeginAnimation(UIElement.OpacityProperty, f);
            }
            catch { el.Visibility = Visibility.Visible; }
        }

        public static void PopOut(FrameworkElement el)
        {
            if (el.Visibility != Visibility.Visible) return;
            try
            {
                DoubleAnimation f = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(130)));
                f.Completed += delegate
                {
                    if (el.Opacity > 0.05) return;
                    el.BeginAnimation(UIElement.OpacityProperty, null);
                    el.Opacity = 1.0;
                    el.Visibility = Visibility.Collapsed;
                };
                el.BeginAnimation(UIElement.OpacityProperty, f);
            }
            catch { el.Visibility = Visibility.Collapsed; }
        }

        // the first thing the user sees: the whole window fades and rises a little
        public static void WindowIntro(UIElement root)
        {
            PageAnim(root, 22);
        }

        public static void ApplyScrollSettings()
        {
            string a = Ui.Settings.GetString("ScrollAnim"), s = Ui.Settings.GetString("ScrollSpeed");
            switch (a) { case "Off": RockScroll.Mode = 0; break; case "Glide": RockScroll.Mode = 2; break; case "Snappy": RockScroll.Mode = 3; break; case "Bouncy": RockScroll.Mode = 4; break; default: RockScroll.Mode = 1; break; }
            switch (s) { case "Slow": RockScroll.Speed = 0.6; break; case "Fast": RockScroll.Speed = 1.7; break; default: RockScroll.Speed = 1.0; break; }
        }
    }
}
