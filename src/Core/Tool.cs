<<<<<<< HEAD
﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace RockToolbox.Native
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

            Process p = StartSafe(psi);
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

        // Process.Start, but when Windows refuses to start a program directly ("The file cannot be accessed by the system" -
        // this happens for the winget / App Installer alias and other Store app aliases) the same command is run through cmd.exe.
        private static Process StartSafe(ProcessStartInfo psi)
        {
            try { return Process.Start(psi); }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1920 && ex.NativeErrorCode != 5 && ex.NativeErrorCode != 193 && ex.NativeErrorCode != 740) throw;
                string cmd = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrEmpty(cmd) || psi.FileName.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase)) throw;
                ProcessStartInfo p2 = new ProcessStartInfo(cmd, "/d /c \"\"" + psi.FileName + "\" " + psi.Arguments + "\"");
                p2.UseShellExecute = false; p2.CreateNoWindow = psi.CreateNoWindow;
                p2.RedirectStandardOutput = psi.RedirectStandardOutput; p2.RedirectStandardError = psi.RedirectStandardError;
                p2.StandardOutputEncoding = psi.StandardOutputEncoding; p2.StandardErrorEncoding = psi.StandardErrorEncoding;
                return Process.Start(p2);
            }
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
            Process p = StartSafe(psi);
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
=======
﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace RockToolbox.Native
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

            Process p = StartSafe(psi);
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

        // Process.Start, but when Windows refuses to start a program directly ("The file cannot be accessed by the system" -
        // this happens for the winget / App Installer alias and other Store app aliases) the same command is run through cmd.exe.
        private static Process StartSafe(ProcessStartInfo psi)
        {
            try { return Process.Start(psi); }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1920 && ex.NativeErrorCode != 5 && ex.NativeErrorCode != 193 && ex.NativeErrorCode != 740) throw;
                string cmd = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrEmpty(cmd) || psi.FileName.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase)) throw;
                ProcessStartInfo p2 = new ProcessStartInfo(cmd, "/d /c \"\"" + psi.FileName + "\" " + psi.Arguments + "\"");
                p2.UseShellExecute = false; p2.CreateNoWindow = psi.CreateNoWindow;
                p2.RedirectStandardOutput = psi.RedirectStandardOutput; p2.RedirectStandardError = psi.RedirectStandardError;
                p2.StandardOutputEncoding = psi.StandardOutputEncoding; p2.StandardErrorEncoding = psi.StandardErrorEncoding;
                return Process.Start(p2);
            }
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
            Process p = StartSafe(psi);
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
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
