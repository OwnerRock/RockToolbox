using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace RDownloader.Native
{
    // Runs console programs hidden and streams their output (replaces Invoke-External / Invoke-LiveTool).
    internal static class Tool
    {
        public static Encoding Oem()
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.UTF8; }
        }

        public static string System32(string exe)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
        }

        // Blocks until the program ends. Throws OperationCanceledException when the user pressed Stop.
        public static int Run(string file, string args, Action<string> onText, Encoding enc)
        {
            if (Work.CancelRequested) throw new OperationCanceledException();
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            if (enc == null) enc = Oem();
            psi.StandardOutputEncoding = enc;
            psi.StandardErrorEncoding = enc;

            Process p = Process.Start(psi);
            Work.Register(p);
            object gate = new object();
            Thread te = new Thread(delegate() { Pump(p.StandardError, onText, gate); });
            te.IsBackground = true;
            te.Start();
            Pump(p.StandardOutput, onText, gate);
            p.WaitForExit();
            te.Join(2000);
            Work.Unregister(p);
            if (Work.CancelRequested) throw new OperationCanceledException();
            int code = p.ExitCode;
            p.Dispose();
            return code;
        }

        private static void Pump(StreamReader r, Action<string> onText, object gate)
        {
            char[] buf = new char[1024];
            try
            {
                int n;
                while ((n = r.Read(buf, 0, buf.Length)) > 0)
                {
                    if (onText == null) continue;
                    string s = new string(buf, 0, n);
                    lock (gate) onText(s);
                }
            }
            catch { }
        }

        // Runs and returns everything the program printed.
        public static string Capture(string file, string args, out int exitCode)
        {
            StringBuilder sb = new StringBuilder();
            exitCode = Run(file, args, delegate(string s) { sb.Append(s); }, null);
            return sb.ToString();
        }

        public static string Capture(string file, string args)
        {
            int c;
            return Capture(file, args, out c);
        }


        // Runs a program (optionally with a visible window) and waits up to timeoutMs. Stop kills it.
        public static int RunLong(string file, string args, bool hidden, int timeoutMs)
        {
            if (Work.CancelRequested) throw new OperationCanceledException();
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = hidden;
            Process p = Process.Start(psi);
            Work.Register(p);
            bool done = p.WaitForExit(timeoutMs);
            Work.Unregister(p);
            if (Work.CancelRequested) throw new OperationCanceledException();
            if (!done)
            {
                try { p.Kill(); } catch { }
                throw new TimeoutException("the program did not finish in " + (timeoutMs / 60000) + " minutes");
            }
            int code = p.ExitCode;
            p.Dispose();
            return code;
        }

        // Fire and forget (for GUI programs / explorer)
        public static void Start(string file, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
    }
}
