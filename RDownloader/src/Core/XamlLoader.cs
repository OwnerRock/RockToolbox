using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace RDownloader.Native
{
    // Loads ui\MainWindow.xaml and expands every <!-- @include path --> line, exactly like the PowerShell Get-XamlSource.
    internal static class XamlLoader
    {
        private static readonly Regex Include = new Regex(@"[ \t]*<!--\s*@include\s+(\S+)\s*-->", RegexOptions.Compiled);

        public static string Read(string uiRoot, string relative)
        {
            string path = Path.Combine(uiRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Missing UI file: " + path);
            string txt = File.ReadAllText(path, Encoding.UTF8).TrimEnd();
            return Include.Replace(txt, delegate(Match m) { return Read(uiRoot, m.Groups[1].Value); });
        }

        // loads any other window (welcome, profile, about) with the same theme + language conversion
        public static Window LoadWindow(string uiRoot, string relative)
        {
            string xaml = Lang.ConvertXaml(Theme.ConvertXaml(Read(uiRoot, relative)));
            using (StringReader sr = new StringReader(xaml))
            using (XmlReader xr = XmlReader.Create(sr))
            {
                return (Window)XamlReader.Load(xr);
            }
        }

        public static Window LoadMainWindow(string uiRoot)
        {
            string xaml = Lang.ConvertXaml(Theme.ConvertXaml(Read(uiRoot, "ui/MainWindow.xaml")));
            using (StringReader sr = new StringReader(xaml))
            using (XmlReader xr = XmlReader.Create(sr))
            {
                return (Window)XamlReader.Load(xr);
            }
        }
    }
}
