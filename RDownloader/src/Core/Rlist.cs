using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RDownloader.Native
{
    internal sealed class RlistEntry
    {
        public string Url = "", Name = "";
        public RlistEntry() { }
        public RlistEntry(string url, string name) { Url = url ?? ""; Name = name ?? ""; }
    }

    // The .Rlist file: a small UTF-8 text file with download links.
    //   ROCKLIST 1
    //   name=My list
    //   dir=C:\Downloads\Rock          (optional)
    //   <link><TAB><file name>        (one line per file; the name can be empty)
    // Lines that start with # are comments.
    internal sealed class Rlist
    {
        public const string Ext = ".Rlist";
        public string Path = "", Name = "", Dir = "";
        public List<RlistEntry> Entries = new List<RlistEntry>();

        public static string Root
        {
            get
            {
                string r = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDownloader");
                try { Directory.CreateDirectory(r); } catch (Exception) { }
                return r;
            }
        }
        private static string KnownFile { get { return System.IO.Path.Combine(Root, "rlists.txt"); } }
        public static string QueueFile { get { return System.IO.Path.Combine(Root, "open.queue"); } }

        public static bool IsRlistPath(string p)
        {
            return !string.IsNullOrEmpty(p) && p.EndsWith(Ext, StringComparison.OrdinalIgnoreCase);
        }

        public static Rlist Load(string path)
        {
            Rlist r = new Rlist();
            r.Path = path;
            r.Name = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#") || l.StartsWith("ROCKLIST", StringComparison.OrdinalIgnoreCase)) continue;
                if (l.StartsWith("name=", StringComparison.OrdinalIgnoreCase)) { r.Name = l.Substring(5).Trim(); continue; }
                if (l.StartsWith("dir=", StringComparison.OrdinalIgnoreCase)) { r.Dir = l.Substring(4).Trim(); continue; }
                // "link<TAB>name" (what the app writes) - but links typed by hand in Notepad also work:
                // just a link, or "link name" separated by spaces, or "link | name"
                string url, nm = "";
                int tab = l.IndexOf('\t');
                if (tab >= 0) { url = l.Substring(0, tab).Trim(); nm = l.Substring(tab + 1).Trim(); }
                else
                {
                    int sp = l.IndexOfAny(new char[] { ' ', '|' });
                    if (sp > 0) { url = l.Substring(0, sp).Trim(); nm = l.Substring(sp + 1).Trim().TrimStart('|', ' '); }
                    else url = l;
                }
                if (url.Length == 0) continue;
                if (url.IndexOf("://", StringComparison.Ordinal) < 0 && !url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                {
                    if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) url = "http://" + url; else continue;
                }
                r.Entries.Add(new RlistEntry(url, nm));
            }
            return r;
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ROCKLIST 1");
            sb.AppendLine("# R-Downloader download list - you can edit this file in Notepad.");
            sb.AppendLine("# Add one download per line:  link  (a file name after a TAB or a space is optional).");
            sb.AppendLine("name=" + (Name ?? ""));
            if (!string.IsNullOrEmpty(Dir)) sb.AppendLine("dir=" + Dir);
            foreach (RlistEntry e in Entries) sb.AppendLine(e.Url + "\t" + (e.Name ?? ""));
            string d = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
            File.WriteAllText(Path, sb.ToString(), new UTF8Encoding(false));
            Remember(Path);
        }

        // adds a link (a link that is already in the list is not added twice). true = added
        public bool Add(string url, string name)
        {
            foreach (RlistEntry e in Entries) if (string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase)) return false;
            Entries.Add(new RlistEntry(url, name));
            return true;
        }

        // ---- the lists this app made or opened (for the "choose a list" menu) ----
        public static List<string> Known()
        {
            List<string> r = new List<string>();
            try
            {
                if (File.Exists(KnownFile))
                    foreach (string l in File.ReadAllLines(KnownFile, Encoding.UTF8))
                    { string t = l.Trim(); if (t.Length > 0 && File.Exists(t) && !r.Contains(t)) r.Add(t); }
            }
            catch (Exception) { }
            return r;
        }

        public static void Remember(string path)
        {
            try
            {
                List<string> k = Known();
                k.RemoveAll(delegate(string s) { return string.Equals(s, path, StringComparison.OrdinalIgnoreCase); });
                k.Insert(0, path);
                if (k.Count > 30) k.RemoveRange(30, k.Count - 30);
                File.WriteAllLines(KnownFile, k.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        // ---- the second copy of the app hands its .Rlist files to the running copy through this queue file ----
        public static void Enqueue(string path)
        {
            try { File.AppendAllText(QueueFile, path + "\r\n", Encoding.UTF8); } catch (Exception) { }
        }

        public static List<string> TakeQueue()
        {
            List<string> r = new List<string>();
            try
            {
                string f = QueueFile;
                if (!File.Exists(f)) return r;
                string tmp = f + "." + Guid.NewGuid().ToString("N");
                File.Move(f, tmp);
                foreach (string l in File.ReadAllLines(tmp, Encoding.UTF8)) { string t = l.Trim(); if (t.Length > 0) r.Add(t); }
                try { File.Delete(tmp); } catch (Exception) { }
            }
            catch (Exception) { }
            return r;
        }
    }
}
