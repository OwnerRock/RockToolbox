using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Threading;

namespace RDownloader.Native
{
    // settings.json in %LOCALAPPDATA%\RDownloader - the same file and keys the PowerShell version uses,
    // so both versions share the user's settings. Unknown keys are kept when saving.
    internal sealed class AppSettings
    {
        private readonly Dictionary<string, object> _v = new Dictionary<string, object>();
        private readonly string _dir;
        private readonly string _file;
        private DispatcherTimer _timer;

        public AppSettings(string dataDir)
        {
            _dir = dataDir;
            _file = Path.Combine(dataDir, "settings.json");
            _v["RememberTab"] = false;
            _v["StartTab"] = "Install";
            _v["LastTab"] = "Install";
            _v["StartMaximized"] = false;
            _v["Topmost"] = false;
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_file)) return;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> obj =
                    ser.DeserializeObject(File.ReadAllText(_file, Encoding.UTF8)) as Dictionary<string, object>;
                if (obj == null) return;
                foreach (KeyValuePair<string, object> kv in obj)
                    if (kv.Value != null) _v[kv.Key] = kv.Value;
            }
            catch { }
        }

        public string GetString(string key)
        {
            object o;
            if (_v.TryGetValue(key, out o) && o != null) return Convert.ToString(o);
            return "";
        }

        public bool GetBool(string key)
        {
            object o;
            if (!_v.TryGetValue(key, out o) || o == null) return false;
            if (o is bool) return (bool)o;
            bool b;
            return bool.TryParse(Convert.ToString(o), out b) && b;
        }

        public bool GetBool(string key, bool def)
        {
            object o;
            if (!_v.TryGetValue(key, out o) || o == null) return def;
            return GetBool(key);
        }

        public void Set(string key, object value) { _v[key] = value; }

        public void SaveSoon()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(400);
                _timer.Tick += delegate { _timer.Stop(); SaveNow(); };
            }
            _timer.Stop();
            _timer.Start();
        }

        public void SaveNow()
        {
            try
            {
                Directory.CreateDirectory(_dir);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                File.WriteAllText(_file, ser.Serialize(_v), new UTF8Encoding(true));
            }
            catch { }
        }
    }
}
