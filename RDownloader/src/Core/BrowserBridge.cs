using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;

namespace RDownloader.Native
{
    // The browser extension talks to the app here: http://127.0.0.1:53741 (or the next free port up to 53750).
    //   GET  /ping  -> {"app":"RDownloader","version":"4.0.0"}
    //   POST /add   (header X-Rock-Ext: 1, JSON {url, filename, referrer, mime, size, userAgent}) -> {"ok":true}
    // Only the extension (chrome-extension:// / moz-extension:// origin) and local tools without an Origin may call it.
    internal static class BrowserBridge
    {
        public const int BasePort = 53741;
        private static HttpListener _l;
        private static Thread _t;
        public static int Port;

        public static bool Running { get { return _l != null && _l.IsListening; } }

        public static void Start()
        {
            if (Running) return;
            for (int p = BasePort; p < BasePort + 10; p++)
            {
                try
                {
                    HttpListener l = new HttpListener();
                    l.Prefixes.Add("http://127.0.0.1:" + p + "/");
                    l.Start();
                    _l = l; Port = p;
                    _t = new Thread(Loop); _t.IsBackground = true; _t.Start();
                    return;
                }
                catch (Exception) { }
            }
        }

        public static void Stop()
        {
            try { if (_l != null) { _l.Close(); } } catch (Exception) { }
            _l = null;
        }

        private static void Loop()
        {
            while (true)
            {
                HttpListener l = _l;
                if (l == null || !l.IsListening) return;
                HttpListenerContext c;
                try { c = l.GetContext(); }
                catch (Exception) { return; }
                try { Handle(c); }
                catch (Exception) { try { c.Response.Abort(); } catch (Exception) { } }
            }
        }

        private static bool OriginOk(string o)
        {
            if (string.IsNullOrEmpty(o)) return true;
            return o.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) || o.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase);
        }

        private static void Reply(HttpListenerContext c, int code, string json, string origin)
        {
            byte[] b = Encoding.UTF8.GetBytes(json ?? "");
            c.Response.StatusCode = code;
            c.Response.ContentType = "application/json; charset=utf-8";
            if (!string.IsNullOrEmpty(origin) && OriginOk(origin))
            {
                c.Response.AddHeader("Access-Control-Allow-Origin", origin);
                c.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-Rock-Ext");
                c.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                c.Response.AddHeader("Vary", "Origin");
            }
            c.Response.ContentLength64 = b.Length;
            c.Response.OutputStream.Write(b, 0, b.Length);
            c.Response.OutputStream.Close();
        }

        private static void Handle(HttpListenerContext c)
        {
            string origin = c.Request.Headers["Origin"];
            if (!OriginOk(origin)) { Reply(c, 403, "{\"ok\":false}", null); return; }
            string path = c.Request.Url.AbsolutePath.TrimEnd('/');
            string method = c.Request.HttpMethod.ToUpperInvariant();
            if (method == "OPTIONS") { Reply(c, 204, "", origin); return; }
            if (method == "GET" && path == "/ping") { Reply(c, 200, "{\"app\":\"RDownloader\",\"version\":\"4.0.0\"}", origin); return; }
            if (method == "POST" && path == "/add")
            {
                if (c.Request.Headers["X-Rock-Ext"] != "1") { Reply(c, 403, "{\"ok\":false}", origin); return; }
                string body;
                using (StreamReader sr = new StreamReader(c.Request.InputStream, Encoding.UTF8)) body = sr.ReadToEnd();
                if (body.Length > 200000) { Reply(c, 413, "{\"ok\":false}", origin); return; }
                Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                string url = S(d, "url");
                if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)))
                { Reply(c, 400, "{\"ok\":false}", origin); return; }
                string fn = S(d, "filename"), rf = S(d, "referrer"), ua = S(d, "userAgent");
                Application app = Application.Current;
                if (app == null) { Reply(c, 503, "{\"ok\":false}", origin); return; }
                app.Dispatcher.BeginInvoke(new Action(delegate
                {
                    try { DlWin.ShowInfo(url, fn, rf, ua, true); }
                    catch (Exception ex) { try { Ui.Log.Warn("Could not show the download window: " + ex.Message, "Extension"); } catch (Exception) { } }
                }));
                Reply(c, 200, "{\"ok\":true}", origin);
                return;
            }
            Reply(c, 404, "{\"ok\":false}", origin);
        }

        private static string S(Dictionary<string, object> d, string k)
        {
            object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
        }
    }
}
