using System;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace RockToolbox.Native
{
    // Status-bar text (colored by level) + a log file (%LOCALAPPDATA%\RockToolbox\native.log). Safe to call from any thread.
    internal sealed class Logger
    {
        private readonly TextBlock _status;
        private readonly string _file;
        private readonly object _gate = new object();
        public static string LastLevel = "INFO";

        public Logger(TextBlock status, string dataDir)
        {
            _status = status;
            _file = Path.Combine(dataDir, "native.log");
        }

        public void Log(string msg, string level, string tag)
        {
            string pre = (level == "ERROR" || level == "WARN") ? level + ": " : "";
            try
            {
                lock (_gate)
                    File.AppendAllText(_file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] [" + tag + "] " + msg + "\r\n", Encoding.UTF8);
            }
            catch { }
            if (_status == null || msg.StartsWith(" ")) return;
            LastLevel = level;
            string text = pre + msg.Trim();
            string color = level == "ERROR" ? "#FF6B6B" : (level == "WARN" ? "#FFB454" : "#C9CDE0");
            Ui.Post(delegate
            {
                _status.Text = text;
                _status.Foreground = Ui.Brush(color);
            });
        }

        public void Info(string msg, string tag) { Log(msg, "INFO", tag); }
        public void Warn(string msg, string tag) { Log(msg, "WARN", tag); }
        public void Error(string msg, string tag) { Log(msg, "ERROR", tag); }
    }
}
