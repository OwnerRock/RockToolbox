<<<<<<< HEAD
﻿using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace RockToolbox.Native
{
    // C# port of src\pages\Repair.ps1 (SFC / DISM / chkdsk / Windows Update reset with live output).
    internal sealed class RepairPage : IPage
    {
        private TextBox _log;
        private readonly StringBuilder _all = new StringBuilder();
        private string _buf = "";
        private double _lastPct = -10.0;

        public string Key { get { return "Repair"; } }
        public void OnShown() { }

        public void Init(Window w, Logger log)
        {
            _log = Ui.Find<TextBox>("txtRepairLog");
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string drive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            string sfc = Path.Combine(sys, "sfc.exe");
            string dism = Path.Combine(sys, "dism.exe");

            Ui.Find<Button>("btnRpSfc").Click += delegate
            {
                Work.Run("System File Checker (SFC)", delegate(WorkCtx c)
                {
                    int before = Len();
                    int code = Step(c, "System File Checker (SFC)", sfc, "/scannow", Encoding.Unicode);
                    Verdict(c, "sfc", code, Since(before));
                });
            };
            Ui.Find<Button>("btnRpScan").Click += delegate
            {
                Work.Run("DISM - ScanHealth", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - ScanHealth", dism, "/Online /Cleanup-Image /ScanHealth", null), "");
                });
            };
            Ui.Find<Button>("btnRpRestore").Click += delegate
            {
                Work.Run("DISM - RestoreHealth", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - RestoreHealth", dism, "/Online /Cleanup-Image /RestoreHealth", null), "");
                });
            };
            Ui.Find<Button>("btnRpFull").Click += delegate
            {
                if (!Ui.Confirm("Run DISM RestoreHealth and then SFC?\n\nThis can take 15-40 minutes. You can press Stop at any time.")) return;
                Work.Run("Full repair (DISM + SFC)", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - RestoreHealth", dism, "/Online /Cleanup-Image /RestoreHealth", null), "");
                    int before = Len();
                    int code = Step(c, "System File Checker (SFC)", sfc, "/scannow", Encoding.Unicode);
                    Verdict(c, "sfc", code, Since(before));
                });
            };
            Ui.Find<Button>("btnRpChk").Click += delegate
            {
                Work.Run("Check disk (read-only)", delegate(WorkCtx c)
                {
                    int code = Step(c, "Check disk (read-only)", Path.Combine(sys, "chkdsk.exe"), drive + " /scan", null);
                    Say(c, "Check disk finished (exit code " + code + "). 0 = no errors, 1 = errors were fixed, 2/3 = cleanup or errors found.", "INFO");
                });
            };
            Ui.Find<Button>("btnRpChkFix").Click += delegate
            {
                if (!Ui.Confirm("Schedule a full disk check and repair of " + drive + " for the next restart?\n\nIt can take a long time on large drives. The PC cannot be used while it runs.")) return;
                Work.Run("Schedule disk repair", delegate(WorkCtx c)
                {
                    Step(c, "Schedule disk repair", Environment.GetEnvironmentVariable("ComSpec"), "/c echo Y| chkdsk " + drive + " /f /r", null);
                    Say(c, "Disk repair is scheduled. Restart Windows to run it.", "WARN");
                });
            };
            Ui.Find<Button>("btnRpComp").Click += delegate
            {
                Work.Run("DISM - Clean component store", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - Clean component store", dism, "/Online /Cleanup-Image /StartComponentCleanup", null), "");
                });
            };
            Ui.Find<Button>("btnRpWu").Click += delegate
            {
                if (!Ui.Confirm("Reset the Windows Update components (services, SoftwareDistribution and catroot2 folders)?")) return;
                Work.Run("Resetting Windows Update...", delegate(WorkCtx c)
                {
                    Add("");
                    Add("=== Reset Windows Update components   " + DateTime.Now.ToString("HH:mm:ss") + " ===");
                    SysUtil.ResetWindowsUpdate(c);
                    Add("Windows Update components were reset.");
                });
            };
            Ui.Find<Button>("btnRpCopy").Click += delegate
            {
                if (_log.Text.Length == 0) { Ui.Log.Warn("The log is empty.", "Repair"); return; }
                try { Clipboard.SetText(_log.Text); Ui.Log.Info("Log copied to the clipboard.", "Repair"); } catch { }
            };
            Ui.Find<Button>("btnRpClear").Click += delegate { _log.Clear(); lock (_all) _all.Length = 0; };
        }

        private int Len() { lock (_all) return _all.Length; }
        private string Since(int start) { lock (_all) return _all.ToString(Math.Min(start, _all.Length), _all.Length - Math.Min(start, _all.Length)); }

        private void Add(string line)
        {
            lock (_all) _all.AppendLine(line);
            Ui.Invoke(delegate { _log.AppendText(line + "\r\n"); _log.ScrollToEnd(); });
        }

        private void Say(WorkCtx c, string text, string level)
        {
            Add(text);
            c.Log(text, level, "Repair");
        }

        private void Verdict(WorkCtx c, string kind, int code, string text)
        {
            string v;
            if (kind == "sfc")
            {
                if (text.Contains("did not find any integrity violations")) v = "SFC: no problems found.";
                else if (text.Contains("successfully repaired")) v = "SFC: corrupted files were found and repaired. Restart Windows.";
                else if (text.Contains("unable to fix")) v = "SFC: corrupted files were found but could not be fixed. Run \"Repair image (DISM RestoreHealth)\" and then SFC again.";
                else if (Regex.IsMatch(text, "pending|could not perform")) v = "SFC could not finish. Restart Windows and try again.";
                else v = "SFC finished - see the log.";
            }
            else v = code == 0 ? "DISM finished successfully." : "DISM finished with exit code " + code + " - see the log.";
            Say(c, v, "INFO");
        }

        private int Step(WorkCtx c, string title, string file, string args, Encoding enc)
        {
            _buf = ""; _lastPct = -10.0;
            Add("");
            Add("=== " + title + "   " + DateTime.Now.ToString("HH:mm:ss") + " ===");
            c.Info(title + "...", "Repair");
            c.Progress(-1, title);
            int code;
            try { code = c.Tool(file, args, delegate(string x) { Feed(x, title); }, enc); }
            catch (OperationCanceledException) { Add("--- stopped by user ---"); throw; }
            if (_buf.Trim().Length > 0) { Add(_buf.Trim()); _buf = ""; }
            Add("--- finished, exit code " + code + " ---");
            return code;
        }

        // raw tool output: "12.5%" style text drives the progress bar, other lines go to the log box
        private void Feed(string text, string title)
        {
            string t = _buf + text;
            string[] parts = Regex.Split(t, "[\r\n]+");
            _buf = parts[parts.Length - 1];
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string seg = parts[i].TrimEnd();
                if (seg.Trim().Length == 0) continue;
                Match m = Regex.Match(seg, @"(\d{1,3}(?:[.,]\d+)?)\s*(%|percent)");
                bool isProg = m.Success && Regex.IsMatch(seg, @"^\s*\[?[=\s]*\d|Verification|complete|Stage|Progress|Total");
                double v = 0;
                bool have = m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                if (have && v >= 0 && v <= 100) Work.SetProgress(v, title + "  " + v.ToString("0") + "%");
                if (isProg)
                {
                    if (have && Math.Floor(v / 10) != Math.Floor(_lastPct / 10))
                    {
                        _lastPct = v;
                        Add(Regex.Replace(seg, "=+", "="));
                    }
                }
                else Add(seg);
            }
        }
    }
}
=======
﻿using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace RockToolbox.Native
{
    // C# port of src\pages\Repair.ps1 (SFC / DISM / chkdsk / Windows Update reset with live output).
    internal sealed class RepairPage : IPage
    {
        private TextBox _log;
        private readonly StringBuilder _all = new StringBuilder();
        private string _buf = "";
        private double _lastPct = -10.0;

        public string Key { get { return "Repair"; } }
        public void OnShown() { }

        public void Init(Window w, Logger log)
        {
            _log = Ui.Find<TextBox>("txtRepairLog");
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string drive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            string sfc = Path.Combine(sys, "sfc.exe");
            string dism = Path.Combine(sys, "dism.exe");

            Ui.Find<Button>("btnRpSfc").Click += delegate
            {
                Work.Run("System File Checker (SFC)", delegate(WorkCtx c)
                {
                    int before = Len();
                    int code = Step(c, "System File Checker (SFC)", sfc, "/scannow", Encoding.Unicode);
                    Verdict(c, "sfc", code, Since(before));
                });
            };
            Ui.Find<Button>("btnRpScan").Click += delegate
            {
                Work.Run("DISM - ScanHealth", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - ScanHealth", dism, "/Online /Cleanup-Image /ScanHealth", null), "");
                });
            };
            Ui.Find<Button>("btnRpRestore").Click += delegate
            {
                Work.Run("DISM - RestoreHealth", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - RestoreHealth", dism, "/Online /Cleanup-Image /RestoreHealth", null), "");
                });
            };
            Ui.Find<Button>("btnRpFull").Click += delegate
            {
                if (!Ui.Confirm("Run DISM RestoreHealth and then SFC?\n\nThis can take 15-40 minutes. You can press Stop at any time.")) return;
                Work.Run("Full repair (DISM + SFC)", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - RestoreHealth", dism, "/Online /Cleanup-Image /RestoreHealth", null), "");
                    int before = Len();
                    int code = Step(c, "System File Checker (SFC)", sfc, "/scannow", Encoding.Unicode);
                    Verdict(c, "sfc", code, Since(before));
                });
            };
            Ui.Find<Button>("btnRpChk").Click += delegate
            {
                Work.Run("Check disk (read-only)", delegate(WorkCtx c)
                {
                    int code = Step(c, "Check disk (read-only)", Path.Combine(sys, "chkdsk.exe"), drive + " /scan", null);
                    Say(c, "Check disk finished (exit code " + code + "). 0 = no errors, 1 = errors were fixed, 2/3 = cleanup or errors found.", "INFO");
                });
            };
            Ui.Find<Button>("btnRpChkFix").Click += delegate
            {
                if (!Ui.Confirm("Schedule a full disk check and repair of " + drive + " for the next restart?\n\nIt can take a long time on large drives. The PC cannot be used while it runs.")) return;
                Work.Run("Schedule disk repair", delegate(WorkCtx c)
                {
                    Step(c, "Schedule disk repair", Environment.GetEnvironmentVariable("ComSpec"), "/c echo Y| chkdsk " + drive + " /f /r", null);
                    Say(c, "Disk repair is scheduled. Restart Windows to run it.", "WARN");
                });
            };
            Ui.Find<Button>("btnRpComp").Click += delegate
            {
                Work.Run("DISM - Clean component store", delegate(WorkCtx c)
                {
                    Verdict(c, "dism", Step(c, "DISM - Clean component store", dism, "/Online /Cleanup-Image /StartComponentCleanup", null), "");
                });
            };
            Ui.Find<Button>("btnRpWu").Click += delegate
            {
                if (!Ui.Confirm("Reset the Windows Update components (services, SoftwareDistribution and catroot2 folders)?")) return;
                Work.Run("Resetting Windows Update...", delegate(WorkCtx c)
                {
                    Add("");
                    Add("=== Reset Windows Update components   " + DateTime.Now.ToString("HH:mm:ss") + " ===");
                    SysUtil.ResetWindowsUpdate(c);
                    Add("Windows Update components were reset.");
                });
            };
            Ui.Find<Button>("btnRpCopy").Click += delegate
            {
                if (_log.Text.Length == 0) { Ui.Log.Warn("The log is empty.", "Repair"); return; }
                try { Clipboard.SetText(_log.Text); Ui.Log.Info("Log copied to the clipboard.", "Repair"); } catch { }
            };
            Ui.Find<Button>("btnRpClear").Click += delegate { _log.Clear(); lock (_all) _all.Length = 0; };
        }

        private int Len() { lock (_all) return _all.Length; }
        private string Since(int start) { lock (_all) return _all.ToString(Math.Min(start, _all.Length), _all.Length - Math.Min(start, _all.Length)); }

        private void Add(string line)
        {
            lock (_all) _all.AppendLine(line);
            Ui.Invoke(delegate { _log.AppendText(line + "\r\n"); _log.ScrollToEnd(); });
        }

        private void Say(WorkCtx c, string text, string level)
        {
            Add(text);
            c.Log(text, level, "Repair");
        }

        private void Verdict(WorkCtx c, string kind, int code, string text)
        {
            string v;
            if (kind == "sfc")
            {
                if (text.Contains("did not find any integrity violations")) v = "SFC: no problems found.";
                else if (text.Contains("successfully repaired")) v = "SFC: corrupted files were found and repaired. Restart Windows.";
                else if (text.Contains("unable to fix")) v = "SFC: corrupted files were found but could not be fixed. Run \"Repair image (DISM RestoreHealth)\" and then SFC again.";
                else if (Regex.IsMatch(text, "pending|could not perform")) v = "SFC could not finish. Restart Windows and try again.";
                else v = "SFC finished - see the log.";
            }
            else v = code == 0 ? "DISM finished successfully." : "DISM finished with exit code " + code + " - see the log.";
            Say(c, v, "INFO");
        }

        private int Step(WorkCtx c, string title, string file, string args, Encoding enc)
        {
            _buf = ""; _lastPct = -10.0;
            Add("");
            Add("=== " + title + "   " + DateTime.Now.ToString("HH:mm:ss") + " ===");
            c.Info(title + "...", "Repair");
            c.Progress(-1, title);
            int code;
            try { code = c.Tool(file, args, delegate(string x) { Feed(x, title); }, enc); }
            catch (OperationCanceledException) { Add("--- stopped by user ---"); throw; }
            if (_buf.Trim().Length > 0) { Add(_buf.Trim()); _buf = ""; }
            Add("--- finished, exit code " + code + " ---");
            return code;
        }

        // raw tool output: "12.5%" style text drives the progress bar, other lines go to the log box
        private void Feed(string text, string title)
        {
            string t = _buf + text;
            string[] parts = Regex.Split(t, "[\r\n]+");
            _buf = parts[parts.Length - 1];
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string seg = parts[i].TrimEnd();
                if (seg.Trim().Length == 0) continue;
                Match m = Regex.Match(seg, @"(\d{1,3}(?:[.,]\d+)?)\s*(%|percent)");
                bool isProg = m.Success && Regex.IsMatch(seg, @"^\s*\[?[=\s]*\d|Verification|complete|Stage|Progress|Total");
                double v = 0;
                bool have = m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                if (have && v >= 0 && v <= 100) Work.SetProgress(v, title + "  " + v.ToString("0") + "%");
                if (isProg)
                {
                    if (have && Math.Floor(v / 10) != Math.Floor(_lastPct / 10))
                    {
                        _lastPct = v;
                        Add(Regex.Replace(seg, "=+", "="));
                    }
                }
                else Add(seg);
            }
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
