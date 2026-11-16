using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace RockToolbox.Native
{
    // one real hardware device that has a vendor driver installed (read from WMI Win32_PnPSignedDriver)
    internal sealed class HwDriver
    {
        public string Name = "", Cls = "", Category = "Other", Maker = "", VersionText = "", Inf = "", DeviceId = "";
        public Version Ver;                 // parsed installed version (null = cannot be compared)
        public DateTime Date = DateTime.MinValue;
        public List<string> Ids = new List<string>();      // the hardware ids that are searched in the catalog (shortest first)
        public string Key = "";             // first id = cache / dedupe key ("" = nothing to search)
    }

    // one driver row of the Microsoft Update Catalog
    internal sealed class CatalogHit
    {
        public string Title = "", Products = "", Classification = "", VersionText = "", UpdateId = "";
        public Version Ver;
        public DateTime Date = DateTime.MinValue;
    }

    // Finds the latest driver of a device in the Microsoft Update Catalog website (by hardware id) and installs it from the CAB.
    // This is deliberately NOT the Windows Update API, so the result is different from the Windows Update page.
    internal static class DriverCatalog
    {
        private const string Host = "https://www.catalog.update.microsoft.com/";
        private const int CacheHours = 6;
        private static readonly object CacheLock = new object();
        private static Dictionary<string, object> _cache;

        static DriverCatalog()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192; } catch { }
            try { ServicePointManager.Expect100Continue = false; ServicePointManager.DefaultConnectionLimit = 32; } catch { }
        }

        // ---------------- small helpers ----------------
        public static string DateText(DateTime d)
        {
            return d == DateTime.MinValue ? "" : d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // "27.20.100.9168" / "1.2" / junk -> Version (always 4 parts so versions compare numerically); null when there is no number
        public static Version ParseVer(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            Match m = Regex.Match(s, @"\d+(\.\d+){1,3}");
            if (!m.Success) return null;
            string[] p = m.Value.Split('.');
            int[] n = new int[4];
            for (int i = 0; i < p.Length && i < 4; i++)
            {
                int v;
                if (!int.TryParse(p[i], NumberStyles.None, CultureInfo.InvariantCulture, out v)) return null;
                n[i] = v;
            }
            return new Version(n[0], n[1], n[2], n[3]);
        }

        private static DateTime ParseDate(string s)
        {
            DateTime d;
            s = (s ?? "").Trim();
            if (s.Length == 0) return DateTime.MinValue;
            if (DateTime.TryParseExact(s, new string[] { "M/d/yyyy", "MM/dd/yyyy", "M/d/yyyy h:mm:ss tt", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            if (DateTime.TryParse(s, out d)) return d;
            return DateTime.MinValue;
        }

        private static string Str(ManagementBaseObject m, string prop)
        {
            try { object o = m[prop]; return o == null ? "" : Convert.ToString(o); }
            catch { return ""; }
        }

        public static string Category(string cls, string name)
        {
            string c = (cls ?? "").ToLowerInvariant();
            switch (c)
            {
                case "display": return "Display";
                case "net": return "Network";
                case "media": case "audioendpoint": return "Audio";
                case "bluetooth": return "Bluetooth";
                case "usb": return "USB";
                case "scsiadapter": case "hdc": case "diskdrive": case "storagecontroller": return "Storage";
                case "camera": case "image": return "Camera";
                case "hidclass": case "keyboard": case "mouse": return "Input";
                case "system": case "processor": case "firmware": return "System";
                case "biometric": return "Biometric";
                case "sensor": return "Sensors";
                case "battery": return "Power";
                case "monitor": return "Monitor";
            }
            string t = (name ?? "").ToLowerInvariant();
            if (Regex.IsMatch(t, "audio|sound|speaker|microphone|realtek.*hd")) return "Audio";
            if (Regex.IsMatch(t, "graphics|display|geforce|radeon|nvidia|uhd|iris")) return "Display";
            if (Regex.IsMatch(t, "wi-?fi|wireless|wlan|ethernet|network|gigabit|lan ")) return "Network";
            if (t.Contains("bluetooth")) return "Bluetooth";
            if (Regex.IsMatch(t, "camera|webcam")) return "Camera";
            if (Regex.IsMatch(t, "nvme|sata|raid|storage")) return "Storage";
            if (t.Contains("usb")) return "USB";
            if (Regex.IsMatch(t, "touchpad|mouse|keyboard")) return "Input";
            if (Regex.IsMatch(t, "chipset|management engine|serial io|gpio|thermal|platform")) return "System";
            return "Other";
        }

        // ---------------- 1) scan the hardware ----------------
        public static List<HwDriver> ScanInstalled()
        {
            List<HwDriver> list = new List<HwDriver>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT DeviceName, DeviceClass, Manufacturer, DriverProviderName, DriverVersion, DriverDate, HardWareID, CompatID, InfName, DeviceID FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL"))
            using (ManagementObjectCollection col = s.Get())
            {
                foreach (ManagementObject m in col)
                {
                    try
                    {
                        HwDriver d = MakeDriver(m);
                        if (d != null && !seen.ContainsKey(d.DeviceId)) { seen[d.DeviceId] = true; list.Add(d); }
                    }
                    catch { }
                }
            }
            return list;
        }

        private static HwDriver MakeDriver(ManagementObject m)
        {
            string name = Str(m, "DeviceName").Trim();
            if (name.Length == 0) return null;
            string id = Str(m, "DeviceID").Trim();
            string up = id.ToUpperInvariant();
            // real hardware buses only (no ROOT\ / SWD\ / software and virtual devices)
            if (!(up.StartsWith("PCI\\") || up.StartsWith("USB\\") || up.StartsWith("HDAUDIO\\") || up.StartsWith("ACPI\\") || up.StartsWith("HID\\") || up.StartsWith("SD\\"))) return null;
            string cls = Str(m, "DeviceClass");
            string lc = cls.ToLowerInvariant();
            if (lc == "softwaredevice" || lc == "volume" || lc == "volumesnapshot" || lc == "computer" || lc == "legacydriver" || lc == "printer" || lc == "printqueue" || lc == "wsdprintdevice" || lc == "softwarecomponent") return null;
            string maker = Str(m, "Manufacturer").Trim();
            if (maker.Length == 0) maker = Str(m, "DriverProviderName").Trim();
            string lm = maker.ToLowerInvariant();
            if (lm.Contains("microsoft") || lm.Contains("(standard")) return null;
            string inf = Str(m, "InfName").Trim();
            bool oem = Regex.IsMatch(inf, @"^oem\d+\.inf$", RegexOptions.IgnoreCase);
            string ver = Str(m, "DriverVersion").Trim();
            if (!oem)
            {
                // an inbox driver of Windows itself: nothing to update from a vendor
                if (maker.Length == 0 || ver.StartsWith("10.0.")) return null;
                if (Regex.IsMatch(name, "generic|virtual|standard |remote desktop|root hub|composite|miniport|hid-compliant|system |software device|acpi ", RegexOptions.IgnoreCase)) return null;
            }
            string[] ids = m["HardWareID"] as string[];
            if (ids == null || ids.Length == 0) return null;

            HwDriver d = new HwDriver();
            d.Name = name; d.Cls = cls; d.Maker = maker; d.Inf = inf; d.DeviceId = id;
            d.VersionText = ver; d.Ver = ParseVer(ver);
            try { d.Date = ManagementDateTimeConverter.ToDateTime(Str(m, "DriverDate")); } catch { d.Date = DateTime.MinValue; }
            if (d.Date.Year < 1990) d.Date = DateTime.MinValue;
            d.Category = Category(cls, name);
            d.Ids = PickIds(ids);
            d.Key = d.Ids.Count > 0 ? d.Ids[0] : "";
            return d;
        }

        // the 2-3 most useful hardware ids, shortest first (the vendor-only and class-code ids are too generic to search)
        private static List<string> PickIds(string[] ids)
        {
            List<string> r = new List<string>();
            foreach (string raw in ids)
            {
                string x = (raw ?? "").Trim().ToUpperInvariant();
                if (x.Length < 8 || r.Contains(x)) continue;
                if (Regex.IsMatch(x, @"^[A-Z0-9_]+\\(VEN|VID)_[0-9A-F]{4}$")) continue;
                if (x.Contains("&CC_") || x.Contains("MS_COMP") || x.StartsWith("*")) continue;
                r.Add(x);
            }
            r.Sort(delegate(string a, string b)
            {
                int c = a.Length.CompareTo(b.Length);
                return c != 0 ? c : string.CompareOrdinal(a, b);
            });
            if (r.Count > 3) r.RemoveRange(3, r.Count - 3);
            return r;
        }

        // ---------------- 2) the catalog website ----------------
        private static HttpWebRequest NewReq(string url, int timeoutMs)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RockToolbox";
            r.Accept = "text/html,*/*";
            r.Headers["Accept-Language"] = "en-US,en;q=0.8";
            r.Timeout = timeoutMs; r.ReadWriteTimeout = timeoutMs;
            r.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            try { r.Proxy = WebRequest.GetSystemWebProxy(); r.Proxy.Credentials = CredentialCache.DefaultCredentials; } catch { }
            return r;
        }

        private static string ReadAll(HttpWebRequest r)
        {
            using (WebResponse resp = r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private static string Clean(string html)
        {
            string t = Regex.Replace(html ?? "", "<[^>]+>", " ");
            t = WebUtility.HtmlDecode(t);
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        // title / products must fit this PC: Windows 10 / 11 x64, or no OS qualifier at all
        private static bool OsOk(string text)
        {
            bool has1011 = Regex.IsMatch(text, @"Windows\s*(10|11)", RegexOptions.IgnoreCase);
            bool anyWin = Regex.IsMatch(text, @"Windows\s*(\d|Vista|XP|Server)", RegexOptions.IgnoreCase);
            if (anyWin && !has1011) return false;
            bool x64 = Regex.IsMatch(text, @"x64|amd64|64-bit", RegexOptions.IgnoreCase);
            if (Regex.IsMatch(text, @"arm64|arm-based", RegexOptions.IgnoreCase) && !x64) return false;
            if (Regex.IsMatch(text, @"\bx86\b|32-bit", RegexOptions.IgnoreCase) && !x64) return false;
            return true;
        }

        public static List<CatalogHit> Search(string query, int timeoutMs)
        {
            string html = ReadAll(NewReq(Host + "Search.aspx?q=" + Uri.EscapeDataString(query), timeoutMs));
            List<CatalogHit> hits = new List<CatalogHit>();
            foreach (Match rm in Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string row = rm.Groups[1].Value;
                Match g = Regex.Match(row, @"goToDetails\(\s*[""']([0-9a-fA-F\-]{36})[""']\s*\)");
                if (!g.Success) continue;
                MatchCollection tds = Regex.Matches(row, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                int ti = -1;
                for (int i = 0; i < tds.Count; i++) if (tds[i].Value.IndexOf("goToDetails", StringComparison.Ordinal) >= 0) { ti = i; break; }
                if (ti < 0 || ti + 4 >= tds.Count) continue;
                CatalogHit h = new CatalogHit();
                h.UpdateId = g.Groups[1].Value;
                h.Title = Clean(tds[ti].Groups[1].Value);
                h.Products = Clean(tds[ti + 1].Groups[1].Value);
                h.Classification = Clean(tds[ti + 2].Groups[1].Value);
                h.Date = ParseDate(Clean(tds[ti + 3].Groups[1].Value));
                h.VersionText = Clean(tds[ti + 4].Groups[1].Value);
                h.Ver = ParseVer(h.VersionText);
                hits.Add(h);
            }
            return hits;
        }

        private static bool Better(CatalogHit a, CatalogHit b)
        {
            int c = a.Ver.CompareTo(b.Ver);
            if (c != 0) return c > 0;
            return a.Date > b.Date;
        }

        // highest-version driver row for the ids (tried one after the other until one gives a result); null = nothing found. Throws on network errors.
        public static CatalogHit FindLatest(List<string> ids, int timeoutMs)
        {
            CatalogHit best = null;
            foreach (string id in ids)
            {
                foreach (CatalogHit h in Search(id, timeoutMs))
                {
                    if (h.Ver == null) continue;
                    if (h.Classification.IndexOf("Driver", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!OsOk(h.Title + " " + h.Products)) continue;
                    if (best == null || Better(h, best)) best = h;
                }
                if (best != null) break;
            }
            return best;
        }

        // ---------------- disk cache (6 hours) ----------------
        private static string CachePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RockToolbox\driver-cache.json");
        }

        private static void LoadCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, object>();
            try
            {
                string p = CachePath();
                if (!File.Exists(p)) return;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> d = ser.DeserializeObject(File.ReadAllText(p, Encoding.UTF8)) as Dictionary<string, object>;
                if (d != null) _cache = d;
            }
            catch { _cache = new Dictionary<string, object>(); }
        }

        private static string E(Dictionary<string, object> e, string k)
        {
            object o;
            return e.TryGetValue(k, out o) && o != null ? Convert.ToString(o) : "";
        }

        private static bool Fresh(Dictionary<string, object> e, double hours)
        {
            long ticks;
            if (!long.TryParse(E(e, "t"), out ticks)) return false;
            try
            {
                double age = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalHours;
                return age >= -1 && age < hours;
            }
            catch { return false; }
        }

        // true = a fresh cache entry exists (hit may be null = "nothing found" was cached)
        private static bool CacheGet(string key, out CatalogHit hit)
        {
            hit = null;
            lock (CacheLock)
            {
                LoadCache();
                object o;
                if (!_cache.TryGetValue(key, out o)) return false;
                Dictionary<string, object> e = o as Dictionary<string, object>;
                if (e == null || !Fresh(e, CacheHours)) return false;
                if (E(e, "found") == "1")
                {
                    CatalogHit h = new CatalogHit();
                    h.Title = E(e, "title"); h.VersionText = E(e, "ver"); h.UpdateId = E(e, "id"); h.Classification = E(e, "cls");
                    h.Ver = ParseVer(h.VersionText); h.Date = ParseDate(E(e, "date"));
                    if (h.Ver != null && h.UpdateId.Length > 0) hit = h;
                }
                return true;
            }
        }

        private static void CachePut(string key, CatalogHit h)
        {
            lock (CacheLock)
            {
                LoadCache();
                Dictionary<string, object> e = new Dictionary<string, object>();
                e["t"] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                e["found"] = h != null ? "1" : "0";
                if (h != null)
                {
                    e["title"] = h.Title; e["ver"] = h.VersionText; e["id"] = h.UpdateId; e["cls"] = h.Classification; e["date"] = DateText(h.Date);
                }
                _cache[key] = e;
            }
        }

        private static void SaveCache()
        {
            lock (CacheLock)
            {
                try
                {
                    LoadCache();
                    List<string> old = new List<string>();
                    foreach (KeyValuePair<string, object> kv in _cache)
                    {
                        Dictionary<string, object> e = kv.Value as Dictionary<string, object>;
                        if (e == null || !Fresh(e, 24 * 7)) old.Add(kv.Key);
                    }
                    foreach (string k in old) _cache.Remove(k);
                    string p = CachePath();
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    ser.MaxJsonLength = int.MaxValue;
                    File.WriteAllText(p, ser.Serialize(_cache), new UTF8Encoding(false));
                }
                catch { }
            }
        }

        // ---------------- lookups with a small thread pool ----------------
        private sealed class KeyIds { public string Key; public List<string> Ids; }

        private sealed class LookupState
        {
            public int Next = -1, Done, Ok, Err;
            public volatile bool Abort;
            public Exception Net;
            public object Gate = new object();
            public Dictionary<string, CatalogHit> Result;
            public List<KeyIds> Todo;
            public int Cached;
        }

        private static bool IsNetDown(Exception ex)
        {
            for (Exception x = ex; x != null; x = x.InnerException)
            {
                WebException we = x as WebException;
                if (we != null)
                {
                    if (we.Status == WebExceptionStatus.NameResolutionFailure || we.Status == WebExceptionStatus.ConnectFailure ||
                        we.Status == WebExceptionStatus.ProxyNameResolutionFailure || we.Status == WebExceptionStatus.TrustFailure ||
                        we.Status == WebExceptionStatus.SecureChannelFailure) return true;
                }
            }
            string m = (ex.Message ?? "").ToLowerInvariant();
            return m.Contains("remote name could not be resolved") || m.Contains("no such host");
        }

        // key (first hardware id) -> latest catalog row (null value = nothing found). Keys that could not be checked are missing.
        // netError receives the first network failure (the lookups then stop); at most 4 requests run at once.
        public static Dictionary<string, CatalogHit> LookupAll(WorkCtx c, List<HwDriver> devs, out Exception netError)
        {
            LookupState st = new LookupState();
            st.Result = new Dictionary<string, CatalogHit>(StringComparer.OrdinalIgnoreCase);
            st.Todo = new List<KeyIds>();
            foreach (HwDriver d in devs)
            {
                if (d.Key.Length == 0 || st.Result.ContainsKey(d.Key)) continue;
                bool queued = false;
                foreach (KeyIds q in st.Todo) if (string.Equals(q.Key, d.Key, StringComparison.OrdinalIgnoreCase)) { queued = true; break; }
                if (queued) continue;
                CatalogHit cached;
                if (CacheGet(d.Key, out cached)) { st.Result[d.Key] = cached; st.Cached++; continue; }
                KeyIds k = new KeyIds(); k.Key = d.Key; k.Ids = d.Ids;
                st.Todo.Add(k);
            }
            int total = st.Todo.Count;
            if (total > 0)
            {
                c.Info("Looking up " + total + " device(s) in the Microsoft Update Catalog (" + st.Cached + " from the cache)...", "Drivers");
                int nThreads = Math.Min(4, total);
                List<Thread> ths = new List<Thread>();
                for (int i = 0; i < nThreads; i++)
                {
                    Thread t = new Thread(delegate() { Worker(c, st); });
                    t.IsBackground = true;
                    ths.Add(t);
                    t.Start();
                }
                foreach (Thread t in ths) t.Join();
                SaveCache();
            }
            netError = st.Net;
            c.ThrowIfCancelled();
            return st.Result;
        }

        private static void Worker(WorkCtx c, LookupState st)
        {
            while (!st.Abort && !c.Cancelled)
            {
                int i = Interlocked.Increment(ref st.Next);
                if (i >= st.Todo.Count) break;
                KeyIds k = st.Todo[i];
                try
                {
                    CatalogHit h = FindLatest(k.Ids, 20000);
                    CachePut(k.Key, h);
                    lock (st.Gate) { st.Result[k.Key] = h; st.Ok++; }
                }
                catch (Exception ex)
                {
                    lock (st.Gate)
                    {
                        st.Err++;
                        if (st.Net == null && (IsNetDown(ex) || (st.Err >= 4 && st.Ok == 0))) { st.Net = ex; st.Abort = true; }
                    }
                }
                int done = Interlocked.Increment(ref st.Done);
                c.Progress(100.0 * done / st.Todo.Count, Lang.L("Looking for driver updates online...") + " (" + done + "/" + st.Todo.Count + ")");
            }
        }

        // ---------------- 4) download + install ----------------
        private static List<string> GetCabUrls(string updateId)
        {
            string json = "[{\"size\":0,\"languages\":\"\",\"uidInfo\":\"" + updateId + "\",\"updateID\":\"" + updateId + "\"}]";
            byte[] data = Encoding.UTF8.GetBytes("updateIDs=" + Uri.EscapeDataString(json));
            HttpWebRequest r = NewReq(Host + "DownloadDialog.aspx", 30000);
            r.Method = "POST";
            r.ContentType = "application/x-www-form-urlencoded";
            r.ContentLength = data.Length;
            using (Stream s = r.GetRequestStream()) s.Write(data, 0, data.Length);
            string html = ReadAll(r);
            List<string> urls = new List<string>();
            foreach (Match m in Regex.Matches(html, @"https?://[^'""]+\.cab", RegexOptions.IgnoreCase))
                if (!urls.Contains(m.Value)) urls.Add(m.Value);
            return urls;
        }

        private static string SafeName(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s ?? "") sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            string r = sb.ToString();
            if (r.Length > 40) r = r.Substring(0, 40);
            return r.Length == 0 ? "driver" : r;
        }

        private static void LogLines(WorkCtx c, string text)
        {
            foreach (string l in (text ?? "").Split('\n')) { string t = l.Trim(); if (t.Length > 0) c.Info("  " + t, "Drivers"); }
        }

        // downloads the driver CAB(s) of a catalog update, expands them and installs with pnputil. true = installed.
        public static bool InstallUpdate(WorkCtx c, string device, CatalogHit hit, out bool reboot, out string error)
        {
            reboot = false; error = "";
            string dir = Path.Combine(Path.Combine(Path.GetTempPath(), "RockToolbox-drivers"), SafeName(device) + "-" + hit.UpdateId.Substring(0, 8));
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                c.Info("Asking the catalog for the download of " + device + "...", "Drivers");
                List<string> urls = GetCabUrls(hit.UpdateId);
                if (urls.Count == 0) { error = "The catalog did not give a download link for this driver."; return false; }
                string files = Path.Combine(dir, "files");
                Directory.CreateDirectory(files);
                int n = 0;
                foreach (string u in urls)
                {
                    c.ThrowIfCancelled();
                    n++;
                    string cab = Path.Combine(dir, n + "-" + SafeName(Path.GetFileNameWithoutExtension(new Uri(u).AbsolutePath)) + ".cab");
                    c.Info("  downloading " + u, "Drivers");
                    Net.Download(u, cab, null, delegate { return c.Cancelled; });
                    string sub = Path.Combine(files, n.ToString(CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(sub);
                    int code;
                    string outp = Tool.Capture(Tool.System32("expand.exe"), "-F:* \"" + cab + "\" \"" + sub + "\"", out code);
                    if (code != 0) { LogLines(c, outp); error = "Could not unpack the driver package (expand.exe code " + code + ")."; return false; }
                }
                if (Directory.GetFiles(files, "*.inf", SearchOption.AllDirectories).Length == 0) { error = "The driver package has no .inf file."; return false; }
                c.Info("  installing with pnputil...", "Drivers");
                int pc;
                string po = Tool.Capture(Tool.System32("pnputil.exe"), "/add-driver \"" + Path.Combine(files, "*.inf") + "\" /subdirs /install", out pc);
                LogLines(c, po);
                if (pc == 0 || pc == 3010)
                {
                    reboot = pc == 3010;
                    try { Directory.Delete(dir, true); } catch { }
                    return true;
                }
                error = "pnputil returned code " + pc + ".";
                return false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                error = ex is WebException ? Lang.L(Net.FriendlyError(ex)) : ex.Message;
                return false;
            }
        }
    }
}
