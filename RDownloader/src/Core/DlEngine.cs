<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace RDownloader.Native
{
    public class RockDownload : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
        private bool _sel;
        public bool Selected { get { return _sel; } set { _sel = value; Raise("Selected"); } }
        private string _status = "Queued";
        public string Status { get { return _status; } set { _status = value; Raise("Status"); Raise("StatusLine"); if (value == "Complete" || value == "Completed" || value == "Finished") { Raise("FileIcon"); Raise("ListGlyph"); } } }
        private string _statusText = "";
        public string StatusText { get { return _statusText; } set { _statusText = value; Raise("StatusText"); Raise("StatusLine"); } }
        private string _lastTry = "";
        public string LastTry { get { return _lastTry; } set { _lastTry = value ?? ""; Raise("LastTry"); } }
        public string StatusLine { get { return (_status == "Downloading" && _pctText.Length > 0) ? _pctText : _statusText; } }
        private string _fileName = "";
        public string FileName { get { return _fileName; } set { _fileName = value; Raise("FileName"); Raise("Glyph"); Raise("GlyphColor"); Raise("FileIcon"); Raise("ListGlyph"); } }
        private string _sizeText = "-";
        public string SizeText { get { return _sizeText; } set { _sizeText = value; Raise("SizeText"); } }
        private double _progress;
        public double Progress { get { return _progress; } set { _progress = value; Raise("Progress"); } }
        private string _pctText = "0%";
        public string PctText { get { return _pctText; } set { _pctText = value; Raise("PctText"); Raise("StatusLine"); } }
        private string _speedText = "-";
        public string SpeedText { get { return _speedText; } set { _speedText = value; Raise("SpeedText"); Raise("SpeedEta"); } }
        public string SpeedEta { get { return _speedText + "   ·   " + _etaText; } }
        private string _progText = "";
        public string ProgText { get { return _progText; } set { _progText = value; Raise("ProgText"); } }
        public string Glyph { get { return DlGlyph.Glyph(_fileName, Url); } }
        public string GlyphColor { get { return DlGlyph.Color(_fileName, Url); } }
        public System.Windows.Media.ImageSource FileIcon { get { return FileIcons.Get(_fileName, FullPath); } }
        public string ListGlyph { get { return FileIcon == null ? Glyph : ""; } }
        public bool ShowWin;
        private string _etaText = "-";
        public string EtaText { get { return _etaText; } set { _etaText = value; Raise("EtaText"); Raise("SpeedEta"); } }
        public string Id { get; set; }
        public string Url { get; set; }
        public string Dir { get; set; }
        public string FullPath { get; set; }
        public long Total { get; set; }
        public long Done { get; set; }
        public int Retries { get; set; }
        public bool AutoResume { get; set; }
        public bool RunAfter { get; set; }
        public bool Verify { get; set; }
        public bool NameFromServer { get; set; }
        public string Seg { get; set; }
        public double SmDone, SmSpeed;      // smoothed values shown on screen (they glide to the real ones, so every number is passed)
        public bool SmInit;
        public string Added { get; set; }
    }
    public class RockDlRangeIgnored : Exception {
        public RockDlRangeIgnored(string m) : base(m) { }
    }

    // one running download: the HTTP engine below or the aria2 (torrent / magnet) job
    public interface IDlJob {
        long Done { get; }
        long Total { get; }
        double Speed { get; }
        string State { get; }
        bool Finished { get; }
        string Err { get; }
        string Final { get; }
        string Hash { get; }
        string SrvName { get; }
        long Limit { get; set; }
        void Begin();
        void Stop(string newState);
        string GetSegState();
        void SetResume(string st);
        long[][] SegInfo();
    }

    public class RockDlEngine : IDlJob {
        private class Seg {
            public long Start;
            public long End;
            public long Pos;
            public bool Done;
            public Exception Err;
            public HttpWebRequest Req;
        }

        private readonly string url;
        private readonly string path;
        private readonly string dir;
        private readonly bool verify;
        private readonly bool nameFromServer;
        private int conns;
        private long limit;
        private long done;
        private long total;
        private double speed;
        private volatile string state = "idle";
        private volatile bool finished;
        private volatile int activeSegs = 1;
        private string err = "";
        private string final = "";
        private string hash = "";
        private string srvName = "";
        private List<Seg> segs;
        private bool rangeOk;
        private bool forceNoRange;
        private HttpWebRequest firstReq;
        private HttpWebResponse firstResp;
        private readonly object lk = new object();
        private Thread runner;
        private List<Seg> resumeSegs;
        private long resumeTotal;
        private SemaphoreSlim gate;      // limits how many connections run at the same time (lowered when a server refuses many connections)
        private int par;

        static RockDlEngine() {
            try {
                ServicePointManager.DefaultConnectionLimit = 64;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.UseNagleAlgorithm = false;
            } catch (Exception) { }
        }

        public RockDlEngine(string url, string path, string dir, bool verify, bool nameFromServer, int conns, long limit) {
            this.url = url;
            this.path = path;
            this.dir = dir;
            this.verify = verify;
            this.nameFromServer = nameFromServer;
            this.conns = conns < 1 ? 1 : (conns > 32 ? 32 : conns);
            this.limit = limit;
        }

        public long Done { get { return Interlocked.Read(ref done); } }
        public long Total { get { return Interlocked.Read(ref total); } }
        public double Speed { get { return speed; } }
        public string State { get { return state; } }
        public bool Finished { get { return finished; } }
        public string Err { get { return err; } }
        public string Final { get { return final; } }
        public string Hash { get { return hash; } }
        public int Segments { get { List<Seg> l = segs; return l == null ? 0 : l.Count; } }
        public string SrvName { get { return srvName; } }

        // progress of every connection as text, saved by the app so a download can continue after the app was closed
        public string GetSegState() {
            List<Seg> l = segs;
            if (l == null || !rangeOk || Total <= 0) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(Total).Append('|');
            foreach (Seg s in l.ToArray()) sb.Append(s.Start).Append(':').Append(s.End).Append(':').Append(s.Pos).Append(':').Append(s.Done ? 1 : 0).Append(';');
            return sb.ToString();
        }

        // {start, end, position, done(0/1)} of every connection - used by the connections table of the progress window
        public long[][] SegInfo() {
            List<Seg> l = segs;
            if (l == null) return new long[0][];
            Seg[] a = l.ToArray();
            long[][] r = new long[a.Length][];
            for (int i = 0; i < a.Length; i++) r[i] = new long[] { a[i].Start, a[i].End, a[i].Pos, a[i].Done ? 1 : 0 };
            return r;
        }

        public void SetResume(string st) {
            try {
                if (string.IsNullOrEmpty(st)) return;
                string[] hp = st.Split('|');
                long tot = long.Parse(hp[0]);
                if (tot <= 0 || hp.Length < 2) return;
                List<Seg> l = new List<Seg>();
                foreach (string it in hp[1].Split(';')) {
                    if (it.Length == 0) continue;
                    string[] f = it.Split(':');
                    Seg g = new Seg();
                    g.Start = long.Parse(f[0]); g.End = long.Parse(f[1]); g.Pos = long.Parse(f[2]); g.Done = (f[3] == "1");
                    l.Add(g);
                }
                if (l.Count > 0) { resumeSegs = l; resumeTotal = tot; }
            } catch (Exception) { }
        }
        public long Limit {
            get { return Interlocked.Read(ref limit); }
            set { Interlocked.Exchange(ref limit, value); }
        }
        public int Conns {
            get { return conns; }
            set { conns = value < 1 ? 1 : (value > 32 ? 32 : value); }
        }

        public void Begin() {
            if (runner != null && runner.IsAlive) return;
            finished = false;
            err = "";
            speed = 0;
            state = "run";
            runner = new Thread(Run);
            runner.IsBackground = true;
            runner.Start();
        }

        public void Stop(string newState) {
            if (state != "run") return;
            state = newState;
            List<Seg> l = segs;
            if (l != null) {
                foreach (Seg g in l.ToArray()) {
                    HttpWebRequest q = g.Req;
                    if (q != null) { try { q.Abort(); } catch (Exception) { } }
                }
            }
            HttpWebRequest f = firstReq;
            if (f != null) { try { f.Abort(); } catch (Exception) { } }
        }

        private HttpWebRequest MakeReq(long from, long to) {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            DlOpts.Apply(r, url);
            r.AllowAutoRedirect = true;
            r.Timeout = 30000;
            r.ReadWriteTimeout = 30000;
            r.KeepAlive = true;
            r.AutomaticDecompression = DecompressionMethods.None;
            r.ServicePoint.ConnectionLimit = Math.Max(conns + 2, 8);
            r.ServicePoint.Expect100Continue = false;
            r.ServicePoint.UseNagleAlgorithm = false;
            try {
                System.Reflection.PropertyInfo pi = typeof(ServicePoint).GetProperty("ReceiveBufferSize");
                if (pi != null) pi.SetValue(r.ServicePoint, 4 * 1024 * 1024, null);
            } catch (Exception) { }
            if (to >= 0) r.AddRange(from, to);
            else if (from > 0) r.AddRange(from);
            return r;
        }

        private static readonly string[] NameKeys = new string[] { "file", "filename", "fname", "name", "title", "f", "download", "dl", "attachment", "out" };

        private static bool HasExt(string n) {
            int dot = n.LastIndexOf('.');
            return dot > 0 && n.Length - dot - 1 >= 1 && n.Length - dot - 1 <= 8 && Regex.IsMatch(n.Substring(dot + 1), "^[A-Za-z0-9]+$");
        }

        private static string NameFromQuery(Uri u) {
            try {
                string q = u.Query;
                if (string.IsNullOrEmpty(q)) return "";
                foreach (string pair in q.TrimStart('?').Split('&')) {
                    int i = pair.IndexOf('=');
                    if (i <= 0) continue;
                    string k = pair.Substring(0, i);
                    string v = pair.Substring(i + 1);
                    foreach (string nk in NameKeys) {
                        if (!string.Equals(k, nk, StringComparison.OrdinalIgnoreCase)) continue;
                        try { v = Uri.UnescapeDataString(v.Replace('+', ' ')); } catch (Exception) { }
                        v = v.Replace('\\', '/');
                        int sl = v.LastIndexOf('/');
                        if (sl >= 0) v = v.Substring(sl + 1);
                        v = v.Trim();
                        if (v.Length > 2 && HasExt(v)) return v;
                    }
                }
            } catch (Exception) { }
            return "";
        }

        private static string LastSeg(Uri u) {
            try {
                string[] s = u.Segments;
                if (s != null && s.Length > 0) return Uri.UnescapeDataString(s[s.Length - 1].Trim('/'));
            } catch (Exception) { }
            return "";
        }

        private static string ExtFromType(string ct) {
            if (ct == null) return "";
            ct = ct.ToLower();
            int sc = ct.IndexOf(';');
            if (sc >= 0) ct = ct.Substring(0, sc);
            ct = ct.Trim();
            switch (ct) {
                case "application/zip": case "application/x-zip-compressed": return ".zip";
                case "application/x-rar-compressed": case "application/vnd.rar": return ".rar";
                case "application/x-7z-compressed": return ".7z";
                case "application/pdf": return ".pdf";
                case "application/x-msdownload": case "application/vnd.microsoft.portable-executable": return ".exe";
                case "application/x-msi": return ".msi";
                case "application/x-iso9660-image": return ".iso";
                case "application/gzip": case "application/x-gzip": return ".gz";
                case "application/x-tar": return ".tar";
                case "video/mp4": return ".mp4";
                case "video/x-matroska": return ".mkv";
                case "audio/mpeg": return ".mp3";
                case "image/jpeg": return ".jpg";
                case "image/png": return ".png";
                case "image/webp": return ".webp";
                case "text/plain": return ".txt";
            }
            return "";
        }

        private void ReadName(HttpWebResponse resp) {
            if (!nameFromServer) return;
            srvName = GuessName(resp, url);
        }

        // the file name a server wants: Content-Disposition, then the link, then the content type
        public static string GuessName(HttpWebResponse resp, string url) {
            string n = "";
            string cd = resp.Headers["Content-Disposition"];
            if (cd != null) {
                Match m = Regex.Match(cd, "filename\\*\\s*=\\s*(?:UTF-8|utf-8)''([^;]+)");
                if (m.Success) {
                    try { n = Uri.UnescapeDataString(m.Groups[1].Value.Trim('"')); } catch (Exception) { }
                } else {
                    m = Regex.Match(cd, "filename\\s*=\\s*\"?([^\";]+)\"?");
                    if (m.Success) n = m.Groups[1].Value;
                }
            }
            if (n.Length == 0) n = NameFromQuery(resp.ResponseUri);
            if (n.Length == 0) { try { n = NameFromQuery(new Uri(url)); } catch (Exception) { } }
            if (n.Length == 0) { string t = LastSeg(resp.ResponseUri); if (HasExt(t)) n = t; }
            if (n.Length == 0) { try { string t2 = LastSeg(new Uri(url)); if (HasExt(t2)) n = t2; } catch (Exception) { } }
            if (n.Length == 0) n = LastSeg(resp.ResponseUri);
            if (n.Length > 0 && !HasExt(n)) n += ExtFromType(resp.ContentType);
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim().TrimEnd('.');
        }

        private void Init(string part) {
            Interlocked.Exchange(ref done, 0);
            Interlocked.Exchange(ref total, 0);
            rangeOk = false;
            firstResp = null;
            HttpWebRequest req = MakeReq(0, -1);
            firstReq = req;
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            long len = resp.ContentLength;
            string ar = resp.Headers["Accept-Ranges"];
            bool ranges = (ar != null && ar.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0) && !forceNoRange;
            ReadName(resp);
            Interlocked.Exchange(ref total, len > 0 ? len : 0);
            List<Seg> list = new List<Seg>();
            long tot = len > 0 ? len : 0;
            if (tot >= 4L * 1024 * 1024 && ranges && conns > 1) {
                try { req.Abort(); } catch (Exception) { }
                try { resp.Close(); } catch (Exception) { }
                firstReq = null;
                rangeOk = true;
                int n = conns;
                long minPart = 1024L * 1024;
                if (tot / n < minPart) n = (int)Math.Max(1, tot / minPart);
                long size = tot / n;
                for (int i = 0; i < n; i++) {
                    Seg s = new Seg();
                    s.Start = i * size;
                    s.End = (i == n - 1) ? tot - 1 : s.Start + size - 1;
                    s.Pos = s.Start;
                    list.Add(s);
                }
            } else {
                rangeOk = tot > 0;
                Seg s = new Seg();
                s.Start = 0;
                s.End = tot > 0 ? tot - 1 : -1;
                s.Pos = 0;
                list.Add(s);
                firstResp = resp;
            }
            using (FileStream fs = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
                if (tot > 0 && list.Count > 1) fs.SetLength(tot);
            }
            segs = list;
        }

        private Seg Steal(Seg me) {
            if (!rangeOk) return null;
            lock (lk) {
                Seg v = null; long best = 0;
                foreach (Seg g in segs) {
                    if (object.ReferenceEquals(g, me) || g.Done || g.End < 0) continue;
                    long rem = g.End - g.Pos + 1;
                    if (rem > best) { best = rem; v = g; }
                }
                if (v == null || best < 2L * 1024 * 1024) return null;
                long oldEnd = v.End;
                long mid = v.Pos + best / 2;
                v.End = mid;
                Seg ns = new Seg(); ns.Start = mid + 1; ns.End = oldEnd; ns.Pos = mid + 1;
                List<Seg> nl = new List<Seg>(segs); nl.Add(ns); segs = nl;
                return ns;
            }
        }

        private void SegWork(object o) {
            Seg s = (Seg)o;
            string part = path + ".part";
            byte[] buf = new byte[262144];
            int fails = 0;
            Stopwatch sw = Stopwatch.StartNew();
            long secStart = 0;
            long secBytes = 0;
            SemaphoreSlim myGate = gate;
            bool gated = false;
            try {
            if (myGate != null) {
                while (state == "run" && !gated) gated = myGate.Wait(200);
                if (!gated) return;
            }
            while (state == "run") {
            while (state == "run" && !s.Done) {
                HttpWebRequest req = null;
                HttpWebResponse r = null;
                Stream ins = null;
                FileStream fs = null;
                try {
                    lock (lk) {
                        if (firstResp != null && s.Pos == s.Start && segs.Count == 1) {
                            r = firstResp;
                            req = firstReq;
                            firstResp = null;
                        }
                    }
                    if (r == null) {
                        bool ranged = (s.End >= 0 || s.Pos > 0);
                        req = MakeReq(s.Pos, s.End);
                        s.Req = req;
                        r = (HttpWebResponse)req.GetResponse();
                        if (ranged && r.StatusCode != HttpStatusCode.PartialContent && (s.Pos > 0 || segs.Count > 1))
                            throw new RockDlRangeIgnored("The server does not support multi-connection downloads.");
                    } else {
                        s.Req = req;
                    }
                    ins = r.GetResponseStream();
                    fs = new FileStream(part, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                    fs.Seek(s.Pos, SeekOrigin.Begin);
                    while (state == "run") {
                        int want = buf.Length;
                        if (s.End >= 0) {
                            long rem = s.End - s.Pos + 1;
                            if (rem <= 0) break;
                            if (rem < want) want = (int)rem;
                        }
                        int n = ins.Read(buf, 0, want);
                        if (n <= 0) {
                            if (s.End < 0) s.Done = true;
                            break;
                        }
                        fs.Write(buf, 0, n);
                        s.Pos += n;
                        Interlocked.Add(ref done, n);
                        fails = 0;
                        long lim = Interlocked.Read(ref limit);
                        if (lim > 0) {
                            long per = lim / Math.Max(1, activeSegs);
                            secBytes += n;
                            long el = sw.ElapsedMilliseconds - secStart;
                            if (el >= 1000) { secStart = sw.ElapsedMilliseconds; secBytes = 0; }
                            else if (secBytes >= per) {
                                Thread.Sleep((int)(1000 - el));
                                secStart = sw.ElapsedMilliseconds;
                                secBytes = 0;
                            }
                        }
                    }
                    fs.Flush();
                    if (s.End >= 0 && s.Pos > s.End) s.Done = true;
                    if (!s.Done && state == "run") {
                        fails++;
                        if (fails > 8) throw new IOException("The connection closed before the file was complete.");
                    }
                }
                catch (RockDlRangeIgnored ex) {
                    s.Err = ex;
                    return;
                }
                catch (Exception ex) {
                    if (state != "run") return;
                    WebException we = ex as WebException;
                    HttpWebResponse er = we != null ? we.Response as HttpWebResponse : null;
                    if (er != null) {
                        int code = (int)er.StatusCode;
                        if (code >= 400 && code < 500 && code != 408 && code != 429) { s.Err = ex; return; }
                    }
                    fails++;
                    if (fails > 8) { s.Err = ex; return; }
                    Thread.Sleep(500 * fails);
                }
                finally {
                    try { if (req != null && !s.Done) req.Abort(); } catch (Exception) { }
                    try { if (fs != null) fs.Close(); } catch (Exception) { }
                    try { if (ins != null) ins.Close(); } catch (Exception) { }
                    try { if (r != null) r.Close(); } catch (Exception) { }
                }
            }
            // this part is finished: instead of going idle, take over the second half of the biggest part that is still running
            // (the slow tail of a download is fixed by sharing the last bytes between all connections)
            if (state != "run") break;
            Seg ns = Steal(s);
            if (ns == null) break;
            s = ns; fails = 0;
            }
            } finally {
                if (gated) { try { myGate.Release(); } catch (Exception) { } }
            }
        }

        private void RunSegments() {
            gate = new SemaphoreSlim(Math.Max(1, par));
            List<Thread> ts = new List<Thread>();
            foreach (Seg s in segs) {
                if (s.Done) continue;
                s.Err = null;
                Thread t = new Thread(SegWork);
                t.IsBackground = true;
                ts.Add(t);
                t.Start(s);
            }
            Stopwatch sw = Stopwatch.StartNew();
            long lastT = 0;
            long lastD = Interlocked.Read(ref done);
            while (true) {
                int act = 0;
                foreach (Thread t in ts) { if (t.IsAlive) act++; }
                activeSegs = act < 1 ? 1 : Math.Min(act, Math.Max(1, par));
                long now = sw.ElapsedMilliseconds;
                if (now - lastT >= 500) {
                    long d = Interlocked.Read(ref done);
                    speed = (d - lastD) * 1000.0 / (now - lastT);
                    lastT = now;
                    lastD = d;
                }
                if (act == 0) break;
                Thread.Sleep(100);
            }
            foreach (Seg s in segs) {
                if (s.Err is RockDlRangeIgnored) throw s.Err;
            }
            foreach (Seg s in segs) {
                if (s.Err != null && state == "run") {
                    err = s.Err.Message;
                    state = "fail";
                    return;
                }
            }
        }

        // A server that drops or refuses some connections must not stop the whole download: failed parts are tried again
        // (with fewer connections at the same time and a growing pause). Only a long run of rounds without any progress gives up.
        private void RunRounds() {
            par = Math.Max(1, segs.Count);
            int stalls = 0;
            while (true) {
                long before = Interlocked.Read(ref done);
                RunSegments();
                if (state != "fail") return;
                bool all = true;
                foreach (Seg s in segs) { if (!s.Done) all = false; }
                if (all) { state = "run"; return; }
                long made = Interlocked.Read(ref done) - before;
                if (made > 0) stalls = 0; else stalls++;
                if (stalls >= 8) return;                       // stays "fail": the page shows the error and retries later
                if (par > 2) par = Math.Max(2, par / 2); else if (par > 1 && stalls >= 2) par = 1;
                string lastErr = err;
                state = "run";                                 // so Pause / Cancel work while we wait
                int wait = Math.Min(8000, 1000 * (stalls + 1));
                for (int w = 0; w < wait && state == "run"; w += 100) Thread.Sleep(100);
                if (state != "run") return;
                foreach (Seg s in segs) { if (!s.Done) s.Err = null; }
                err = lastErr;
            }
        }

        private void Run() {
            string part = path + ".part";
            try {
                for (int attempt = 0; attempt < 2; attempt++) {
                    try {
                        if (segs == null && resumeSegs != null) {
                            // continue a download saved by an earlier run of the app
                            bool okPart = false;
                            try {
                                long need = resumeSegs.Count > 1 ? resumeTotal : 1;
                                okPart = File.Exists(part) && new FileInfo(part).Length >= need;
                            } catch (Exception) { }
                            if (okPart) {
                                segs = resumeSegs; rangeOk = true; firstResp = null; firstReq = null;
                                Interlocked.Exchange(ref total, resumeTotal);
                                long dd = 0;
                                foreach (Seg g in segs) dd += (g.Pos - g.Start);
                                Interlocked.Exchange(ref done, dd);
                            }
                            resumeSegs = null;
                        }
                        if (segs != null && (!rangeOk || Total <= 0)) segs = null;
                        if (segs == null) Init(part);
                        RunRounds();
                        break;
                    } catch (RockDlRangeIgnored) {
                        if (attempt == 1) throw;
                        forceNoRange = true;
                        conns = 1;
                        segs = null;
                        firstResp = null;
                        Interlocked.Exchange(ref done, 0);
                    }
                }
                if (state == "cancel") {
                    try { File.Delete(part); } catch (Exception) { }
                    segs = null;
                    Interlocked.Exchange(ref done, 0);
                } else if (state == "run") {
                    long tot = Total;
                    bool all = true;
                    foreach (Seg s in segs) { if (!s.Done) all = false; }
                    if (!all || (tot > 0 && Interlocked.Read(ref done) < tot))
                        throw new IOException("The connection closed before the file was complete.");
                    string fin = path;
                    if (srvName.Length > 0) fin = Path.Combine(dir, srvName);
                    if (File.Exists(fin)) {
                        string b = Path.GetFileNameWithoutExtension(fin);
                        string x = Path.GetExtension(fin);
                        string d = Path.GetDirectoryName(fin);
                        int i = 1;
                        while (File.Exists(fin)) { fin = Path.Combine(d, b + " (" + i + ")" + x); i++; }
                    }
                    File.Move(part, fin);
                    final = fin;
                    if (verify) {
                        using (SHA256 sha = SHA256.Create())
                        using (FileStream rs = File.OpenRead(fin)) {
                            hash = BitConverter.ToString(sha.ComputeHash(rs)).Replace("-", "").ToLower();
                        }
                    }
                    state = "done";
                }
            } catch (Exception ex) {
                err = ex.Message;
                if (state == "run") state = "fail";
            } finally {
                speed = 0;
                try { if (firstResp != null) firstResp.Close(); } catch (Exception) { }
                firstResp = null;
                finished = true;
            }
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace RDownloader.Native
{
    public class RockDownload : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
        private bool _sel;
        public bool Selected { get { return _sel; } set { _sel = value; Raise("Selected"); } }
        private string _status = "Queued";
        public string Status { get { return _status; } set { _status = value; Raise("Status"); Raise("StatusLine"); if (value == "Complete" || value == "Completed" || value == "Finished") { Raise("FileIcon"); Raise("ListGlyph"); } } }
        private string _statusText = "";
        public string StatusText { get { return _statusText; } set { _statusText = value; Raise("StatusText"); Raise("StatusLine"); } }
        private string _lastTry = "";
        public string LastTry { get { return _lastTry; } set { _lastTry = value ?? ""; Raise("LastTry"); } }
        public string StatusLine { get { return (_status == "Downloading" && _pctText.Length > 0) ? _pctText : _statusText; } }
        private string _fileName = "";
        public string FileName { get { return _fileName; } set { _fileName = value; Raise("FileName"); Raise("Glyph"); Raise("GlyphColor"); Raise("FileIcon"); Raise("ListGlyph"); } }
        private string _sizeText = "-";
        public string SizeText { get { return _sizeText; } set { _sizeText = value; Raise("SizeText"); } }
        private double _progress;
        public double Progress { get { return _progress; } set { _progress = value; Raise("Progress"); } }
        private string _pctText = "0%";
        public string PctText { get { return _pctText; } set { _pctText = value; Raise("PctText"); Raise("StatusLine"); } }
        private string _speedText = "-";
        public string SpeedText { get { return _speedText; } set { _speedText = value; Raise("SpeedText"); Raise("SpeedEta"); } }
        public string SpeedEta { get { return _speedText + "   ·   " + _etaText; } }
        private string _progText = "";
        public string ProgText { get { return _progText; } set { _progText = value; Raise("ProgText"); } }
        public string Glyph { get { return DlGlyph.Glyph(_fileName, Url); } }
        public string GlyphColor { get { return DlGlyph.Color(_fileName, Url); } }
        public System.Windows.Media.ImageSource FileIcon { get { return FileIcons.Get(_fileName, FullPath); } }
        public string ListGlyph { get { return FileIcon == null ? Glyph : ""; } }
        public bool ShowWin;
        private string _etaText = "-";
        public string EtaText { get { return _etaText; } set { _etaText = value; Raise("EtaText"); Raise("SpeedEta"); } }
        public string Id { get; set; }
        public string Url { get; set; }
        public string Dir { get; set; }
        public string FullPath { get; set; }
        public long Total { get; set; }
        public long Done { get; set; }
        public int Retries { get; set; }
        public bool AutoResume { get; set; }
        public bool RunAfter { get; set; }
        public bool Verify { get; set; }
        public bool NameFromServer { get; set; }
        public string Seg { get; set; }
        public double SmDone, SmSpeed;      // smoothed values shown on screen (they glide to the real ones, so every number is passed)
        public bool SmInit;
        public string Added { get; set; }
    }
    public class RockDlRangeIgnored : Exception {
        public RockDlRangeIgnored(string m) : base(m) { }
    }

    // one running download: the HTTP engine below or the aria2 (torrent / magnet) job
    public interface IDlJob {
        long Done { get; }
        long Total { get; }
        double Speed { get; }
        string State { get; }
        bool Finished { get; }
        string Err { get; }
        string Final { get; }
        string Hash { get; }
        string SrvName { get; }
        long Limit { get; set; }
        void Begin();
        void Stop(string newState);
        string GetSegState();
        void SetResume(string st);
        long[][] SegInfo();
    }

    public class RockDlEngine : IDlJob {
        private class Seg {
            public long Start;
            public long End;
            public long Pos;
            public bool Done;
            public Exception Err;
            public HttpWebRequest Req;
        }

        private readonly string url;
        private readonly string path;
        private readonly string dir;
        private readonly bool verify;
        private readonly bool nameFromServer;
        private int conns;
        private long limit;
        private long done;
        private long total;
        private double speed;
        private volatile string state = "idle";
        private volatile bool finished;
        private volatile int activeSegs = 1;
        private string err = "";
        private string final = "";
        private string hash = "";
        private string srvName = "";
        private List<Seg> segs;
        private bool rangeOk;
        private bool forceNoRange;
        private HttpWebRequest firstReq;
        private HttpWebResponse firstResp;
        private readonly object lk = new object();
        private Thread runner;
        private List<Seg> resumeSegs;
        private long resumeTotal;
        private SemaphoreSlim gate;      // limits how many connections run at the same time (lowered when a server refuses many connections)
        private int par;

        static RockDlEngine() {
            try {
                ServicePointManager.DefaultConnectionLimit = 64;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.UseNagleAlgorithm = false;
            } catch (Exception) { }
        }

        public RockDlEngine(string url, string path, string dir, bool verify, bool nameFromServer, int conns, long limit) {
            this.url = url;
            this.path = path;
            this.dir = dir;
            this.verify = verify;
            this.nameFromServer = nameFromServer;
            this.conns = conns < 1 ? 1 : (conns > 32 ? 32 : conns);
            this.limit = limit;
        }

        public long Done { get { return Interlocked.Read(ref done); } }
        public long Total { get { return Interlocked.Read(ref total); } }
        public double Speed { get { return speed; } }
        public string State { get { return state; } }
        public bool Finished { get { return finished; } }
        public string Err { get { return err; } }
        public string Final { get { return final; } }
        public string Hash { get { return hash; } }
        public int Segments { get { List<Seg> l = segs; return l == null ? 0 : l.Count; } }
        public string SrvName { get { return srvName; } }

        // progress of every connection as text, saved by the app so a download can continue after the app was closed
        public string GetSegState() {
            List<Seg> l = segs;
            if (l == null || !rangeOk || Total <= 0) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(Total).Append('|');
            foreach (Seg s in l.ToArray()) sb.Append(s.Start).Append(':').Append(s.End).Append(':').Append(s.Pos).Append(':').Append(s.Done ? 1 : 0).Append(';');
            return sb.ToString();
        }

        // {start, end, position, done(0/1)} of every connection - used by the connections table of the progress window
        public long[][] SegInfo() {
            List<Seg> l = segs;
            if (l == null) return new long[0][];
            Seg[] a = l.ToArray();
            long[][] r = new long[a.Length][];
            for (int i = 0; i < a.Length; i++) r[i] = new long[] { a[i].Start, a[i].End, a[i].Pos, a[i].Done ? 1 : 0 };
            return r;
        }

        public void SetResume(string st) {
            try {
                if (string.IsNullOrEmpty(st)) return;
                string[] hp = st.Split('|');
                long tot = long.Parse(hp[0]);
                if (tot <= 0 || hp.Length < 2) return;
                List<Seg> l = new List<Seg>();
                foreach (string it in hp[1].Split(';')) {
                    if (it.Length == 0) continue;
                    string[] f = it.Split(':');
                    Seg g = new Seg();
                    g.Start = long.Parse(f[0]); g.End = long.Parse(f[1]); g.Pos = long.Parse(f[2]); g.Done = (f[3] == "1");
                    l.Add(g);
                }
                if (l.Count > 0) { resumeSegs = l; resumeTotal = tot; }
            } catch (Exception) { }
        }
        public long Limit {
            get { return Interlocked.Read(ref limit); }
            set { Interlocked.Exchange(ref limit, value); }
        }
        public int Conns {
            get { return conns; }
            set { conns = value < 1 ? 1 : (value > 32 ? 32 : value); }
        }

        public void Begin() {
            if (runner != null && runner.IsAlive) return;
            finished = false;
            err = "";
            speed = 0;
            state = "run";
            runner = new Thread(Run);
            runner.IsBackground = true;
            runner.Start();
        }

        public void Stop(string newState) {
            if (state != "run") return;
            state = newState;
            List<Seg> l = segs;
            if (l != null) {
                foreach (Seg g in l.ToArray()) {
                    HttpWebRequest q = g.Req;
                    if (q != null) { try { q.Abort(); } catch (Exception) { } }
                }
            }
            HttpWebRequest f = firstReq;
            if (f != null) { try { f.Abort(); } catch (Exception) { } }
        }

        private HttpWebRequest MakeReq(long from, long to) {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            DlOpts.Apply(r, url);
            r.AllowAutoRedirect = true;
            r.Timeout = 30000;
            r.ReadWriteTimeout = 30000;
            r.KeepAlive = true;
            r.AutomaticDecompression = DecompressionMethods.None;
            r.ServicePoint.ConnectionLimit = Math.Max(conns + 2, 8);
            r.ServicePoint.Expect100Continue = false;
            r.ServicePoint.UseNagleAlgorithm = false;
            try {
                System.Reflection.PropertyInfo pi = typeof(ServicePoint).GetProperty("ReceiveBufferSize");
                if (pi != null) pi.SetValue(r.ServicePoint, 4 * 1024 * 1024, null);
            } catch (Exception) { }
            if (to >= 0) r.AddRange(from, to);
            else if (from > 0) r.AddRange(from);
            return r;
        }

        private static readonly string[] NameKeys = new string[] { "file", "filename", "fname", "name", "title", "f", "download", "dl", "attachment", "out" };

        private static bool HasExt(string n) {
            int dot = n.LastIndexOf('.');
            return dot > 0 && n.Length - dot - 1 >= 1 && n.Length - dot - 1 <= 8 && Regex.IsMatch(n.Substring(dot + 1), "^[A-Za-z0-9]+$");
        }

        private static string NameFromQuery(Uri u) {
            try {
                string q = u.Query;
                if (string.IsNullOrEmpty(q)) return "";
                foreach (string pair in q.TrimStart('?').Split('&')) {
                    int i = pair.IndexOf('=');
                    if (i <= 0) continue;
                    string k = pair.Substring(0, i);
                    string v = pair.Substring(i + 1);
                    foreach (string nk in NameKeys) {
                        if (!string.Equals(k, nk, StringComparison.OrdinalIgnoreCase)) continue;
                        try { v = Uri.UnescapeDataString(v.Replace('+', ' ')); } catch (Exception) { }
                        v = v.Replace('\\', '/');
                        int sl = v.LastIndexOf('/');
                        if (sl >= 0) v = v.Substring(sl + 1);
                        v = v.Trim();
                        if (v.Length > 2 && HasExt(v)) return v;
                    }
                }
            } catch (Exception) { }
            return "";
        }

        private static string LastSeg(Uri u) {
            try {
                string[] s = u.Segments;
                if (s != null && s.Length > 0) return Uri.UnescapeDataString(s[s.Length - 1].Trim('/'));
            } catch (Exception) { }
            return "";
        }

        private static string ExtFromType(string ct) {
            if (ct == null) return "";
            ct = ct.ToLower();
            int sc = ct.IndexOf(';');
            if (sc >= 0) ct = ct.Substring(0, sc);
            ct = ct.Trim();
            switch (ct) {
                case "application/zip": case "application/x-zip-compressed": return ".zip";
                case "application/x-rar-compressed": case "application/vnd.rar": return ".rar";
                case "application/x-7z-compressed": return ".7z";
                case "application/pdf": return ".pdf";
                case "application/x-msdownload": case "application/vnd.microsoft.portable-executable": return ".exe";
                case "application/x-msi": return ".msi";
                case "application/x-iso9660-image": return ".iso";
                case "application/gzip": case "application/x-gzip": return ".gz";
                case "application/x-tar": return ".tar";
                case "video/mp4": return ".mp4";
                case "video/x-matroska": return ".mkv";
                case "audio/mpeg": return ".mp3";
                case "image/jpeg": return ".jpg";
                case "image/png": return ".png";
                case "image/webp": return ".webp";
                case "text/plain": return ".txt";
            }
            return "";
        }

        private void ReadName(HttpWebResponse resp) {
            if (!nameFromServer) return;
            srvName = GuessName(resp, url);
        }

        // the file name a server wants: Content-Disposition, then the link, then the content type
        public static string GuessName(HttpWebResponse resp, string url) {
            string n = "";
            string cd = resp.Headers["Content-Disposition"];
            if (cd != null) {
                Match m = Regex.Match(cd, "filename\\*\\s*=\\s*(?:UTF-8|utf-8)''([^;]+)");
                if (m.Success) {
                    try { n = Uri.UnescapeDataString(m.Groups[1].Value.Trim('"')); } catch (Exception) { }
                } else {
                    m = Regex.Match(cd, "filename\\s*=\\s*\"?([^\";]+)\"?");
                    if (m.Success) n = m.Groups[1].Value;
                }
            }
            if (n.Length == 0) n = NameFromQuery(resp.ResponseUri);
            if (n.Length == 0) { try { n = NameFromQuery(new Uri(url)); } catch (Exception) { } }
            if (n.Length == 0) { string t = LastSeg(resp.ResponseUri); if (HasExt(t)) n = t; }
            if (n.Length == 0) { try { string t2 = LastSeg(new Uri(url)); if (HasExt(t2)) n = t2; } catch (Exception) { } }
            if (n.Length == 0) n = LastSeg(resp.ResponseUri);
            if (n.Length > 0 && !HasExt(n)) n += ExtFromType(resp.ContentType);
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim().TrimEnd('.');
        }

        private void Init(string part) {
            Interlocked.Exchange(ref done, 0);
            Interlocked.Exchange(ref total, 0);
            rangeOk = false;
            firstResp = null;
            HttpWebRequest req = MakeReq(0, -1);
            firstReq = req;
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            long len = resp.ContentLength;
            string ar = resp.Headers["Accept-Ranges"];
            bool ranges = (ar != null && ar.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0) && !forceNoRange;
            ReadName(resp);
            Interlocked.Exchange(ref total, len > 0 ? len : 0);
            List<Seg> list = new List<Seg>();
            long tot = len > 0 ? len : 0;
            if (tot >= 4L * 1024 * 1024 && ranges && conns > 1) {
                try { req.Abort(); } catch (Exception) { }
                try { resp.Close(); } catch (Exception) { }
                firstReq = null;
                rangeOk = true;
                int n = conns;
                long minPart = 1024L * 1024;
                if (tot / n < minPart) n = (int)Math.Max(1, tot / minPart);
                long size = tot / n;
                for (int i = 0; i < n; i++) {
                    Seg s = new Seg();
                    s.Start = i * size;
                    s.End = (i == n - 1) ? tot - 1 : s.Start + size - 1;
                    s.Pos = s.Start;
                    list.Add(s);
                }
            } else {
                rangeOk = tot > 0;
                Seg s = new Seg();
                s.Start = 0;
                s.End = tot > 0 ? tot - 1 : -1;
                s.Pos = 0;
                list.Add(s);
                firstResp = resp;
            }
            using (FileStream fs = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
                if (tot > 0 && list.Count > 1) fs.SetLength(tot);
            }
            segs = list;
        }

        private Seg Steal(Seg me) {
            if (!rangeOk) return null;
            lock (lk) {
                Seg v = null; long best = 0;
                foreach (Seg g in segs) {
                    if (object.ReferenceEquals(g, me) || g.Done || g.End < 0) continue;
                    long rem = g.End - g.Pos + 1;
                    if (rem > best) { best = rem; v = g; }
                }
                if (v == null || best < 2L * 1024 * 1024) return null;
                long oldEnd = v.End;
                long mid = v.Pos + best / 2;
                v.End = mid;
                Seg ns = new Seg(); ns.Start = mid + 1; ns.End = oldEnd; ns.Pos = mid + 1;
                List<Seg> nl = new List<Seg>(segs); nl.Add(ns); segs = nl;
                return ns;
            }
        }

        private void SegWork(object o) {
            Seg s = (Seg)o;
            string part = path + ".part";
            byte[] buf = new byte[262144];
            int fails = 0;
            Stopwatch sw = Stopwatch.StartNew();
            long secStart = 0;
            long secBytes = 0;
            SemaphoreSlim myGate = gate;
            bool gated = false;
            try {
            if (myGate != null) {
                while (state == "run" && !gated) gated = myGate.Wait(200);
                if (!gated) return;
            }
            while (state == "run") {
            while (state == "run" && !s.Done) {
                HttpWebRequest req = null;
                HttpWebResponse r = null;
                Stream ins = null;
                FileStream fs = null;
                try {
                    lock (lk) {
                        if (firstResp != null && s.Pos == s.Start && segs.Count == 1) {
                            r = firstResp;
                            req = firstReq;
                            firstResp = null;
                        }
                    }
                    if (r == null) {
                        bool ranged = (s.End >= 0 || s.Pos > 0);
                        req = MakeReq(s.Pos, s.End);
                        s.Req = req;
                        r = (HttpWebResponse)req.GetResponse();
                        if (ranged && r.StatusCode != HttpStatusCode.PartialContent && (s.Pos > 0 || segs.Count > 1))
                            throw new RockDlRangeIgnored("The server does not support multi-connection downloads.");
                    } else {
                        s.Req = req;
                    }
                    ins = r.GetResponseStream();
                    fs = new FileStream(part, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                    fs.Seek(s.Pos, SeekOrigin.Begin);
                    while (state == "run") {
                        int want = buf.Length;
                        if (s.End >= 0) {
                            long rem = s.End - s.Pos + 1;
                            if (rem <= 0) break;
                            if (rem < want) want = (int)rem;
                        }
                        int n = ins.Read(buf, 0, want);
                        if (n <= 0) {
                            if (s.End < 0) s.Done = true;
                            break;
                        }
                        fs.Write(buf, 0, n);
                        s.Pos += n;
                        Interlocked.Add(ref done, n);
                        fails = 0;
                        long lim = Interlocked.Read(ref limit);
                        if (lim > 0) {
                            long per = lim / Math.Max(1, activeSegs);
                            secBytes += n;
                            long el = sw.ElapsedMilliseconds - secStart;
                            if (el >= 1000) { secStart = sw.ElapsedMilliseconds; secBytes = 0; }
                            else if (secBytes >= per) {
                                Thread.Sleep((int)(1000 - el));
                                secStart = sw.ElapsedMilliseconds;
                                secBytes = 0;
                            }
                        }
                    }
                    fs.Flush();
                    if (s.End >= 0 && s.Pos > s.End) s.Done = true;
                    if (!s.Done && state == "run") {
                        fails++;
                        if (fails > 8) throw new IOException("The connection closed before the file was complete.");
                    }
                }
                catch (RockDlRangeIgnored ex) {
                    s.Err = ex;
                    return;
                }
                catch (Exception ex) {
                    if (state != "run") return;
                    WebException we = ex as WebException;
                    HttpWebResponse er = we != null ? we.Response as HttpWebResponse : null;
                    if (er != null) {
                        int code = (int)er.StatusCode;
                        if (code >= 400 && code < 500 && code != 408 && code != 429) { s.Err = ex; return; }
                    }
                    fails++;
                    if (fails > 8) { s.Err = ex; return; }
                    Thread.Sleep(500 * fails);
                }
                finally {
                    try { if (req != null && !s.Done) req.Abort(); } catch (Exception) { }
                    try { if (fs != null) fs.Close(); } catch (Exception) { }
                    try { if (ins != null) ins.Close(); } catch (Exception) { }
                    try { if (r != null) r.Close(); } catch (Exception) { }
                }
            }
            // this part is finished: instead of going idle, take over the second half of the biggest part that is still running
            // (the slow tail of a download is fixed by sharing the last bytes between all connections)
            if (state != "run") break;
            Seg ns = Steal(s);
            if (ns == null) break;
            s = ns; fails = 0;
            }
            } finally {
                if (gated) { try { myGate.Release(); } catch (Exception) { } }
            }
        }

        private void RunSegments() {
            gate = new SemaphoreSlim(Math.Max(1, par));
            List<Thread> ts = new List<Thread>();
            foreach (Seg s in segs) {
                if (s.Done) continue;
                s.Err = null;
                Thread t = new Thread(SegWork);
                t.IsBackground = true;
                ts.Add(t);
                t.Start(s);
            }
            Stopwatch sw = Stopwatch.StartNew();
            long lastT = 0;
            long lastD = Interlocked.Read(ref done);
            while (true) {
                int act = 0;
                foreach (Thread t in ts) { if (t.IsAlive) act++; }
                activeSegs = act < 1 ? 1 : Math.Min(act, Math.Max(1, par));
                long now = sw.ElapsedMilliseconds;
                if (now - lastT >= 500) {
                    long d = Interlocked.Read(ref done);
                    speed = (d - lastD) * 1000.0 / (now - lastT);
                    lastT = now;
                    lastD = d;
                }
                if (act == 0) break;
                Thread.Sleep(100);
            }
            foreach (Seg s in segs) {
                if (s.Err is RockDlRangeIgnored) throw s.Err;
            }
            foreach (Seg s in segs) {
                if (s.Err != null && state == "run") {
                    err = s.Err.Message;
                    state = "fail";
                    return;
                }
            }
        }

        // A server that drops or refuses some connections must not stop the whole download: failed parts are tried again
        // (with fewer connections at the same time and a growing pause). Only a long run of rounds without any progress gives up.
        private void RunRounds() {
            par = Math.Max(1, segs.Count);
            int stalls = 0;
            while (true) {
                long before = Interlocked.Read(ref done);
                RunSegments();
                if (state != "fail") return;
                bool all = true;
                foreach (Seg s in segs) { if (!s.Done) all = false; }
                if (all) { state = "run"; return; }
                long made = Interlocked.Read(ref done) - before;
                if (made > 0) stalls = 0; else stalls++;
                if (stalls >= 8) return;                       // stays "fail": the page shows the error and retries later
                if (par > 2) par = Math.Max(2, par / 2); else if (par > 1 && stalls >= 2) par = 1;
                string lastErr = err;
                state = "run";                                 // so Pause / Cancel work while we wait
                int wait = Math.Min(8000, 1000 * (stalls + 1));
                for (int w = 0; w < wait && state == "run"; w += 100) Thread.Sleep(100);
                if (state != "run") return;
                foreach (Seg s in segs) { if (!s.Done) s.Err = null; }
                err = lastErr;
            }
        }

        private void Run() {
            string part = path + ".part";
            try {
                for (int attempt = 0; attempt < 2; attempt++) {
                    try {
                        if (segs == null && resumeSegs != null) {
                            // continue a download saved by an earlier run of the app
                            bool okPart = false;
                            try {
                                long need = resumeSegs.Count > 1 ? resumeTotal : 1;
                                okPart = File.Exists(part) && new FileInfo(part).Length >= need;
                            } catch (Exception) { }
                            if (okPart) {
                                segs = resumeSegs; rangeOk = true; firstResp = null; firstReq = null;
                                Interlocked.Exchange(ref total, resumeTotal);
                                long dd = 0;
                                foreach (Seg g in segs) dd += (g.Pos - g.Start);
                                Interlocked.Exchange(ref done, dd);
                            }
                            resumeSegs = null;
                        }
                        if (segs != null && (!rangeOk || Total <= 0)) segs = null;
                        if (segs == null) Init(part);
                        RunRounds();
                        break;
                    } catch (RockDlRangeIgnored) {
                        if (attempt == 1) throw;
                        forceNoRange = true;
                        conns = 1;
                        segs = null;
                        firstResp = null;
                        Interlocked.Exchange(ref done, 0);
                    }
                }
                if (state == "cancel") {
                    try { File.Delete(part); } catch (Exception) { }
                    segs = null;
                    Interlocked.Exchange(ref done, 0);
                } else if (state == "run") {
                    long tot = Total;
                    bool all = true;
                    foreach (Seg s in segs) { if (!s.Done) all = false; }
                    if (!all || (tot > 0 && Interlocked.Read(ref done) < tot))
                        throw new IOException("The connection closed before the file was complete.");
                    string fin = path;
                    if (srvName.Length > 0) fin = Path.Combine(dir, srvName);
                    if (File.Exists(fin)) {
                        string b = Path.GetFileNameWithoutExtension(fin);
                        string x = Path.GetExtension(fin);
                        string d = Path.GetDirectoryName(fin);
                        int i = 1;
                        while (File.Exists(fin)) { fin = Path.Combine(d, b + " (" + i + ")" + x); i++; }
                    }
                    File.Move(part, fin);
                    final = fin;
                    if (verify) {
                        using (SHA256 sha = SHA256.Create())
                        using (FileStream rs = File.OpenRead(fin)) {
                            hash = BitConverter.ToString(sha.ComputeHash(rs)).Replace("-", "").ToLower();
                        }
                    }
                    state = "done";
                }
            } catch (Exception ex) {
                err = ex.Message;
                if (state == "run") state = "fail";
            } finally {
                speed = 0;
                try { if (firstResp != null) firstResp.Close(); } catch (Exception) { }
                firstResp = null;
                finished = true;
            }
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
