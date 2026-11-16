# ==============================================================================
# MODULE: src\pages\Downloads.ps1
# ==============================================================================
# Module: pages/Downloads - Download Manager page (queue, pause / resume, speed limit)
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

if (-not ('RockDownload' -as [type])) { Add-Type -TypeDefinition @'
using System.ComponentModel;
public class RockDownload : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private void Raise(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    private bool _sel;
    public bool Selected { get { return _sel; } set { _sel = value; Raise("Selected"); } }
    private string _status = "Queued";
    public string Status { get { return _status; } set { _status = value; Raise("Status"); } }
    private string _statusText = "";
    public string StatusText { get { return _statusText; } set { _statusText = value; Raise("StatusText"); } }
    private string _fileName = "";
    public string FileName { get { return _fileName; } set { _fileName = value; Raise("FileName"); } }
    private string _sizeText = "-";
    public string SizeText { get { return _sizeText; } set { _sizeText = value; Raise("SizeText"); } }
    private double _progress;
    public double Progress { get { return _progress; } set { _progress = value; Raise("Progress"); } }
    private string _pctText = "0%";
    public string PctText { get { return _pctText; } set { _pctText = value; Raise("PctText"); } }
    private string _speedText = "-";
    public string SpeedText { get { return _speedText; } set { _speedText = value; Raise("SpeedText"); } }
    private string _etaText = "-";
    public string EtaText { get { return _etaText; } set { _etaText = value; Raise("EtaText"); } }
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
    public string Added { get; set; }
}
'@ }

$script:DlItems   = New-Object 'System.Collections.Generic.List[RockDownload]'
$script:DlJobs    = @{}
$script:DlEngines = @{}
$script:DlTicking = $false
$script:DlLimits  = [ordered]@{ 'Unlimited' = 0; '256 KB/s' = 256; '512 KB/s' = 512; '1 MB/s' = 1024; '2 MB/s' = 2048; '5 MB/s' = 5120; '10 MB/s' = 10240; '20 MB/s' = 20480 }

# ---- the download engine (C#): several parallel connections per file, pause / resume, speed limit
if (-not ('RockDlEngine' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

public class RockDlRangeIgnored : Exception {
    public RockDlRangeIgnored(string m) : base(m) { }
}

public class RockDlEngine {
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
        r.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RockToolbox";
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
        srvName = n.Trim().TrimEnd('.');
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

    private void SegWork(object o) {
        Seg s = (Seg)o;
        string part = path + ".part";
        byte[] buf = new byte[262144];
        int fails = 0;
        Stopwatch sw = Stopwatch.StartNew();
        long secStart = 0;
        long secBytes = 0;
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
    }

    private void RunSegments() {
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
            activeSegs = act < 1 ? 1 : act;
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
                    RunSegments();
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
'@ }

# ---- small helpers
function Format-DlSize {
    param([double]$b)
    if ($b -ge 1GB) { return ('{0:0.0} GB' -f ($b / 1GB)) }
    if ($b -ge 1MB) { return ('{0:0.0} MB' -f ($b / 1MB)) }
    if ($b -ge 1KB) { return ('{0:0} KB' -f ($b / 1KB)) }
    return ('{0:0} B' -f $b)
}
function Format-DlEta {
    param([double]$Sec)
    if ($Sec -lt 0 -or $Sec -gt 8640000) { return '-' }
    $t = [TimeSpan]::FromSeconds([Math]::Round($Sec))
    if ($t.TotalHours -ge 1) { return ('{0}h {1:00}m' -f [int][Math]::Floor($t.TotalHours), $t.Minutes) }
    if ($t.TotalMinutes -ge 1) { return ('{0}m {1:00}s' -f $t.Minutes, $t.Seconds) }
    return ('{0}s' -f $t.Seconds)
}
function Set-DlStatus {
    param($It, [string]$Value)
    $It.Status = $Value
    $It.StatusText = L $Value
}
function Get-DlDir {
    $d = ([string]$script:Settings['DlDir']).Trim()
    if (-not $d) { $d = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\Rock' }
    return $d
}
function Get-DlLimitBytes {
    $k = $script:DlLimits[[string]$cmbDlLimit.SelectedItem]
    if ($k) { return ([int64]$k * 1024) }
    return [int64]0
}
function Get-DlConns {
    $c = 16
    try { $c = [int]$cmbDlConn.SelectedItem } catch { }
    if ($c -lt 1) { $c = 1 }
    return $c
}
function Get-DlSafeName {
    param([string]$Name)
    $n = [string]$Name
    foreach ($c in [IO.Path]::GetInvalidFileNameChars()) { $n = $n.Replace([string]$c, '_') }
    $n = $n.Trim().TrimEnd('.')
    if (-not $n) { $n = 'download' }
    if ($n.Length -gt 180) { $n = $n.Substring(0, 180) }
    return $n
}
function Get-DlNameFromUrl {
    param([Uri]$Uri)
    # many file hosts hide the real name in the query (...?file=My.App.v1.zip) - the path itself is just an id
    try {
        foreach ($pair in ($Uri.Query.TrimStart('?') -split '&')) {
            $i = $pair.IndexOf('=')
            if ($i -le 0) { continue }
            $k = $pair.Substring(0, $i).ToLower(); $v = $pair.Substring($i + 1)
            if ($k -in 'file', 'filename', 'fname', 'name', 'title', 'f', 'download', 'dl', 'attachment', 'out') {
                try { $v = [Uri]::UnescapeDataString($v.Replace('+', ' ')) } catch { }
                $v = ($v -replace '\\', '/'); $v = $v.Substring($v.LastIndexOf('/') + 1).Trim()
                if ($v.Length -gt 2 -and $v -match '\.[A-Za-z0-9]{1,8}$') { return $v }
            }
        }
    } catch { }
    try {
        $segs = $Uri.Segments
        $last = [Uri]::UnescapeDataString(([string]$segs[$segs.Count - 1]).Trim('/'))
        if ($last) { return $last }
    } catch { }
    return 'download'
}
function Get-DlUniquePath {
    param([string]$Dir, [string]$Name)
    $base = [IO.Path]::GetFileNameWithoutExtension($Name); $ext = [IO.Path]::GetExtension($Name)
    $p = Join-Path $Dir $Name; $i = 1
    while ((Test-Path -LiteralPath $p) -or (@($script:DlItems | Where-Object { $_.FullPath -eq $p }).Count -gt 0)) {
        $p = Join-Path $Dir ('{0} ({1}){2}' -f $base, $i, $ext); $i++
    }
    return $p
}

# ---- list helpers
function Update-DlCount {
    $src = @()
    if ($lvDownloads.ItemsSource) { $src = @($lvDownloads.ItemsSource) }
    $n = @($src | Where-Object { $_.Selected }).Count
    $txtDlCount.Text = "$(L 'Selected'): $n $(L 'of') $($src.Count)"
}
function Apply-DlFilter {
    $f = [string]$cmbDlFilter.SelectedItem
    $q = ([string]$txtDlSearch.Text).Trim()
    $list = New-Object 'System.Collections.Generic.List[RockDownload]'
    foreach ($it in $script:DlItems) {
        $st = [string]$it.Status
        if ($f -eq 'Active' -and ($st -ne 'Downloading' -and $st -ne 'Queued' -and $st -ne 'Paused')) { continue }
        if ($f -eq 'Completed' -and $st -ne 'Completed') { continue }
        if ($f -eq 'Failed' -and ($st -ne 'Failed' -and $st -ne 'Cancelled')) { continue }
        if ($q -and ($it.FileName -notlike "*$q*") -and ($it.Url -notlike "*$q*")) { continue }
        $list.Add($it)
    }
    $lvDownloads.ItemsSource = $list
    Update-DlCount
}
function Get-DlTargets {
    param([bool]$AllIfNone)
    $sel = @($script:DlItems | Where-Object { $_.Selected })
    if ($sel.Count -eq 0 -and $lvDownloads.SelectedItem) { $sel = @($lvDownloads.SelectedItem) }
    if ($sel.Count -eq 0 -and $AllIfNone -and $lvDownloads.ItemsSource) { $sel = @($lvDownloads.ItemsSource) }
    return $sel
}
function Update-DlRow {
    param($It, [double]$Speed)
    if ($It.Total -gt 0) {
        $p = [Math]::Min(100.0, $It.Done * 100.0 / $It.Total)
        $It.Progress = $p
        $It.PctText = ('{0:0}%' -f $p)
        $It.SizeText = Format-DlSize $It.Total
        if ($Speed -gt 1) { $It.EtaText = Format-DlEta (($It.Total - $It.Done) / $Speed) } else { $It.EtaText = '-' }
    } else {
        $It.PctText = ''
        $It.SizeText = Format-DlSize $It.Done
        $It.EtaText = '-'
    }
    if ($Speed -gt 1) { $It.SpeedText = (Format-DlSize $Speed) + '/s' } else { $It.SpeedText = '-' }
}

# ---- engine
function Start-DlJob {
    param($It)
    $eng = $script:DlEngines[$It.Id]
    if (-not $eng) {
        $eng = New-Object RockDlEngine([string]$It.Url, [string]$It.FullPath, [string]$It.Dir, [bool]$It.Verify, [bool]$It.NameFromServer, [int](Get-DlConns), [int64](Get-DlLimitBytes))
        if ($It.Seg) { $eng.SetResume([string]$It.Seg) }      # continue from the saved position (after the app was restarted)
        $script:DlEngines[$It.Id] = $eng
    } else {
        $eng.Limit = [int64](Get-DlLimitBytes)
    }
    $eng.Begin()
    $script:DlJobs[$It.Id] = @{ Eng = $eng }
    Set-DlStatus $It 'Downloading'
}

function Start-DlQueued {
    $max = 3
    try { $max = [int]$cmbDlMax.SelectedItem } catch { }
    if ($max -lt 1) { $max = 1 }
    $running = $script:DlJobs.Count
    foreach ($it in @($script:DlItems)) {
        if ($running -ge $max) { break }
        if ($it.Status -eq 'Queued' -and -not $script:DlJobs.ContainsKey($it.Id)) {
            try { Start-DlJob $it; $running++ }
            catch {
                Set-DlStatus $it 'Failed'
                Log "Could not start $($it.FileName): $($_.Exception.Message)" 'ERROR' 'Downloads'
            }
        }
    }
}

function Complete-DlJob {
    param($It, $Job)
    $eng = $Job.Eng
    $script:DlJobs.Remove($It.Id)
    $It.SpeedText = '-'; $It.EtaText = '-'
    switch ([string]$eng.State) {
        'done' {
            $script:DlEngines.Remove($It.Id)
            $final = [string]$eng.Final
            if (-not $final) { $final = $It.FullPath }
            $It.FullPath = $final
            $It.FileName = [IO.Path]::GetFileName($final)
            try { $It.SizeText = Format-DlSize ((Get-Item -LiteralPath $final).Length) } catch { }
            $It.Progress = 100.0; $It.PctText = '100%'
            Set-DlStatus $It 'Completed'
            Save-DlState
            Log "Download finished: $($It.FileName)" 'INFO' 'Downloads'
            if ($eng.Hash) { Log "SHA-256 of $($It.FileName): $($eng.Hash)" 'INFO' 'Downloads' }
            if ($It.RunAfter) {
                try { Start-Process -FilePath $final } catch { Log "Could not open $($It.FileName): $($_.Exception.Message)" 'WARN' 'Downloads' }
            }
        }
        'pause' { Set-DlStatus $It 'Paused'; Save-DlState }
        'cancel' {
            $script:DlEngines.Remove($It.Id)
            try { Remove-Item -LiteralPath ($It.FullPath + '.part') -Force -ErrorAction SilentlyContinue } catch { }
            $It.Done = 0; $It.Progress = 0.0; $It.PctText = '0%'; $It.SizeText = '-'
            Set-DlStatus $It 'Cancelled'
        }
        default {
            $err = [string]$eng.Err
            if ($It.AutoResume -and $It.Retries -lt 3 -and $err -notmatch '\(4\d\d\)') {
                $It.Retries++
                Set-DlStatus $It 'Queued'
                Log "Connection problem with $($It.FileName) - retrying ($($It.Retries) of 3)..." 'WARN' 'Downloads'
            } else {
                Set-DlStatus $It 'Failed'
                Log "Download failed: $($It.FileName) - $err" 'ERROR' 'Downloads'
            }
        }
    }
}

function Update-Downloads {
    if ($script:DlTicking) { return }
    $script:DlTicking = $true
    try {
        $speed = 0.0; $act = 0; $que = 0; $pau = 0
        foreach ($it in @($script:DlItems)) {
            $job = $script:DlJobs[$it.Id]
            if ($job) {
                $eng = $job.Eng
                if ($eng.Finished) { Complete-DlJob $it $job }
                else {
                    $it.Done = [int64]$eng.Done; $it.Total = [int64]$eng.Total
                    if ($it.NameFromServer) { $sn = [string]$eng.SrvName; if ($sn -and $it.FileName -ne $sn) { $it.FileName = $sn } }
                    $sp = [double]$eng.Speed
                    $speed += $sp; $act++
                    Update-DlRow $it $sp
                }
            }
            elseif ($it.Status -eq 'Queued') { $que++ }
            elseif ($it.Status -eq 'Paused') { $pau++ }
        }
        Start-DlQueued
        $script:DlSaveTick++
        if ($act -gt 0 -and ($script:DlSaveTick % 10) -eq 0) { Save-DlState }     # every ~5 s while downloading
        $txtDlInfo.Text = "$(L 'Total speed'): $(Format-DlSize $speed)/s   |   $act $(L 'active'), $que $(L 'queued'), $pau $(L 'paused')"
    }
    finally { $script:DlTicking = $false }
}

function Add-DlItems {
    param([string[]]$Urls, [string]$Dir, [string]$Name, [bool]$StartNow, [bool]$AutoResume, [bool]$RunAfter, [bool]$Verify)
    try { New-Item -ItemType Directory -Force -Path $Dir | Out-Null }
    catch { Log "Could not create the folder: $($_.Exception.Message)" 'ERROR' 'Downloads'; return }
    $list = @($Urls | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    $single = ($list.Count -eq 1)
    $added = 0
    foreach ($u in $list) {
        $uri = $null
        if (-not [Uri]::TryCreate($u, [UriKind]::Absolute, [ref]$uri) -or ($uri.Scheme -ne 'http' -and $uri.Scheme -ne 'https')) {
            Log "Skipped (not an http/https link): $u" 'WARN' 'Downloads'
            continue
        }
        $custom = ($single -and $Name -and $Name.Trim())
        if ($custom) { $nm = Get-DlSafeName $Name } else { $nm = Get-DlSafeName (Get-DlNameFromUrl $uri) }
        $it = New-Object RockDownload
        $it.Id = [guid]::NewGuid().ToString('N')
        $it.Url = $u
        $it.Dir = $Dir
        $it.FullPath = Get-DlUniquePath $Dir $nm
        $it.FileName = [IO.Path]::GetFileName($it.FullPath)
        $it.NameFromServer = ((-not $custom) -and ([IO.Path]::GetExtension($nm) -eq ''))
        $it.AutoResume = $AutoResume; $it.RunAfter = $RunAfter; $it.Verify = $Verify
        $it.Added = (Get-Date).ToString('o')
        try { Remove-Item -LiteralPath ($it.FullPath + '.part') -Force -ErrorAction SilentlyContinue } catch { }
        if ($StartNow) { Set-DlStatus $it 'Queued' } else { Set-DlStatus $it 'Paused' }
        $script:DlItems.Add($it)
        $added++
    }
    if ($added -gt 0) {
        Log "$added download(s) added." 'INFO' 'Downloads'
        Apply-DlFilter
        Update-Downloads
        Save-DlState
    }
}


# ---- the download list survives closing the app (finished files stay listed even if the file is gone)
$script:DlSaveTick = 0
function Get-DlStatePath {
    $root = Join-Path $env:LOCALAPPDATA 'RockToolbox'
    if (-not (Test-Path -LiteralPath $root)) { New-Item -ItemType Directory -Force -Path $root | Out-Null }
    return (Join-Path $root 'downloads.json')
}
function Save-DlState {
    try {
        $arr = New-Object System.Collections.ArrayList
        foreach ($it in $script:DlItems) {
            $seg = [string]$it.Seg
            $eng = $script:DlEngines[$it.Id]
            if ($eng) { try { $g = [string]$eng.GetSegState(); if ($g) { $seg = $g } } catch { } }
            if ([string]$it.Status -eq 'Completed') { $seg = '' }
            [void]$arr.Add([ordered]@{
                Id = $it.Id; Url = $it.Url; Dir = $it.Dir; FullPath = $it.FullPath; FileName = $it.FileName
                Total = [int64]$it.Total; Done = [int64]$it.Done; Status = [string]$it.Status; SizeText = $it.SizeText
                AutoResume = [bool]$it.AutoResume; RunAfter = [bool]$it.RunAfter; Verify = [bool]$it.Verify
                NameFromServer = [bool]$it.NameFromServer; Seg = $seg; Added = [string]$it.Added
            })
        }
        $json = ConvertTo-Json -InputObject @($arr) -Depth 4
        $path = Get-DlStatePath
        $tmp = $path + '.tmp'
        [IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $tmp -Destination $path -Force
    } catch { }
}
$script:DlSaveTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:DlSaveTimer.Interval = [TimeSpan]::FromMilliseconds(700)
$script:DlSaveTimer.Add_Tick({ $script:DlSaveTimer.Stop(); Save-DlState })
function Request-DlSave { $script:DlSaveTimer.Stop(); $script:DlSaveTimer.Start() }

function Load-DlState {
    $path = Get-DlStatePath
    if (-not (Test-Path -LiteralPath $path)) { return }
    try {
        $rows = @(Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json)
        foreach ($r in $rows) {
            if (-not $r.Url) { continue }
            $it = New-Object RockDownload
            $it.Id = [string]$r.Id; if (-not $it.Id) { $it.Id = [guid]::NewGuid().ToString('N') }
            $it.Url = [string]$r.Url; $it.Dir = [string]$r.Dir; $it.FullPath = [string]$r.FullPath
            $it.FileName = [string]$r.FileName
            $it.Total = [int64]$r.Total; $it.Done = [int64]$r.Done
            $it.AutoResume = [bool]$r.AutoResume; $it.RunAfter = $false; $it.Verify = [bool]$r.Verify
            $it.NameFromServer = [bool]$r.NameFromServer; $it.Seg = [string]$r.Seg; $it.Added = [string]$r.Added
            $it.SizeText = [string]$r.SizeText; if (-not $it.SizeText) { $it.SizeText = '-' }
            $st = [string]$r.Status
            if ($st -eq 'Downloading' -or $st -eq 'Queued') { $st = 'Paused' }          # it was stopped when the app closed
            if ($st -eq 'Completed') { $it.Progress = 100.0; $it.PctText = '100%' }
            else {
                $pct = 0.0
                if ($it.Total -gt 0) { $pct = [Math]::Min(100.0, $it.Done * 100.0 / $it.Total) }
                $it.Progress = $pct; $it.PctText = ('{0:0}%' -f $pct)
                if ($it.Total -gt 0) { $it.SizeText = Format-DlSize $it.Total }
                # the partial file must still exist to continue; otherwise start from 0 again
                if (($st -eq 'Paused' -or $st -eq 'Failed') -and -not (Test-Path -LiteralPath ($it.FullPath + '.part'))) {
                    $it.Done = 0; $it.Progress = 0.0; $it.PctText = '0%'; $it.Seg = ''
                }
            }
            Set-DlStatus $it $st
            $script:DlItems.Add($it)
        }
        if ($rows.Count -gt 0) { Log ("$($rows.Count) download(s) restored from the last session.") 'INFO' 'Downloads' }
    } catch { Log ("Could not read the saved download list: " + $_.Exception.Message) 'WARN' 'Downloads' }
}

# ---- actions
function Invoke-DlPause {
    foreach ($it in @(Get-DlTargets $true)) {
        $job = $script:DlJobs[$it.Id]
        if ($job) {
            if ($it.Status -eq 'Downloading') { $job.Eng.Stop('pause'); $it.StatusText = L 'Pausing...' }
        }
        elseif ($it.Status -eq 'Queued') { Set-DlStatus $it 'Paused' }
        $it.Selected = $false
    }
    Update-DlCount
    Request-DlSave
}
function Invoke-DlResume {
    foreach ($it in @(Get-DlTargets $true)) {
        if (-not $script:DlJobs.ContainsKey($it.Id) -and ($it.Status -eq 'Paused' -or $it.Status -eq 'Failed' -or $it.Status -eq 'Cancelled')) {
            $it.Retries = 0
            Set-DlStatus $it 'Queued'
        }
        $it.Selected = $false
    }
    Update-DlCount
    Update-Downloads
    Request-DlSave
}
function Invoke-DlCancel {
    $t = @(Get-DlTargets $false)
    if ($t.Count -eq 0) { Log 'Tick the downloads you want to cancel first.' 'WARN' 'Downloads'; return }
    foreach ($it in $t) {
        $job = $script:DlJobs[$it.Id]
        if ($job) { $job.Eng.Stop('cancel'); $it.StatusText = L 'Cancelling...' }
        elseif ($it.Status -eq 'Queued' -or $it.Status -eq 'Paused' -or $it.Status -eq 'Failed') {
            try { Remove-Item -LiteralPath ($it.FullPath + '.part') -Force -ErrorAction SilentlyContinue } catch { }
            $it.Done = 0; $it.Progress = 0.0; $it.PctText = '0%'; $it.SizeText = '-'
            Set-DlStatus $it 'Cancelled'
        }
        $it.Selected = $false
    }
    Update-DlCount
    Request-DlSave
}
function Invoke-DlClear {
    $gone = @($script:DlItems | Where-Object { $_.Status -eq 'Completed' -or $_.Status -eq 'Failed' -or $_.Status -eq 'Cancelled' })
    foreach ($it in $gone) { [void]$script:DlItems.Remove($it) }
    Apply-DlFilter
    Log "Cleared $($gone.Count) finished download(s)." 'INFO' 'Downloads'
    Request-DlSave
}
function Open-DlFolder {
    $it = $lvDownloads.SelectedItem
    try {
        if ($it -and (Test-Path -LiteralPath $it.FullPath)) { Start-Process explorer.exe -ArgumentList "/select,`"$($it.FullPath)`""; return }
        if ($it -and [string]$it.Status -eq 'Completed') {
            [void](Show-RockMessage -Text ((L 'File not found') + "`n" + (L 'This file is no longer on your PC. It may have been moved, renamed or deleted.') + "`n`n" + $it.FullPath) -Caption 'Rock Toolbox' -Buttons 'OK' -Icon 'Warning')
            return
        }
        $d = Get-DlDir
        if ($it -and (Test-Path -LiteralPath $it.Dir)) { $d = $it.Dir }
        New-Item -ItemType Directory -Force -Path $d | Out-Null
        Start-Process explorer.exe -ArgumentList "`"$d`""
    } catch { Log "Could not open the folder: $($_.Exception.Message)" 'WARN' 'Downloads' }
}

# ---- "Add download" window
function Show-DlAddDialog {
    $x = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Add download" Width="500" SizeToContent="Height" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" FontFamily="Segoe UI" FontSize="13">
  <Window.Resources>
    <Style x:Key="DlgBtn" TargetType="Button">
      <Setter Property="Foreground" Value="#F0F4FF"/>
      <Setter Property="Background" Value="#1E3547"/>
      <Setter Property="BorderBrush" Value="#35506A"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="3" Padding="22,7">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <StackPanel Margin="18">
    <TextBlock Text="Add download" FontSize="15" FontWeight="Bold" Foreground="White" Margin="0,0,0,12"/>
    <TextBlock Text="Links (one per line)" Foreground="#9AA0B8" Margin="0,0,0,4"/>
    <TextBox x:Name="dlUrls" Height="86" AcceptsReturn="True" TextWrapping="NoWrap" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
             Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" FontFamily="Consolas" Padding="4"/>
    <TextBlock Text="Save to:" Foreground="#9AA0B8" Margin="0,12,0,4"/>
    <DockPanel>
      <Button x:Name="dlBrowse" Content="Browse..." DockPanel.Dock="Right" Style="{StaticResource DlgBtn}" Margin="6,0,0,0"/>
      <TextBox x:Name="dlDir" Height="30" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" FontFamily="Consolas"/>
    </DockPanel>
    <TextBlock Text="File name (optional)" Foreground="#9AA0B8" Margin="0,12,0,4"/>
    <TextBox x:Name="dlName" Height="30" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"
             ToolTip="Only used when you add a single link"/>
    <StackPanel Margin="0,14,0,0">
      <CheckBox x:Name="dlStart" Content="Start downloading right away" Foreground="#E6E8F2" IsChecked="True" Margin="0,3"/>
      <CheckBox x:Name="dlResume" Content="Resume if the connection drops" Foreground="#E6E8F2" IsChecked="True" Margin="0,3"/>
      <CheckBox x:Name="dlRun" Content="Run the file when finished" Foreground="#E6E8F2" Margin="0,3"/>
      <CheckBox x:Name="dlVerify" Content="Calculate SHA-256 hash" Foreground="#E6E8F2" Margin="0,3"/>
    </StackPanel>
    <TextBlock x:Name="dlErr" Foreground="#FF6B6B" TextWrapping="Wrap" Visibility="Collapsed" Margin="0,10,0,0"/>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
      <Button x:Name="dlCancel" Content="Cancel" Style="{StaticResource DlgBtn}" IsCancel="True"/>
      <Button x:Name="dlOk" Content="Add to queue" Style="{StaticResource DlgBtn}" Background="#2F6FB0" Foreground="#FFFFFF" IsDefault="True" Margin="8,0,0,0"/>
    </StackPanel>
  </StackPanel>
</Window>
'@
    $x = Add-RockDialogChrome -Text $x -Title 'Add download'
    $x = Convert-XamlTheme -Text $x -Literal
    if (Test-Translated) { $x = Convert-XamlLang $x }
    $w = [Windows.Markup.XamlReader]::Parse($x)
    Initialize-RockDialogClose $w
    $script:DlDlg = @{ Win = $w; Ok = $false; Urls = @(); Dir = ''; Name = ''; Start = $true; Resume = $true; Run = $false; Verify = $false }
    $w.FindName('dlDir').Text = Get-DlDir
    try {
        $clip = [System.Windows.Clipboard]::GetText()
        if ($clip) {
            $found = @($clip -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^https?://\S+$' })
            if ($found.Count -gt 0) { $w.FindName('dlUrls').Text = ($found -join "`r`n") }
        }
    } catch { }
    if (Test-Rtl) { $w.FlowDirection = 'RightToLeft' }
    $w.FindName('dlBrowse').Add_Click({
        try {
            Add-Type -AssemblyName System.Windows.Forms
            $fd = New-Object System.Windows.Forms.FolderBrowserDialog
            $fd.SelectedPath = [string]$script:DlDlg.Win.FindName('dlDir').Text
            if ($fd.ShowDialog() -eq 'OK') { $script:DlDlg.Win.FindName('dlDir').Text = $fd.SelectedPath }
        } catch { }
    })
    $w.FindName('dlUrls').Add_TextChanged({ $script:DlDlg.Win.FindName('dlErr').Visibility = 'Collapsed' })
    $w.FindName('dlOk').Add_Click({
        $dw = $script:DlDlg.Win
        $urls = @(([string]$dw.FindName('dlUrls').Text) -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^https?://\S+$' })
        $dirText = ([string]$dw.FindName('dlDir').Text).Trim()
        $err = $dw.FindName('dlErr')
        if ($urls.Count -eq 0) { $err.Text = L 'Enter at least one link that starts with http:// or https://'; $err.Visibility = 'Visible'; return }
        if (-not $dirText) { $err.Text = L 'Choose a folder to save the files in.'; $err.Visibility = 'Visible'; return }
        $script:DlDlg.Urls = $urls
        $script:DlDlg.Dir = $dirText
        $script:DlDlg.Name = [string]$dw.FindName('dlName').Text
        $script:DlDlg.Start = [bool]$dw.FindName('dlStart').IsChecked
        $script:DlDlg.Resume = [bool]$dw.FindName('dlResume').IsChecked
        $script:DlDlg.Run = [bool]$dw.FindName('dlRun').IsChecked
        $script:DlDlg.Verify = [bool]$dw.FindName('dlVerify').IsChecked
        $script:DlDlg.Ok = $true
        $dw.Close()
    })
    $w.Add_Loaded({ try { [void]$script:DlDlg.Win.FindName('dlUrls').Focus() } catch { } })
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    [void]$w.ShowDialog()
    $r = $script:DlDlg
    $script:DlDlg = $null
    if ($r.Ok) { Add-DlItems -Urls $r.Urls -Dir $r.Dir -Name $r.Name -StartNow $r.Start -AutoResume $r.Resume -RunAfter $r.Run -Verify $r.Verify }
}

# ---- wire up the page
foreach ($dlT in 'All downloads', 'Active', 'Completed', 'Failed') { [void]$cmbDlFilter.Items.Add($dlT) }
$cmbDlFilter.SelectedIndex = 0
foreach ($dlN in 1..8) { [void]$cmbDlMax.Items.Add([int]$dlN) }
$dlMaxSaved = 3
try { $dlMaxSaved = [int]$script:Settings['DlMax'] } catch { }
if ($dlMaxSaved -lt 1 -or $dlMaxSaved -gt 8) { $dlMaxSaved = 3 }
$cmbDlMax.SelectedItem = [int]$dlMaxSaved
foreach ($dlK in $script:DlLimits.Keys) { [void]$cmbDlLimit.Items.Add([string]$dlK) }
$dlLimSaved = [string]$script:Settings['DlLimit']
if (-not $script:DlLimits.Contains($dlLimSaved)) { $dlLimSaved = 'Unlimited' }
$cmbDlLimit.SelectedItem = $dlLimSaved
foreach ($dlC in 1, 2, 4, 6, 8, 12, 16, 24, 32) { [void]$cmbDlConn.Items.Add([int]$dlC) }
$dlConnSaved = 16
try { $dlConnSaved = [int]$script:Settings['DlConn'] } catch { }
if (-not $cmbDlConn.Items.Contains([int]$dlConnSaved)) { $dlConnSaved = 16 }
$cmbDlConn.SelectedItem = [int]$dlConnSaved
$txtDlDir.Text = Get-DlDir
$txtDlInfo.Text = L 'Add a link to start downloading.'

$cmbDlFilter.Add_SelectionChanged({ Apply-DlFilter })
Register-Debounce $txtDlSearch { Apply-DlFilter }
$cmbDlMax.Add_SelectionChanged({
    if ($cmbDlMax.SelectedItem) { $script:Settings['DlMax'] = [int]$cmbDlMax.SelectedItem; Save-Settings }
    Update-Downloads
})
$cmbDlLimit.Add_SelectionChanged({
    if ($cmbDlLimit.SelectedItem) { $script:Settings['DlLimit'] = [string]$cmbDlLimit.SelectedItem; Save-Settings }
    $lim = Get-DlLimitBytes
    foreach ($j in @($script:DlJobs.Values)) { try { $j.Eng.Limit = [int64]$lim } catch { } }
})
$cmbDlConn.Add_SelectionChanged({
    if ($cmbDlConn.SelectedItem) { $script:Settings['DlConn'] = [int]$cmbDlConn.SelectedItem; Save-Settings }
})
$txtDlDir.Add_LostFocus({
    $script:Settings['DlDir'] = ([string]$txtDlDir.Text).Trim()
    Save-Settings
})
$btnDlBrowse.Add_Click({
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $fd = New-Object System.Windows.Forms.FolderBrowserDialog
        $fd.SelectedPath = Get-DlDir
        if ($fd.ShowDialog() -eq 'OK') {
            $txtDlDir.Text = $fd.SelectedPath
            $script:Settings['DlDir'] = $fd.SelectedPath
            Save-Settings
        }
    } catch { Log "Could not open the folder picker: $($_.Exception.Message)" 'WARN' 'Downloads' }
})
$btnDlAdd.Add_Click({ Show-DlAddDialog })
$btnDlPause.Add_Click({ Invoke-DlPause })
$btnDlResume.Add_Click({ Invoke-DlResume })
$btnDlCancel.Add_Click({ Invoke-DlCancel })
$btnDlClear.Add_Click({ Invoke-DlClear })
$btnDlOpen.Add_Click({ Open-DlFolder })
$lvDownloads.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{
    param($sender, $e)
    $src = $e.OriginalSource
    if ($src -is [System.Windows.Controls.Button] -and $src.Name -eq 'dlCopy') {
        try {
            [System.Windows.Clipboard]::SetText([string]$src.Tag)
            Log ('Link copied: ' + [string]$src.Tag) 'INFO' 'Downloads'
            $src.Content = (L 'Copied')
            $t = New-Object System.Windows.Threading.DispatcherTimer
            $t.Interval = [TimeSpan]::FromMilliseconds(1200)
            $t.Tag = $src
            $t.Add_Tick({ param($ts, $te) $ts.Stop(); try { $ts.Tag.Content = (L 'Copy') } catch { } })
            $t.Start()
        } catch { }
        return
    }
    Update-DlCount
})
$btnDlSettings.Add_Click({
    if ($brDlSave.Visibility -eq 'Visible') { $brDlSave.Visibility = 'Collapsed' }
    else { $brDlSave.Visibility = 'Visible'; try { Initialize-CardAnim $brDlSave; Start-CardFade $brDlSave 0 } catch { } }
})
$lvDownloads.Add_MouseDoubleClick({
    $it = $lvDownloads.SelectedItem
    if ($it -and $it.Status -eq 'Completed' -and (Test-Path -LiteralPath $it.FullPath)) {
        try { Start-Process -FilePath $it.FullPath } catch { Log "Could not open the file: $($_.Exception.Message)" 'WARN' 'Downloads' }
    }
})

$script:DlTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:DlTimer.Interval = [TimeSpan]::FromMilliseconds(500)
$script:DlTimer.Add_Tick({ try { Update-Downloads } catch { } })
$script:DlTimer.Start()
Load-DlState
Apply-DlFilter
# closing the app: pause what is running so it can continue next time, then save the list
$window.Add_Closing({
    try {
        foreach ($j in @($script:DlJobs.Values)) { try { $j.Eng.Stop('pause') } catch { } }
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($sw.ElapsedMilliseconds -lt 1500) {
            $alive = 0
            foreach ($j in @($script:DlJobs.Values)) { if (-not $j.Eng.Finished) { $alive++ } }
            if ($alive -eq 0) { break }
            Start-Sleep -Milliseconds 30
        }
        Save-DlState
    } catch { }
})

