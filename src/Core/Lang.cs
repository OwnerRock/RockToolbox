<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace RockToolbox.Native
{
    // Interface languages. A language is a small JSON file (English text -> translated text) in the "lang" folder:
    // Arabic ships with the program, the others are downloaded once from the official repo.
    internal static class Lang
    {
        public static readonly string[][] List = {
            new string[] { "English", "en", "English" }, new string[] { "Arabic", "ar", "العربية" }, new string[] { "French", "fr", "Français" },
            new string[] { "Spanish", "es", "Español" }, new string[] { "German", "de", "Deutsch" }, new string[] { "Turkish", "tr", "Türkçe" },
            new string[] { "Russian", "ru", "Русский" }, new string[] { "Chinese", "zh", "中文" }, new string[] { "Japanese", "ja", "日本語" },
            new string[] { "Portuguese", "pt", "Português" }, new string[] { "Italian", "it", "Italiano" }, new string[] { "Hindi", "hi", "हिन्दी" } };

        private const string PackBase = "https://raw.githubusercontent.com/OwnerRock/RockToolbox/main/lang/";
        private static readonly Dictionary<string, string> Loc = new Dictionary<string, string>();
        public static string Name = "English";

        public static bool Translated { get { return Name != "English"; } }
        public static bool Rtl { get { return Name == "Arabic" || Name == "Persian" || Name == "Hebrew"; } }

        private static string AppDir { get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); } }
        private static string UserDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RockToolbox\lang"); } }

        public static string Code(string name) { foreach (string[] l in List) if (l[0] == name) return l[1]; return null; }

        private static string PackPath(string name)
        {
            string c = Code(name); if (c == null) return null;
            string a = Path.Combine(Path.Combine(AppDir, "lang"), c + ".json"); if (File.Exists(a)) return a;
            string u = Path.Combine(UserDir, c + ".json"); if (File.Exists(u)) return u;
            return null;
        }

        public static bool HasPack(string name) { return name == "English" || PackPath(name) != null; }

        private static bool Import(string name)
        {
            try
            {
                string p = PackPath(name); if (p == null) return false;
                JavaScriptSerializer ser = new JavaScriptSerializer(); ser.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> j = ser.DeserializeObject(File.ReadAllText(p, System.Text.Encoding.UTF8)) as Dictionary<string, object>;
                if (j == null) return false;
                Loc.Clear();
                foreach (KeyValuePair<string, object> kv in j) if (kv.Value != null && Convert.ToString(kv.Value).Length > 0) Loc[kv.Key] = Convert.ToString(kv.Value);
                return Loc.Count > 0;
            }
            catch { return false; }
        }

        public static void Init(AppSettings s)
        {
            string n = s.GetString("Language");
            Name = "English";
            if (n.Length > 0 && n != "English" && Import(n)) Name = n;
        }

        public static string L(string text)
        {
            if (!Translated || text == null) return text;
            string t; return Loc.TryGetValue(text, out t) ? t : text;
        }

        // translates Text= / Content= / ToolTip= attributes of a XAML text
        public static string ConvertXaml(string text)
        {
            if (!Translated) return text;
            return Regex.Replace(text, "(?<=\\b(?:Text|Content|ToolTip)=\")([^\"]*)", delegate(Match m)
            {
                string k = WebUtility.HtmlDecode(m.Value); string v;
                return Loc.TryGetValue(k, out v) ? SecurityElement.Escape(v) : m.Value;
            });
        }

        // downloads a language pack (used when a non-built-in language is picked)
        public static string Download(string name)
        {
            string c = Code(name); if (c == null) return "Unknown language.";
            string last = "";
            foreach (string url in new string[] { PackBase + c + ".json", "https://github.com/OwnerRock/RockToolbox/raw/main/lang/" + c + ".json" })
            {
                try
                {
                    Directory.CreateDirectory(UserDir);
                    string file = Path.Combine(UserDir, c + ".json");
                    Net.Download(url, file, null, null);
                    if (new FileInfo(file).Length > 10) return "";
                }
                catch (Exception ex) { last = ex.Message; }
            }
            return last.Length > 0 ? last : "No data received.";
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace RockToolbox.Native
{
    // Interface languages. A language is a small JSON file (English text -> translated text) in the "lang" folder:
    // Arabic ships with the program, the others are downloaded once from the official repo.
    internal static class Lang
    {
        public static readonly string[][] List = {
            new string[] { "English", "en", "English" }, new string[] { "Arabic", "ar", "العربية" }, new string[] { "French", "fr", "Français" },
            new string[] { "Spanish", "es", "Español" }, new string[] { "German", "de", "Deutsch" }, new string[] { "Turkish", "tr", "Türkçe" },
            new string[] { "Russian", "ru", "Русский" }, new string[] { "Chinese", "zh", "中文" }, new string[] { "Japanese", "ja", "日本語" },
            new string[] { "Portuguese", "pt", "Português" }, new string[] { "Italian", "it", "Italiano" }, new string[] { "Hindi", "hi", "हिन्दी" } };

        private const string PackBase = "https://raw.githubusercontent.com/OwnerRock/RockToolbox/main/lang/";
        private static readonly Dictionary<string, string> Loc = new Dictionary<string, string>();
        public static string Name = "English";

        public static bool Translated { get { return Name != "English"; } }
        public static bool Rtl { get { return Name == "Arabic" || Name == "Persian" || Name == "Hebrew"; } }

        private static string AppDir { get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); } }
        private static string UserDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RockToolbox\lang"); } }

        public static string Code(string name) { foreach (string[] l in List) if (l[0] == name) return l[1]; return null; }

        private static string PackPath(string name)
        {
            string c = Code(name); if (c == null) return null;
            string a = Path.Combine(Path.Combine(AppDir, "lang"), c + ".json"); if (File.Exists(a)) return a;
            string u = Path.Combine(UserDir, c + ".json"); if (File.Exists(u)) return u;
            return null;
        }

        public static bool HasPack(string name) { return name == "English" || PackPath(name) != null; }

        private static bool Import(string name)
        {
            try
            {
                string p = PackPath(name); if (p == null) return false;
                JavaScriptSerializer ser = new JavaScriptSerializer(); ser.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> j = ser.DeserializeObject(File.ReadAllText(p, System.Text.Encoding.UTF8)) as Dictionary<string, object>;
                if (j == null) return false;
                Loc.Clear();
                foreach (KeyValuePair<string, object> kv in j) if (kv.Value != null && Convert.ToString(kv.Value).Length > 0) Loc[kv.Key] = Convert.ToString(kv.Value);
                return Loc.Count > 0;
            }
            catch { return false; }
        }

        public static void Init(AppSettings s)
        {
            string n = s.GetString("Language");
            Name = "English";
            if (n.Length > 0 && n != "English" && Import(n)) Name = n;
        }

        public static string L(string text)
        {
            if (!Translated || text == null) return text;
            string t; return Loc.TryGetValue(text, out t) ? t : text;
        }

        // translates Text= / Content= / ToolTip= attributes of a XAML text
        public static string ConvertXaml(string text)
        {
            if (!Translated) return text;
            return Regex.Replace(text, "(?<=\\b(?:Text|Content|ToolTip)=\")([^\"]*)", delegate(Match m)
            {
                string k = WebUtility.HtmlDecode(m.Value); string v;
                return Loc.TryGetValue(k, out v) ? SecurityElement.Escape(v) : m.Value;
            });
        }

        // downloads a language pack (used when a non-built-in language is picked)
        public static string Download(string name)
        {
            string c = Code(name); if (c == null) return "Unknown language.";
            string last = "";
            foreach (string url in new string[] { PackBase + c + ".json", "https://github.com/OwnerRock/RockToolbox/raw/main/lang/" + c + ".json" })
            {
                try
                {
                    Directory.CreateDirectory(UserDir);
                    string file = Path.Combine(UserDir, c + ".json");
                    Net.Download(url, file, null, null);
                    if (new FileInfo(file).Length > 10) return "";
                }
                catch (Exception ex) { last = ex.Message; }
            }
            return last.Length > 0 ? last : "No data received.";
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
