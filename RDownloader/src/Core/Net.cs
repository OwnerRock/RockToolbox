using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace RDownloader.Native
{
    // HTTP helpers (TLS 1.2, browser-like User-Agent) - replaces Invoke-RestMethod / Invoke-WebRequest.
    internal static class Net
    {
        static Net()
        {
            // Tls12 = 3072, Tls11 = 768, Tls = 192 (the .NET 4.0 enum lacks the newer names)
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192; } catch { }
            try { ServicePointManager.Expect100Continue = false; ServicePointManager.DefaultConnectionLimit = 32; } catch { }
        }

        private static HttpWebRequest Make(string url, int timeoutMs)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = DlOpts.UserAgent;
            r.Accept = "*/*";
            r.Timeout = timeoutMs; r.ReadWriteTimeout = timeoutMs;
            r.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            try { if (Ui.Settings != null && Ui.Settings.GetString("ProxyMode") == "none") r.Proxy = null; else r.Proxy = DlOpts.BuildProxy(); } catch { }
            return r;
        }

        // GET without following redirects; returns the Location header (or "" when there is none)
        public static string GetRedirectLocation(string url, int timeoutMs)
        {
            HttpWebRequest r = Make(url, timeoutMs);
            r.AllowAutoRedirect = false;
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
                {
                    string loc = resp.Headers["Location"];
                    return loc ?? "";
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse hr = ex.Response as HttpWebResponse;
                if (hr != null)
                {
                    string loc = hr.Headers["Location"]; hr.Close();
                    if (!string.IsNullOrEmpty(loc)) return loc;
                }
                throw;
            }
        }

        // tries every url in order and returns the first body that downloads; 'used' receives the url that worked
        public static string TryGetFirst(string[] urls, int timeoutMs, out string used)
        {
            used = "";
            Exception last = null;
            foreach (string u in urls)
            {
                try { string s = GetString(u, timeoutMs); used = u; return s; }
                catch (Exception ex) { last = ex; }
            }
            if (last != null) throw last;
            throw new InvalidOperationException("No address to try.");
        }

        // quick connectivity check: a network adapter is up and at least one well known host name resolves (or a proxy is configured)
        public static bool IsOnline()
        {
            try { if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false; } catch { }
            try
            {
                IWebProxy px = WebRequest.GetSystemWebProxy();
                Uri target = new Uri("https://github.com/");
                Uri via = px.GetProxy(target);
                if (via != null && via != target) return true;   // traffic goes through a proxy: the proxy resolves names
            }
            catch { }
            string[] hosts = new string[] { "github.com", "www.msftconnecttest.com", "www.microsoft.com" };
            object gate = new object();
            int pending = hosts.Length; bool ok = false;
            using (System.Threading.ManualResetEvent done = new System.Threading.ManualResetEvent(false))
            {
                foreach (string h in hosts)
                {
                    string host = h;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        bool good = false;
                        try { good = Dns.GetHostEntry(host).AddressList.Length > 0; } catch { }
                        lock (gate)
                        {
                            if (good) ok = true;
                            pending--;
                            if (good || pending == 0) { try { done.Set(); } catch { } }
                        }
                    });
                }
                done.WaitOne(4000);
                lock (gate) { return ok; }
            }
        }

        // short human text for a network exception
        public static string FriendlyError(Exception ex)
        {
            if (ex == null) return "Unknown error.";
            Exception e = ex;
            AggregateException ag = e as AggregateException;
            if (ag != null && ag.InnerExceptions.Count > 0) e = ag.InnerExceptions[0];
            string all = "";
            for (Exception x = e; x != null; x = x.InnerException) all += " " + x.Message;
            string low = all.ToLowerInvariant();
            WebException we = null;
            for (Exception x = e; x != null && we == null; x = x.InnerException) we = x as WebException;
            if (we != null)
            {
                HttpWebResponse hr = we.Response as HttpWebResponse;
                if (hr != null)
                {
                    int code = (int)hr.StatusCode;
                    if (code == 403 || code == 429) return "The server is limiting requests right now (HTTP " + code + "). Try again in a few minutes.";
                    if (code == 404) return "The server does not have this item (HTTP 404).";
                    if (code >= 500) return "The server has a problem right now (HTTP " + code + "). Try again later.";
                    return "The server answered with HTTP " + code + ".";
                }
                switch (we.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure: return "No internet connection or DNS cannot reach the server.";
                    case WebExceptionStatus.Timeout: return "The server took too long to answer (timeout).";
                    case WebExceptionStatus.ConnectFailure: return "Could not connect to the server. Check your internet connection, firewall or proxy.";
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.SecureChannelFailure: return "Secure connection (TLS) failed. Check the PC date and time and your antivirus or proxy.";
                    case WebExceptionStatus.ProxyNameResolutionFailure: return "The configured proxy cannot be reached.";
                }
            }
            if (low.Contains("remote name could not be resolved") || low.Contains("no such host") || low.Contains("name or service not known") || low.Contains("host is unknown"))
                return "No internet connection or DNS cannot reach the server.";
            if (low.Contains("timed out") || low.Contains("timeout")) return "The server took too long to answer (timeout).";
            if (low.Contains("403") || low.Contains("429") || low.Contains("rate limit")) return "The server is limiting requests right now. Try again in a few minutes.";
            if (low.Contains("ssl") || low.Contains("tls") || low.Contains("secure channel") || low.Contains("certificate")) return "Secure connection (TLS) failed. Check the PC date and time and your antivirus or proxy.";
            if (low.Contains("unable to connect") || low.Contains("actively refused") || low.Contains("connection was closed") || low.Contains("forcibly closed"))
                return "Could not connect to the server. Check your internet connection, firewall or proxy.";
            return e.Message;
        }

        public static string GetString(string url, int timeoutMs)
        {
            using (WebResponse resp = Make(url, timeoutMs).GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        public static byte[] GetBytes(string url, int timeoutMs)
        {
            using (WebResponse resp = Make(url, timeoutMs).GetResponse())
            using (Stream st = resp.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[65536]; int n;
                while ((n = st.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                return ms.ToArray();
            }
        }

        public static object GetJson(string url, int timeoutMs)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            return ser.DeserializeObject(GetString(url, timeoutMs));
        }

        // download to a file; onProgress(received, total) may be null. Honors Stop through cancelled().
        public static void Download(string url, string file, Action<long, long> onProgress, Func<bool> cancelled)
        {
            string tmp = file + ".part";
            using (WebResponse resp = Make(url, 60000).GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = File.Create(tmp))
            {
                long total = resp.ContentLength, got = 0; int n;
                byte[] buf = new byte[81920];
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancelled != null && cancelled()) throw new OperationCanceledException();
                    dst.Write(buf, 0, n); got += n;
                    if (onProgress != null) onProgress(got, total);
                }
            }
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        // helpers for JavaScriptSerializer results
        // JSON arrays come back as object[] or ArrayList depending on the framework: accept both
        public static System.Collections.ArrayList Arr(object o)
        {
            System.Collections.ArrayList a = o as System.Collections.ArrayList;
            if (a != null) return a;
            System.Collections.IEnumerable en = o as System.Collections.IEnumerable;
            if (en == null || o is string || o is System.Collections.IDictionary) return null;
            a = new System.Collections.ArrayList();
            foreach (object x in en) a.Add(x);
            return a;
        }

        public static object Get(object o, string key)
        {
            System.Collections.Generic.Dictionary<string, object> d = o as System.Collections.Generic.Dictionary<string, object>;
            object v; return d != null && d.TryGetValue(key, out v) ? v : null;
        }
        public static string Str(object o, string key) { object v = Get(o, key); return v == null ? "" : Convert.ToString(v); }
        public static int Int(object o, string key) { object v = Get(o, key); try { return v == null ? 0 : Convert.ToInt32(v); } catch { return 0; } }
    }
}
