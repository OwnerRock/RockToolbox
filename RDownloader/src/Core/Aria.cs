using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace RDownloader.Native
{
    // Torrent / magnet downloads: the work is done by aria2c.exe (fetched once from the official aria2 GitHub releases,
    // or the file you choose in Settings -> "AriaPath").
    internal static class Aria
    {
        public static bool IsMagnet(string u) { return !string.IsNullOrEmpty(u) && u.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase); }

        public static bool IsTorrentLink(string u)
        {
            if (string.IsNullOrEmpty(u)) return false;
            if (IsMagnet(u)) return true;
            string t = u.Trim();
            int q = t.IndexOf('?'); if (q > 0) t = t.Substring(0, q);
            return t.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
        }

        public static string NameOf(string u)
        {
            try
            {
                if (IsMagnet(u))
                {
                    Match m = Regex.Match(u, @"[?&]dn=([^&]+)");
                    if (m.Success) return Uri.UnescapeDataString(m.Groups[1].Value.Replace('+', ' '));
                    Match h = Regex.Match(u, @"btih:([A-Za-z0-9]+)");
                    return "Torrent " + (h.Success ? h.Groups[1].Value.Substring(0, Math.Min(8, h.Groups[1].Value.Length)) : "magnet");
                }
                string t = u.Trim(); int q = t.IndexOf('?'); if (q > 0) t = t.Substring(0, q);
                string n = Path.GetFileNameWithoutExtension(t.Replace('/', Path.DirectorySeparatorChar));
                return string.IsNullOrEmpty(n) ? "Torrent" : Uri.UnescapeDataString(n);
            }
            catch (Exception) { return "Torrent"; }
        }

        public static string ExePath
        {
            get
            {
                string custom = (Ui.Settings.GetString("AriaPath") ?? "").Trim();
                if (custom.Length > 0 && File.Exists(custom)) return custom;
                return Path.Combine(Rlist.Root, @"aria2\aria2c.exe");
            }
        }

        // makes sure aria2c.exe exists (downloads it the first time). Throws with a readable message when that fails.
        public static string Ensure(Action<string> say)
        {
            string exe = ExePath;
            if (File.Exists(exe)) return exe;
            if (say != null) say("Getting aria2 (one time)...");
            string dir = Path.GetDirectoryName(exe);
            Directory.CreateDirectory(dir);
            object rel = Net.GetJson("https://api.github.com/repos/aria2/aria2/releases/latest", 20000);
            string url = "";
            foreach (object a in Net.Arr(Net.Get(rel, "assets")))
            {
                string nm = Net.Str(a, "name");
                if (Regex.IsMatch(nm, @"win-64bit.*\.zip$", RegexOptions.IgnoreCase)) { url = Net.Str(a, "browser_download_url"); break; }
            }
            if (url.Length == 0) throw new InvalidOperationException("Could not find aria2 for Windows in the official releases. Put aria2c.exe somewhere and set its path in the settings (AriaPath).");
            string zip = Path.Combine(dir, "aria2.zip");
            Net.Download(url, zip, null, null);
            string tmp = Path.Combine(dir, "x");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);
            int code;
            Tool.Capture(Tool.System32("tar.exe"), "-xf \"" + zip + "\" -C \"" + tmp + "\"", out code);
            string[] f = Directory.GetFiles(tmp, "aria2c.exe", SearchOption.AllDirectories);
            if (f.Length == 0) throw new InvalidOperationException("Could not unpack aria2 (tar code " + code + ").");
            File.Copy(f[0], exe, true);
            try { Directory.Delete(tmp, true); File.Delete(zip); } catch (Exception) { }
            return exe;
        }
    }

    // one torrent / magnet download = one aria2c.exe process; its console readout gives the progress
    internal sealed class AriaJob : IDlJob
    {
        private readonly string link, dir;
        private long limit, done, total;
        private volatile string state = "idle", err = "", final = "";
        private volatile bool finished;
        private double speed;
        private Process proc;
        private Thread th;

        // seeding after the download: minutes (0 = do not seed), stop ratio, upload limit (KB/s) - set in Options > Torrents
        private static string SeedArgs()
        {
            int min = 0, up = 0; double ratio = 0;
            int.TryParse(Ui.Settings.GetString("TorSeedMin"), out min); int.TryParse(Ui.Settings.GetString("TorUpKb"), out up);
            double.TryParse((Ui.Settings.GetString("TorRatio") ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ratio);
            if (min < 0) min = 0; if (up < 0) up = 0;
            string r = min.ToString() + (ratio > 0 ? " --seed-ratio=" + ratio.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) : (min > 0 ? " --seed-ratio=0.0" : ""));
            if (up > 0) r += " --max-upload-limit=" + up + "K";
            return r;
        }
        public AriaJob(string link, string dir, long limit) { this.link = link; this.dir = dir; this.limit = limit; final = dir; }

        public long Done { get { return Interlocked.Read(ref done); } }
        public long Total { get { return Interlocked.Read(ref total); } }
        public double Speed { get { return speed; } }
        public string State { get { return state; } }
        public bool Finished { get { return finished; } }
        public string Err { get { return err; } }
        public string Final { get { return final; } }
        public string Hash { get { return ""; } }
        public string SrvName { get { return ""; } }
        public long Limit { get { return Interlocked.Read(ref limit); } set { Interlocked.Exchange(ref limit, value); } }
        public string GetSegState() { return ""; }
        public void SetResume(string st) { }
        public long[][] SegInfo() { return new long[0][]; }

        public void Begin()
        {
            if (th != null && th.IsAlive) return;
            finished = false; err = ""; speed = 0; state = "run";
            th = new Thread(Run); th.IsBackground = true; th.Start();
        }

        public void Stop(string newState)
        {
            if (state != "run") return;
            state = newState;
            try { Process p = proc; if (p != null && !p.HasExited) Work.KillTreePublic(p); } catch (Exception) { }
        }

        private static long Num(string s)
        {
            double d; if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return (long)d;
            return 0;
        }

        private void Run()
        {
            string lastErr = "";
            try
            {
                string exe = Aria.Ensure(null);
                Directory.CreateDirectory(dir);
                StringBuilder a = new StringBuilder();
                a.Append("--dir=\"").Append(dir.TrimEnd('\\')).Append("\" --continue=true --seed-time=").Append(SeedArgs()).Append(" --file-allocation=none --auto-file-renaming=true");
                a.Append(" --human-readable=false --summary-interval=1 --console-log-level=warn --download-result=full --enable-color=false");
                a.Append(" --bt-save-metadata=true --bt-max-peers=0 --max-connection-per-server=16 --split=16 --min-split-size=1M");
                long lim = Limit; if (lim > 0) a.Append(" --max-overall-download-limit=").Append(lim);
                a.Append(" \"").Append(link).Append("\"");
                ProcessStartInfo si = new ProcessStartInfo(exe, a.ToString());
                si.UseShellExecute = false; si.CreateNoWindow = true; si.RedirectStandardOutput = true; si.RedirectStandardError = true;
                si.StandardOutputEncoding = Encoding.UTF8; si.StandardErrorEncoding = Encoding.UTF8;
                Process p = Process.Start(si);
                proc = p;
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (!string.IsNullOrEmpty(e.Data)) lastErr = e.Data.Trim(); };
                p.BeginErrorReadLine();
                StringBuilder sb = new StringBuilder();
                Regex prog = new Regex(@"\[#\w+\s+([\d\.]+)[A-Za-z]*/([\d\.]+)[A-Za-z]*(?:\((\d+)%\))?(?:.*?DL:([\d\.]+))?");
                Regex ok = new Regex(@"\|OK\s*\|[^|]*\|(.+)$");
                int ch;
                while ((ch = p.StandardOutput.Read()) >= 0)
                {
                    if (ch != '\r' && ch != '\n') { sb.Append((char)ch); continue; }
                    string line = sb.ToString().Trim(); sb.Length = 0;
                    if (line.Length == 0) continue;
                    Match m = prog.Match(line);
                    if (m.Success)
                    {
                        long d = Num(m.Groups[1].Value), t = Num(m.Groups[2].Value);
                        Interlocked.Exchange(ref done, d); Interlocked.Exchange(ref total, t);
                        speed = m.Groups[4].Success ? Num(m.Groups[4].Value) : 0;
                        continue;
                    }
                    Match o = ok.Match(line);
                    if (o.Success) { string f = o.Groups[1].Value.Trim(); if (f.Length > 0 && !f.StartsWith("[METADATA]")) final = f; continue; }
                    if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("ERR", StringComparison.Ordinal) >= 0) lastErr = line;
                }
                p.WaitForExit();
                speed = 0;
                if (state == "pause" || state == "cancel") { }
                else if (p.ExitCode == 0) { Interlocked.Exchange(ref done, Total); state = "done"; }
                else { err = lastErr.Length > 0 ? lastErr : "aria2c stopped with code " + p.ExitCode; state = "error"; }
            }
            catch (Exception ex)
            {
                if (state == "pause" || state == "cancel") { }
                else { err = ex.Message; state = "error"; }
            }
            finally { speed = 0; finished = true; }
        }
    }
}
