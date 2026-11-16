using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace RockToolbox.Native
{
    // Download size of an app: winget show -> installer url -> HEAD request (Content-Length). Cached; never blocks the UI.
    internal static class AppSize
    {
        private static readonly Dictionary<string, long> Cache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Lock = new object();

        public static string Format(long b)
        {
            if (b <= 0) return "-";
            double mb = b / 1048576.0;
            if (mb >= 1024) return (mb / 1024).ToString("0.0") + " GB";
            if (mb >= 1) return mb.ToString("0") + " MB";
            return "< 1 MB";
        }

        public static bool TryCached(string id, out long size) { lock (Lock) { return Cache.TryGetValue(id, out size); } }

        // done(size) runs on a worker thread; size 0 = unknown
        public static void Fetch(string id, Action<long> done)
        {
            long c;
            if (TryCached(id, out c)) { done(c); return; }
            ThreadPool.QueueUserWorkItem(delegate
            {
                long size = 0;
                try
                {
                    string txt = Tool.Capture("winget", "show --id " + id + " --exact --accept-source-agreements --disable-interactivity");
                    Match m = Regex.Match(txt ?? "", @"Installer Url:\s*(https?://\S+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(m.Groups[1].Value);
                        rq.Method = "HEAD"; rq.Timeout = 12000; rq.AllowAutoRedirect = true; rq.UserAgent = "RockToolbox";
                        using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse()) size = rs.ContentLength > 0 ? rs.ContentLength : 0;
                    }
                }
                catch { }
                lock (Lock) { Cache[id] = size; }
                try { done(size); } catch { }
            });
        }
    }
}
