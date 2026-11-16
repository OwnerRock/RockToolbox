using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace RDownloader.Native
{
    // the Options window settings that change how requests are made (proxy, User-Agent, site logins, per-server limit, virus scan)
    internal static class DlOpts
    {
        public const string DefaultUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RDownloader";

        public static string UserAgent
        {
            get { string s = (Ui.Settings.GetString("UserAgent") ?? "").Trim(); return s.Length == 0 ? DefaultUa : s; }
        }

        // proxy: ProxyMode = system | none | manual (ProxyHost = host:port, ProxyUser, ProxyPass)
        public static IWebProxy BuildProxy()
        {
            string mode = Ui.Settings.GetString("ProxyMode");
            if (mode == "none") return null;
            if (mode == "manual")
            {
                string host = (Ui.Settings.GetString("ProxyHost") ?? "").Trim();
                if (host.Length > 0)
                {
                    if (!host.Contains("://")) host = "http://" + host;
                    try
                    {
                        WebProxy p = new WebProxy(new Uri(host));
                        string u = Ui.Settings.GetString("ProxyUser") ?? "";
                        if (u.Length > 0) p.Credentials = new NetworkCredential(u, Ui.Settings.GetString("ProxyPass") ?? "");
                        return p;
                    }
                    catch (Exception) { }
                }
            }
            IWebProxy sys = WebRequest.GetSystemWebProxy();
            try { sys.Credentials = CredentialCache.DefaultCredentials; } catch (Exception) { }
            return sys;
        }

        // site logins are stored one per line:  host<TAB>user<TAB>password
        public static List<string[]> Logins()
        {
            List<string[]> l = new List<string[]>();
            foreach (string line in (Ui.Settings.GetString("SiteLogins") ?? "").Split('\n'))
            {
                string[] p = line.TrimEnd('\r').Split('\t');
                if (p.Length >= 3 && p[0].Length > 0) l.Add(p);
            }
            return l;
        }
        public static void SaveLogins(List<string[]> l)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string[] p in l) sb.Append(p[0]).Append('\t').Append(p[1]).Append('\t').Append(p[2]).Append('\n');
            Ui.Settings.Set("SiteLogins", sb.ToString()); Ui.Settings.SaveSoon();
        }

        public static void Apply(HttpWebRequest r, string url)
        {
            r.UserAgent = UserAgent;
            try { IWebProxy px = BuildProxy(); if (Ui.Settings.GetString("ProxyMode") == "none") r.Proxy = null; else if (px != null) r.Proxy = px; } catch (Exception) { }
            try
            {
                Uri u = new Uri(url);
                foreach (string[] l in Logins())
                    if (u.Host.Equals(l[0], StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + l[0], StringComparison.OrdinalIgnoreCase))
                    { r.Credentials = new NetworkCredential(l[1], l[2]); r.PreAuthenticate = true; break; }
            }
            catch (Exception) { }
        }

        // 0 = no extra limit
        public static int PerServer { get { int n = 0; int.TryParse(Ui.Settings.GetString("PerServer"), out n); return n < 0 ? 0 : n; } }
        public static int CapConns(int n) { int c = PerServer; return c > 0 ? Math.Min(n, c) : n; }

        // runs the virus-scan command on a finished file:  ScanCmd = path to a program, ScanArgs = arguments ({file} is replaced)
        public static void Scan(string file)
        {
            string cmd = (Ui.Settings.GetString("ScanCmd") ?? "").Trim();
            if (cmd.Length == 0 || !File.Exists(file)) return;
            string args = (Ui.Settings.GetString("ScanArgs") ?? "").Trim();
            args = args.Length == 0 ? "\"" + file + "\"" : args.Replace("{file}", file);
            try { ProcessStartInfo si = new ProcessStartInfo(cmd, args); si.UseShellExecute = true; Process.Start(si); Ui.Log.Info("Virus scan started for " + Path.GetFileName(file), "Downloads"); }
            catch (Exception ex) { Ui.Log.Warn("Could not start the virus scan: " + ex.Message, "Downloads"); }
        }
    }
}
