using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RDownloader.Native
{
    // Small animation helpers used by the welcome screen, the profile window and the About window.
    internal static class Fx
    {
        public static Duration Dur(int ms) { return new Duration(TimeSpan.FromMilliseconds(ms)); }

        private static CubicEase Out() { CubicEase e = new CubicEase(); e.EasingMode = EasingMode.EaseOut; return e; }

        // rises a little while fading in
        public static void FadeUp(UIElement el, int delayMs, double fromY)
        {
            TranslateTransform tt = new TranslateTransform(0, fromY);
            el.RenderTransform = tt;
            TimeSpan bt = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation ay = new DoubleAnimation(fromY, 0.0, Dur(520)); ay.EasingFunction = Out(); ay.BeginTime = bt;
            DoubleAnimation af = new DoubleAnimation(0.0, 1.0, Dur(520)); af.BeginTime = bt;
            el.Opacity = 0;
            tt.BeginAnimation(TranslateTransform.YProperty, ay);
            el.BeginAnimation(UIElement.OpacityProperty, af);
        }

        // springs in from small
        public static void Pop(UIElement el, int delayMs)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform sc = new ScaleTransform(0.4, 0.4);
            el.RenderTransform = sc;
            BackEase ease = new BackEase(); ease.EasingMode = EasingMode.EaseOut; ease.Amplitude = 0.7;
            TimeSpan bt = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation a = new DoubleAnimation(0.4, 1.0, Dur(650)); a.EasingFunction = ease; a.BeginTime = bt;
            DoubleAnimation f = new DoubleAnimation(0.0, 1.0, Dur(320)); f.BeginTime = bt;
            el.Opacity = 0;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            el.BeginAnimation(UIElement.OpacityProperty, f);
        }

        // an expanding, fading ring (times <= 0 = forever)
        public static void Ring(UIElement el, int times, double from, double to, double op, int delayMs)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform sc = new ScaleTransform(from, from);
            el.RenderTransform = sc;
            RepeatBehavior rep = times <= 0 ? RepeatBehavior.Forever : new RepeatBehavior((double)times);
            TimeSpan bt = TimeSpan.FromMilliseconds(delayMs);
            DoubleAnimation a = new DoubleAnimation(from, to, Dur(1300)); a.RepeatBehavior = rep; a.BeginTime = bt;
            DoubleAnimation o = new DoubleAnimation(op, 0.0, Dur(1300)); o.RepeatBehavior = rep; o.BeginTime = bt;
            el.Opacity = 0;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            el.BeginAnimation(UIElement.OpacityProperty, o);
        }

        public static void Bounce(UIElement el)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform sc = new ScaleTransform(0.78, 0.78);
            el.RenderTransform = sc;
            BackEase ease = new BackEase(); ease.EasingMode = EasingMode.EaseOut; ease.Amplitude = 1.1;
            DoubleAnimation a = new DoubleAnimation(0.78, 1.0, Dur(480)); a.EasingFunction = ease;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        public static void Spin(UIElement el)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            RotateTransform rt = new RotateTransform(0);
            el.RenderTransform = rt;
            DoubleAnimation a = new DoubleAnimation(0.0, 360.0, Dur(1100)); a.RepeatBehavior = RepeatBehavior.Forever;
            rt.BeginAnimation(RotateTransform.AngleProperty, a);
        }

        // a slow back-and-forth drift (background glows)
        public static void Drift(FrameworkElement el, double dx, double dy, int ms)
        {
            TranslateTransform tt = new TranslateTransform(0, 0);
            el.RenderTransform = tt;
            SineEase ease = new SineEase(); ease.EasingMode = EasingMode.EaseInOut;
            DoubleAnimation ax = new DoubleAnimation(0.0, dx, Dur(ms)); ax.AutoReverse = true; ax.RepeatBehavior = RepeatBehavior.Forever; ax.EasingFunction = ease;
            DoubleAnimation ay = new DoubleAnimation(0.0, dy, Dur((int)(ms * 1.3))); ay.AutoReverse = true; ay.RepeatBehavior = RepeatBehavior.Forever; ay.EasingFunction = ease;
            tt.BeginAnimation(TranslateTransform.XProperty, ax);
            tt.BeginAnimation(TranslateTransform.YProperty, ay);
        }

        // photo -> brush for the round avatar
        public static BitmapSource LoadBitmap(string path)
        {
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache; bi.DecodePixelWidth = 256;
                bi.UriSource = new Uri(path); bi.EndInit(); bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public static void PaintAvatar(System.Windows.Shapes.Shape el, UIElement letter, BitmapSource bmp)
        {
            if (bmp != null)
            {
                ImageBrush ib = new ImageBrush(bmp); ib.Stretch = Stretch.UniformToFill;
                el.Fill = ib; letter.Visibility = Visibility.Collapsed;
            }
            else
            {
                el.Fill = Ui.Brush("#B8C0CC"); letter.Visibility = Visibility.Visible;
            }
        }

        public static string SaveAvatar(BitmapSource bmp, string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            string png = Path.Combine(dataDir, "avatar.png");
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (FileStream fs = File.Create(png)) enc.Save(fs);
            return png;
        }

        public static BitmapSource PickPhoto(Window owner)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"; dlg.Title = "Choose your photo";
                if (dlg.ShowDialog(owner) == true) return LoadBitmap(dlg.FileName);
            }
            catch { }
            return null;
        }
    }

    // First-run welcome screen (own window, shown before the main window): greeting, language, look, profile.
    // Also holds the profile window and the About window (same design).
}
