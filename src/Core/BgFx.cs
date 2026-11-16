using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RockToolbox.Native
{
    // Background picture with opacity and blur. The blur is computed ONCE on a small copy (a live blur effect re-renders every frame).
    internal static class BgFx
    {
        private static BitmapSource _orig;
        private static readonly Dictionary<int, BitmapSource> Cache = new Dictionary<int, BitmapSource>();

        public static bool HasImage { get { string p = Ui.Settings.GetString("BgImage"); return p.Length > 0 && File.Exists(p); } }
        public static int Opacity { get { int v = 40; try { string s = Ui.Settings.GetString("BgOpacity"); if (s.Length > 0) v = Convert.ToInt32(s); } catch { } return Math.Max(0, Math.Min(100, v)); } }
        public static int Blur { get { int v = 0; try { v = Convert.ToInt32(Ui.Settings.GetString("BgBlur")); } catch { } return Math.Max(0, Math.Min(60, v)); } }

        public static void Apply()
        {
            Image img = Ui.Find<Image>("imgBg");
            bool ok = false;
            if (HasImage)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(Ui.Settings.GetString("BgImage"));
                    MemoryStream ms = new MemoryStream(bytes);
                    BitmapFrame bmp = BitmapFrame.Create(ms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                    if (bmp.CanFreeze) bmp.Freeze();
                    ms.Dispose();
                    _orig = bmp; Cache.Clear();
                    img.Opacity = Opacity / 100.0; img.Visibility = Visibility.Visible;
                    ApplyBlur();
                    ok = true;
                }
                catch (Exception ex) { Ui.Log.Error("Could not load the background image: " + ex.Message, "Settings"); }
            }
            if (!ok) { _orig = null; img.Source = null; img.Visibility = Visibility.Collapsed; }
        }

        public static void SetOpacity() { Image img = Ui.Find<Image>("imgBg"); img.Opacity = Opacity / 100.0; }

        public static void ApplyBlur()
        {
            if (_orig == null) return;
            Image img = Ui.Find<Image>("imgBg");
            img.Effect = null;
            BitmapSource src = BlurSource(Blur);
            if (src != null) img.Source = src;
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        }

        private static BitmapSource BlurSource(double radius)
        {
            BitmapSource src = _orig;
            if (src == null) return null;
            if (radius <= 0) return src;
            int key = (int)radius; BitmapSource hit;
            if (Cache.TryGetValue(key, out hit)) return hit;
            try
            {
                int ww = Math.Min(320, src.PixelWidth);
                double sc = (double)ww / src.PixelWidth;
                int hh = Math.Max(8, (int)Math.Round(src.PixelHeight * sc));
                DrawingVisual dv = new DrawingVisual();
                RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.Fant);
                using (DrawingContext dc = dv.RenderOpen()) dc.DrawImage(src, new Rect(0, 0, ww, hh));
                RenderTargetBitmap rtb = new RenderTargetBitmap(ww, hh, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                int stride = ww * 4; byte[] px = new byte[stride * hh];
                rtb.CopyPixels(px, stride, 0);
                int rad = Math.Max(1, (int)Math.Round(radius * ww / 1500.0 * 1.3));
                Box(px, ww, hh, rad, 3);
                BitmapSource outp = BitmapSource.Create(ww, hh, 96, 96, PixelFormats.Pbgra32, null, px, stride);
                outp.Freeze();
                Cache[key] = outp;
                return outp;
            }
            catch { return src; }
        }

        private static void Box(byte[] px, int w, int h, int r, int passes)
        {
            if (r < 1) return;
            byte[] tmp = new byte[px.Length];
            for (int p = 0; p < passes; p++) { Pass(px, tmp, w, h, r, true); Pass(tmp, px, w, h, r, false); }
        }

        private static void Pass(byte[] src, byte[] dst, int w, int h, int r, bool horiz)
        {
            int len = horiz ? w : h, lines = horiz ? h : w;
            int step = horiz ? 4 : w * 4, lineStep = horiz ? w * 4 : 4;
            int win = 2 * r + 1;
            for (int l = 0; l < lines; l++)
            {
                int baseIdx = l * lineStep;
                for (int c = 0; c < 4; c++)
                {
                    int sum = 0;
                    for (int k = -r; k <= r; k++) { int i = k < 0 ? 0 : (k >= len ? len - 1 : k); sum += src[baseIdx + i * step + c]; }
                    for (int x = 0; x < len; x++)
                    {
                        dst[baseIdx + x * step + c] = (byte)(sum / win);
                        int addI = x + r + 1; if (addI >= len) addI = len - 1;
                        int subI = x - r; if (subI < 0) subI = 0;
                        sum += src[baseIdx + addI * step + c] - src[baseIdx + subI * step + c];
                    }
                }
            }
        }
    }
}
