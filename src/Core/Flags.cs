using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RockToolbox.Native
{
    // Country flags drawn with WPF shapes (60x40 canvas, scaled): used by the welcome language page and the gear-menu language list.
    internal static class Flags
    {
        private static Brush Br(string hex)
        {
            SolidColorBrush b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private static void DRect(Canvas cv, double x, double y, double w, double h, string hex)
        {
            Rectangle r = new Rectangle(); r.Width = w; r.Height = h; r.Fill = Br(hex);
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y); cv.Children.Add(r);
        }

        private static void DCircle(Canvas cv, double cx, double cy, double rad, string hex, bool ring)
        {
            Ellipse e = new Ellipse(); e.Width = 2 * rad; e.Height = 2 * rad;
            if (ring) { e.Stroke = Br(hex); e.StrokeThickness = 0.9; } else e.Fill = Br(hex);
            Canvas.SetLeft(e, cx - rad); Canvas.SetTop(e, cy - rad); cv.Children.Add(e);
        }

        private static void DPoly(Canvas cv, string pts, string hex)
        {
            Polygon p = new Polygon(); p.Points = PointCollection.Parse(pts); p.Fill = Br(hex); cv.Children.Add(p);
        }

        private static void DLine(Canvas cv, double x1, double y1, double x2, double y2, double th, string hex)
        {
            Line l = new Line(); l.X1 = x1; l.Y1 = y1; l.X2 = x2; l.Y2 = y2; l.Stroke = Br(hex); l.StrokeThickness = th; cv.Children.Add(l);
        }

        private static void DStar(Canvas cv, double cx, double cy, double rad, string hex)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string[] pts = new string[10];
            for (int i = 0; i < 10; i++)
            {
                double r = (i % 2 == 1) ? rad * 0.382 : rad;
                double a = (-90 + i * 36) * Math.PI / 180.0;
                pts[i] = (cx + r * Math.Cos(a)).ToString("0.##", inv) + "," + (cy + r * Math.Sin(a)).ToString("0.##", inv);
            }
            DPoly(cv, string.Join(" ", pts), hex);
        }

        private static void Stripes3(Canvas cv, string a, string b, string c)
        {
            DRect(cv, 0, 0, 60, 13.4, a); DRect(cv, 0, 13.3, 60, 13.4, b); DRect(cv, 0, 26.6, 60, 13.4, c);
        }

        private static void Vert3(Canvas cv, string a, string b, string c)
        {
            DRect(cv, 0, 0, 20.2, 40, a); DRect(cv, 20, 0, 20.2, 40, b); DRect(cv, 40, 0, 20, 40, c);
        }

        public static FrameworkElement Make(string code, double w, double h, double ring)
        {
            Canvas cv = new Canvas(); cv.Width = 60; cv.Height = 40;
            switch (code)
            {
                case "en":
                    DRect(cv, 0, 0, 60, 40, "#012169");
                    DLine(cv, 0, 0, 60, 40, 7, "#FFFFFF"); DLine(cv, 60, 0, 0, 40, 7, "#FFFFFF");
                    DLine(cv, 0, 0, 60, 40, 2.6, "#C8102E"); DLine(cv, 60, 0, 0, 40, 2.6, "#C8102E");
                    DRect(cv, 0, 14, 60, 12, "#FFFFFF"); DRect(cv, 24, 0, 12, 40, "#FFFFFF");
                    DRect(cv, 0, 16.4, 60, 7.2, "#C8102E"); DRect(cv, 26.4, 0, 7.2, 40, "#C8102E");
                    break;
                case "ar":
                    Stripes3(cv, "#CE1126", "#FFFFFF", "#000000"); DCircle(cv, 30, 20, 3.2, "#C09300", false);
                    break;
                case "fr": Vert3(cv, "#0055A4", "#FFFFFF", "#EF4135"); break;
                case "es": DRect(cv, 0, 0, 60, 10.2, "#AA151B"); DRect(cv, 0, 10, 60, 20.2, "#F1BF00"); DRect(cv, 0, 30, 60, 10, "#AA151B"); break;
                case "de": Stripes3(cv, "#000000", "#DD0000", "#FFCE00"); break;
                case "tr":
                    DRect(cv, 0, 0, 60, 40, "#E30A17"); DCircle(cv, 22, 20, 10, "#FFFFFF", false); DCircle(cv, 25, 20, 8, "#E30A17", false); DStar(cv, 33.5, 20, 5, "#FFFFFF");
                    break;
                case "ru": Stripes3(cv, "#FFFFFF", "#0039A6", "#D52B1E"); break;
                case "zh":
                    DRect(cv, 0, 0, 60, 40, "#DE2910"); DStar(cv, 10, 10, 6.2, "#FFDE00");
                    DStar(cv, 20, 4, 2.1, "#FFDE00"); DStar(cv, 24, 8.5, 2.1, "#FFDE00"); DStar(cv, 24, 14.5, 2.1, "#FFDE00"); DStar(cv, 20, 19, 2.1, "#FFDE00");
                    break;
                case "ja": DRect(cv, 0, 0, 60, 40, "#FFFFFF"); DCircle(cv, 30, 20, 12, "#BC002D", false); break;
                case "pt": DRect(cv, 0, 0, 60, 40, "#009C3B"); DPoly(cv, "30,3 56,20 30,37 4,20", "#FFDF00"); DCircle(cv, 30, 20, 9.5, "#002776", false); break;
                case "it": Vert3(cv, "#009246", "#FFFFFF", "#CE2B37"); break;
                case "hi":
                    Stripes3(cv, "#FF9933", "#FFFFFF", "#138808"); DCircle(cv, 30, 20, 5.4, "#000080", true); DCircle(cv, 30, 20, 1.1, "#000080", false);
                    break;
                default: DRect(cv, 0, 0, 60, 40, "#9AA0B8"); break;
            }
            Viewbox vb = new Viewbox(); vb.Stretch = Stretch.Fill; vb.Child = cv;
            double iw = Math.Max(4, w - 2 * ring), ih = Math.Max(4, h - 2 * ring);
            double rr = Math.Max(3, Math.Min(iw, ih) * 0.22);
            Grid inner = new Grid(); inner.Width = iw; inner.Height = ih;
            inner.Clip = new RectangleGeometry(new Rect(0, 0, iw, ih), rr, rr);
            inner.Children.Add(vb);
            Border b = new Border();
            b.Width = w; b.Height = h; b.CornerRadius = new CornerRadius(rr + ring);
            b.BorderThickness = new Thickness(ring); b.BorderBrush = Brushes.Transparent;
            b.Child = inner;
            return b;
        }
    }
}
