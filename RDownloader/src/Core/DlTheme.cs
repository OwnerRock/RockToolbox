using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;

namespace RDownloader.Native
{
    // The download windows are drawn in one calm dark palette (ui\CalmStyles.xaml). This class re-colours that palette for
    // light mode and for the Modern look (orange > pink > purple), so the windows follow the choice made in Settings.
    internal static class DlTheme
    {
        private static readonly Dictionary<string, string> Light = new Dictionary<string, string>
        {
            { "1B2027", "#F3F6FA" }, { "171C22", "#E7ECF2" }, { "232A33", "#FFFFFF" }, { "1F252D", "#F6F8FB" }, { "28313B", "#EEF2F7" },
            { "313B47", "#C9D2DE" }, { "2A323C", "#DCE3EC" }, { "2D3743", "#E3E9F1" }, { "364352", "#D3DCE7" }, { "46525F", "#AEB9C7" },
            { "10141A", "#E9EEF4" }, { "26303A", "#EDF1F6" }, { "202730", "#EEF2F7" }, { "B94441", "#C62828" }, { "D9534F", "#D9534F" },
            { "E4E9F0", "#1F2733" }, { "8895A6", "#5E6B7D" }, { "AAB6C4", "#445268" }, { "C9D2DD", "#334055" },
            { "52B788", "#1E8E4E" }, { "E5827F", "#D32F2F" }, { "E0A76B", "#B36B00" }, { "E0B26B", "#B7791F" }, { "7C9CD8", "#3F63B5" }, { "D88C9A", "#B24A64" }, { "6BC4B0", "#1F8F7A" },
            { "1F3A33", "#DDF3E7" }, { "1B1F25", "#FFFFFF" }, { "B8C0CC", "#4A5568" }, { "4DB6A0", "#1F8F7A" }, { "E5604D", "#D32F2F" }
        };

        public static string Map(string hex)
        {
            string k = hex.TrimStart('#').ToUpperInvariant();
            if (Theme.Modern)
            {
                if (k == "4DA3C4") k = "FF375F";
                else if (k == "4FB7A8") k = "BF5AF2";
            }
            else if (Theme.Light)
            {
                if (k == "4DA3C4") return "#2B86A8";
                if (k == "4FB7A8") return "#1F9A8A";
            }
            string v;
            if (Theme.Light && Light.TryGetValue(k, out v)) return v;
            return "#" + k;
        }

        public static Brush Br(string hex) { return Ui.Brush(Map(hex)); }

        public static LinearGradientBrush GradBrush()
        {
            string a = Theme.Modern ? "#FF9F0A" : Map("#4DA3C4"), b = Theme.Modern ? "#BF5AF2" : Map("#4FB7A8");
            LinearGradientBrush g = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), 0);
            return g;
        }

        public static string Convert(string xaml)
        {
            if (Theme.Modern)
            {
                // two-colour gradients become the three-colour brand gradient
                xaml = Regex.Replace(xaml, "<GradientStop Color=\"#4DA3C4\" Offset=\"0\"/><GradientStop Color=\"#4FB7A8\" Offset=\"1\"/>",
                    "<GradientStop Color=\"#FF9F0A\" Offset=\"0\"/><GradientStop Color=\"#FF375F\" Offset=\"0.55\"/><GradientStop Color=\"#BF5AF2\" Offset=\"1\"/>");
            }
            return Regex.Replace(xaml, "#([0-9A-Fa-f]{6})\\b", delegate(Match m)
            {
                string v = m.Value;
                if (Theme.Modern && (v.ToUpperInvariant() == "#FF9F0A" || v.ToUpperInvariant() == "#FF375F" || v.ToUpperInvariant() == "#BF5AF2")) return v;
                return Map(v);
            });
        }

        // loads one of the download windows with the current light / dark, Classic / Modern and language settings
        public static Window Load(string rel)
        {
            string xaml = Convert(XamlLoader.Read(Ui.Root, rel));
            xaml = Lang.ConvertXaml(xaml);
            using (StringReader sr = new StringReader(xaml))
            using (XmlReader xr = XmlReader.Create(sr))
            {
                return (Window)XamlReader.Load(xr);
            }
        }
    }
}
