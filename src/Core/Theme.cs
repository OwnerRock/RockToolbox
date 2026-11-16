using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace RockToolbox.Native
{
    // Theme engine (port of Get-Palette / Convert-XamlTheme): the XAML and every brush made in code go through the palette,
    // so light / dark mode, accent colour and background style apply everywhere. Changing it needs a restart of the window.
    internal static class Theme
    {
        public static readonly string[] AccentNames = { "Ocean", "Blue", "Violet", "Purple", "Pink", "Rose", "Red", "Orange", "Amber", "Lime", "Green", "Teal" };
        public static readonly string[] AccentHex = { "#4FD1FF", "#4F8CFF", "#8B7BFF", "#B36BFF", "#FF6BC1", "#FF5C7A", "#FF5555", "#FF9F43", "#FFC84A", "#A3E635", "#3DDC84", "#2DD4BF" };
        public static readonly string[] BgNames = { "Default", "Midnight", "Graphite", "Forest", "Wine", "Deep dark" };
        private static readonly double[][] BgVar = {
            new double[] { -1, 1.0, 1.0 }, new double[] { 228, 2.4, 0.95 }, new double[] { 0, 0.0, 1.05 },
            new double[] { 150, 1.6, 0.95 }, new double[] { 335, 1.8, 0.98 }, new double[] { -1, 0.8, 0.45 } };
        private static readonly string[] Surf = { "1F2329", "1B1F24", "181B20", "23272D", "14171B", "2B3038", "35506A", "1E3547", "3A3F4B", "262B33", "0F1116", "343A44", "12131A" };
        private static readonly string[] Text = { "E6E8F2", "C9CDE0", "D5D9E6", "9AA0B8", "6B7186", "F2F4FF", "8D93AD", "F0F4FF" };
        private static readonly string[] Status = { "3DDC84", "FFB454", "FF6B6B", "FF4D4D", "FF5555", "5CE08A" };
        private static readonly Dictionary<string, string> LightMap = new Dictionary<string, string>
        {
            { "1F2329", "#F3F5F9" }, { "1B1F24", "#FFFFFF" }, { "181B20", "#E8ECF2" }, { "23272D", "#FFFFFF" }, { "14171B", "#FFFFFF" },
            { "2B3038", "#D5DBE5" }, { "35506A", "#B7C3D3" }, { "1E3547", "#E1E7F0" }, { "3A3F4B", "#B9C1CD" }, { "262B33", "#DDE3EC" },
            { "0F1116", "#DCE2EB" }, { "343A44", "#D5DBE5" }, { "12131A", "#E6EAF0" },
            { "E6E8F2", "#1F2733" }, { "C9CDE0", "#3B4556" }, { "D5D9E6", "#2C3546" }, { "9AA0B8", "#5A6578" }, { "6B7186", "#6A7488" },
            { "F2F4FF", "#111A27" }, { "8D93AD", "#66718A" }, { "F0F4FF", "#1B2733" },
            { "3DDC84", "#1E8E4E" }, { "FFB454", "#B36B00" }, { "FF6B6B", "#D32F2F" }, { "FF4D4D", "#D32F2F" }, { "FF5555", "#D32F2F" }, { "5CE08A", "#2E9E5B" }
        };

        public static Dictionary<string, string> Pal = new Dictionary<string, string>();
        public static bool Light;

        // "Modern" look: the brand gradient (orange > pink > purple) on primary buttons / selections / switches, rounder shapes, a side menu.
        // "Classic" is the original blue look. Chosen in Settings (or on the welcome screen) and stored as UiStyle.
        public static bool Modern;
        public static readonly string[] GradKeys = { "2F6FB0", "4A78B0", "2F8BFF", "4F8CFF" };
        private static Brush _grad;

        public static Brush Grad()
        {
            if (_grad == null)
            {
                LinearGradientBrush b = new LinearGradientBrush();
                b.StartPoint = new System.Windows.Point(0, 0); b.EndPoint = new System.Windows.Point(1, 1);
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF9F0A"), 0.0));
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF375F"), 0.55));
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#BF5AF2"), 1.0));
                b.Freeze();
                _grad = b;
            }
            return _grad;
        }

        public static bool IsGrad(string hex)
        {
            if (!Modern || hex == null || hex.Length != 7 || hex[0] != '#') return false;
            return Array.IndexOf(GradKeys, hex.Substring(1).ToUpperInvariant()) >= 0;
        }

        public static string CurrentAccentHex(AppSettings s)
        {
            string name = s.GetString("Accent");
            int i = Array.IndexOf(AccentNames, name);
            if (i >= 0) return AccentHex[i];
            string x = s.GetString("AccentHex");
            if (Regex.IsMatch(x, "^#[0-9A-Fa-f]{6}$")) return x.ToUpperInvariant();
            return "#4FD1FF";
        }

        private static double[] ToHsl(string hex)
        {
            string h = hex.TrimStart('#');
            double r = Convert.ToInt32(h.Substring(0, 2), 16) / 255.0, g = Convert.ToInt32(h.Substring(2, 2), 16) / 255.0, b = Convert.ToInt32(h.Substring(4, 2), 16) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2.0, d = max - min, hh = 0, ss = 0;
            if (d > 0)
            {
                ss = d / (1 - Math.Abs(2 * l - 1));
                if (max == r) hh = 60 * (((g - b) / d) % 6);
                else if (max == g) hh = 60 * (((b - r) / d) + 2);
                else hh = 60 * (((r - g) / d) + 4);
                if (hh < 0) hh += 360;
            }
            return new double[] { hh, ss, l };
        }

        private static string FromHsl(double H, double S, double L)
        {
            H = ((H % 360) + 360) % 360; S = Math.Max(0.0, Math.Min(1.0, S)); L = Math.Max(0.0, Math.Min(1.0, L));
            double c = (1 - Math.Abs(2 * L - 1)) * S, x = c * (1 - Math.Abs(((H / 60) % 2) - 1)), m = L - c / 2, r = 0, g = 0, b = 0;
            switch ((int)Math.Floor(H / 60))
            {
                case 0: r = c; g = x; break; case 1: r = x; g = c; break; case 2: g = c; b = x; break;
                case 3: g = x; b = c; break; case 4: r = x; b = c; break; default: r = c; b = x; break;
            }
            return "#" + ((int)Math.Round((r + m) * 255)).ToString("X2") + ((int)Math.Round((g + m) * 255)).ToString("X2") + ((int)Math.Round((b + m) * 255)).ToString("X2");
        }

        public static void Build(AppSettings s)
        {
            Light = s.GetString("Theme") != "Dark";
            Modern = s.GetString("UiStyle") != "Classic";
            string aHex = CurrentAccentHex(s);
            bool ocean = aHex == "#4FD1FF";
            string bgc = s.GetString("BgColor");
            bool hasBgc = Regex.IsMatch(bgc, "^#[0-9A-Fa-f]{6}$");
            double h = ToHsl(aHex)[0]; if (ocean) h = 211;
            Dictionary<string, string> pal = new Dictionary<string, string>();
            if (Light)
            {
                foreach (string k in Surf) pal[k] = LightMap[k];
                foreach (string k in Text) pal[k] = LightMap[k];
                foreach (string k in Status) pal[k] = LightMap[k];
                pal["WHITE"] = "#111827";
                if (hasBgc) pal["1F2329"] = bgc.ToUpperInvariant();
                if (!ocean) { pal["1E3547"] = FromHsl(h, 0.30, 0.92); pal["35506A"] = FromHsl(h, 0.25, 0.78); }
                pal["4FD1FF"] = FromHsl(h, 0.85, 0.38); pal["2F6FB0"] = FromHsl(h, 0.57, 0.44); pal["4A78B0"] = FromHsl(h, 0.45, 0.46);
                pal["2F8BFF"] = FromHsl(h, 0.90, 0.46); pal["4F8CFF"] = pal["2F8BFF"]; pal["1E3A4C"] = FromHsl(h, 0.70, 0.92);
            }
            else
            {
                int vi = Array.IndexOf(BgNames, s.GetString("Background")); if (vi < 0) vi = 0;
                double[] v = BgVar[vi];
                if (hasBgc)
                {
                    double[] c = ToHsl(bgc);
                    v = new double[] { c[0], Math.Max(0.0, Math.Min(4.0, c[1] / 0.17)), Math.Max(0.3, Math.Min(3.0, c[2] / 0.15)) };
                }
                foreach (string k in Surf)
                {
                    pal[k] = "#" + k;
                    if (v[0] >= 0 || v[1] != 1.0 || v[2] != 1.0)
                    {
                        double[] x = ToHsl("#" + k); double hh = x[0], ss = Math.Min(1.0, x[1] * v[1]);
                        if (v[0] >= 0) { hh = v[0]; ss = Math.Min(0.55, x[1] * v[1]); }
                        pal[k] = FromHsl(hh, ss, Math.Min(0.95, x[2] * v[2]));
                    }
                }
                foreach (string k in Text) pal[k] = "#" + k;
                foreach (string k in Status) pal[k] = "#" + k;
                pal["WHITE"] = "#FFFFFF";
                if (ocean)
                {
                    pal["4FD1FF"] = "#4FD1FF"; pal["2F6FB0"] = "#2F6FB0"; pal["4A78B0"] = "#4A78B0"; pal["2F8BFF"] = "#2F8BFF"; pal["4F8CFF"] = "#4F8CFF"; pal["1E3A4C"] = "#1E3A4C";
                }
                else
                {
                    pal["4FD1FF"] = aHex; pal["2F6FB0"] = FromHsl(h, 0.57, 0.44); pal["4A78B0"] = FromHsl(h, 0.40, 0.49);
                    pal["2F8BFF"] = FromHsl(h, 1.00, 0.59); pal["4F8CFF"] = pal["2F8BFF"]; pal["1E3A4C"] = FromHsl(h, 0.38, 0.22);
                    pal["1E3547"] = FromHsl(h, 0.35, 0.21); pal["35506A"] = FromHsl(h, 0.33, 0.32);
                }
            }
            // modern look: the default (Ocean) accent becomes the brand pink; a colour picked by the user is kept
            if (Modern && ocean) pal["4FD1FF"] = Light ? "#E0244F" : "#FF375F";
            Pal = pal;
        }

        // "#RRGGBB" -> palette colour when it is one of the themed keys
        public static string Map(string hex)
        {
            if (hex != null && hex.Length == 7 && hex[0] == '#')
            {
                string v; if (Pal.TryGetValue(hex.Substring(1).ToUpperInvariant(), out v)) return v;
            }
            return hex;
        }

        private const string GradXaml = "<LinearGradientBrush x:Key=\"RockGrad\" StartPoint=\"0,0\" EndPoint=\"1,1\"><GradientStop Color=\"#FF9F0A\" Offset=\"0\"/><GradientStop Color=\"#FF375F\" Offset=\"0.55\"/><GradientStop Color=\"#BF5AF2\" Offset=\"1\"/></LinearGradientBrush>";

        // modern look: tabs move into a side menu, corners get rounder
        private static string ModernShape(string text)
        {
            text = text.Replace("<Border DockPanel.Dock=\"Top\" Background=\"#181B20\" BorderBrush=\"#2B3038\" BorderThickness=\"1\" CornerRadius=\"6\" Padding=\"8,8,8,2\" Margin=\"0,0,0,10\">",
                "<Border DockPanel.Dock=\"Left\" Width=\"188\" Background=\"#181B20\" BorderBrush=\"#2B3038\" BorderThickness=\"1\" CornerRadius=\"6\" Padding=\"8\" Margin=\"0,0,10,0\">");
            text = text.Replace("<WrapPanel x:Name=\"tabBar\" Orientation=\"Horizontal\"/>",
                "<ScrollViewer VerticalScrollBarVisibility=\"Auto\"><StackPanel x:Name=\"tabBar\"/></ScrollViewer>");
            text = text.Replace("<Setter Property=\"SelectionBrush\" Value=\"#4A78B0\"/>", "<Setter Property=\"SelectionBrush\" Value=\"#35506A\"/>");
            text = text.Replace("CornerRadius=\"3\"", "CornerRadius=\"9\"");
            text = text.Replace("CornerRadius=\"5\"", "CornerRadius=\"9\"");
            text = text.Replace("CornerRadius=\"6\"", "CornerRadius=\"13\"");
            text = text.Replace("CornerRadius=\"8\"", "CornerRadius=\"12\"");
            return text;
        }

        // rewrites the hex colours of a XAML text through the palette
        public static string ConvertXaml(string text)
        {
            if (Modern) text = ModernShape(text);
            text = Regex.Replace(text, "<Button\\b[^>]*>", delegate(Match m)
            {
                string t = m.Value;
                if (Regex.IsMatch(t, "Background=\"#(2F6FB0|B0343A)\"") && !t.Contains("Foreground=")) t = "<Button Foreground=\"#FFFFFF\"" + t.Substring(7);
                return t;
            });
            text = Regex.Replace(text, "=\"#([0-9A-Fa-f]{6})\"", delegate(Match m)
            {
                string k = m.Groups[1].Value.ToUpperInvariant(); string v;
                if (Modern && Array.IndexOf(GradKeys, k) >= 0) return "=\"{StaticResource RockGrad}\"";
                return Pal.TryGetValue(k, out v) ? "=\"" + v + "\"" : m.Value;
            });
            string white; if (!Pal.TryGetValue("WHITE", out white)) white = "#FFFFFF";
            text = Regex.Replace(text, "(Foreground|CaretBrush)=\"White\"", delegate(Match m) { return m.Groups[1].Value + "=\"" + white + "\""; });
            if (Modern)
            {
                int i = text.IndexOf("<Window.Resources>", StringComparison.Ordinal);
                if (i >= 0) text = text.Insert(i + "<Window.Resources>".Length, GradXaml);
            }
            return text;
        }
    }
}
