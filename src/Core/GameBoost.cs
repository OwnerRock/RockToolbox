using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    // "Best Windows settings for games" (desktop / laptop) and the Tencent Gaming Buddy / Game Loop booster for PUBG Mobile.
    // Every changed value is saved first in gameboost.bak, so "Restore" puts the old values back exactly.
    internal static class GameBoost
    {
        private static string BakFile { get { return Path.Combine(UiHost.DataDir, "gameboost.bak"); } }
        private const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        // ---------- device detection ----------
        public sealed class Device { public bool Laptop; public string Cpu = "", Gpu = ""; public int Cores; public double RamGb; public bool Discrete; }

        public static Device Detect()
        {
            Device d = new Device();
            try { using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, NumberOfLogicalProcessors FROM Win32_Processor")) foreach (ManagementObject o in s.Get()) { d.Cpu = Convert.ToString(o["Name"]).Trim(); d.Cores += Convert.ToInt32(o["NumberOfLogicalProcessors"]); } } catch { }
            try { using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem")) foreach (ManagementObject o in s.Get()) d.RamGb = Convert.ToDouble(o["TotalPhysicalMemory"]) / 1073741824.0; } catch { }
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    foreach (ManagementObject o in s.Get())
                    {
                        string n = Convert.ToString(o["Name"]);
                        if (d.Gpu.Length > 0) d.Gpu += " + ";
                        d.Gpu += n;
                        if (Regex.IsMatch(n, @"NVIDIA|GeForce|Radeon RX|Radeon Pro|Arc ", RegexOptions.IgnoreCase) && !Regex.IsMatch(n, @"Graphics$", RegexOptions.IgnoreCase)) d.Discrete = true;
                    }
            }
            catch { }
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT * FROM Win32_Battery")) foreach (ManagementObject o in s.Get()) d.Laptop = true;
                if (!d.Laptop)
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                        foreach (ManagementObject o in s.Get())
                        {
                            ushort[] t = o["ChassisTypes"] as ushort[];
                            if (t != null) foreach (ushort x in t) if (x == 8 || x == 9 || x == 10 || x == 11 || x == 12 || x == 14 || x == 18 || x == 21 || x == 30 || x == 31 || x == 32) d.Laptop = true;
                        }
            }
            catch { }
            return d;
        }

        // ---------- backup ----------
        private sealed class Bak
        {
            public List<string> Lines = new List<string>();
            private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Bak() { try { if (File.Exists(BakFile)) foreach (string l in File.ReadAllLines(BakFile, Encoding.UTF8)) { if (l.Length > 0) { Lines.Add(l); _seen.Add(Key(l)); } } } catch { } }
            private static string Key(string l) { string[] p = l.Split('\t'); return p.Length >= 3 && p[0] == "R" ? p[1] + "|" + p[2] : l; }
            public void Add(string l) { if (_seen.Add(Key(l))) Lines.Add(l); }
            public void Save() { try { File.WriteAllLines(BakFile, Lines.ToArray(), Encoding.UTF8); } catch { } }
        }

        private static void SetReg(WorkCtx c, Bak b, string path, string name, object val, RegistryValueKind kind, string why)
        {
            object old = Reg.Get(path, name);
            string oldS = old == null ? "-" : Convert.ToString(old);
            string oldK = old == null ? "-" : (old is int ? "D" : "S");
            b.Add("R\t" + path + "\t" + name + "\t" + oldK + "\t" + oldS);
            try { Reg.Set(path, name, val, kind); c.Info("  " + why, "Games"); }
            catch (Exception ex) { c.Warn("  skipped (" + why + "): " + ex.Message, "Games"); }
        }

        private static string Pc(WorkCtx c, string args)
        {
            int code; return Tool.Capture("powercfg.exe", args, out code);
        }

        private static string ActivePlan()
        {
            Match m = Regex.Match(Tool.Capture("powercfg.exe", "/getactivescheme"), @"([0-9a-fA-F]{8}-[0-9a-fA-F-]{27})");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static int AcValue(string sub, string setting)
        {
            Match m = Regex.Match(Tool.Capture("powercfg.exe", "/query SCHEME_CURRENT " + sub + " " + setting), @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)");
            return m.Success ? Convert.ToInt32(m.Groups[1].Value, 16) : -1;
        }

        private static void SetAc(WorkCtx c, Bak b, string sub, string setting, int val, string why)
        {
            int old = AcValue(sub, setting);
            if (old >= 0) b.Add("A\t" + sub + "\t" + setting + "\t" + old);
            Pc(c, "/setacvalueindex SCHEME_CURRENT " + sub + " " + setting + " " + val);
            c.Info("  " + why, "Games");
        }

        // ---------- the two profiles ----------
        public static void Apply(WorkCtx c, bool laptop)
        {
            Device d = Detect();
            c.Info("This PC: " + (d.Laptop ? "laptop" : "desktop") + ", " + d.Cpu + ", " + d.Cores + " threads, " + d.RamGb.ToString("0") + " GB RAM, GPU: " + d.Gpu, "Games");
            if (laptop != d.Laptop) c.Warn("Note: you chose the " + (laptop ? "laptop" : "desktop") + " profile but this PC looks like a " + (d.Laptop ? "laptop" : "desktop") + ". Applying what you chose.", "Games");
            Bak b = new Bak();
            c.Info("Applying the " + (laptop ? "laptop" : "desktop") + " gaming profile...", "Games");
            SetReg(c, b, @"HKCU:\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord, "Windows Game Mode: on");
            SetReg(c, b, @"HKCU:\Software\Microsoft\GameBar", "AllowAutoGameMode", 1, RegistryValueKind.DWord, "Windows Game Mode: allowed");
            SetReg(c, b, @"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord, "Background game recording (Game DVR): off");
            SetReg(c, b, @"HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord, "Xbox Game Bar capture: off");
            SetReg(c, b, @"HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, RegistryValueKind.DWord, "Hardware-accelerated GPU scheduling: on (needs a restart)");
            SetReg(c, b, @"HKCU:\Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String, "Mouse acceleration (enhance pointer precision): off");
            SetReg(c, b, @"HKCU:\Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String, "Mouse acceleration threshold 1: 0");
            SetReg(c, b, @"HKCU:\Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String, "Mouse acceleration threshold 2: 0");
            string mm = @"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
            SetReg(c, b, mm, "SystemResponsiveness", 0, RegistryValueKind.DWord, "Games get the CPU first (SystemResponsiveness 0)");
            SetReg(c, b, mm, "NetworkThrottlingIndex", -1, RegistryValueKind.DWord, "Network throttling: off");
            SetReg(c, b, mm + @"\Tasks\Games", "GPU Priority", 8, RegistryValueKind.DWord, "Games: GPU priority 8");
            SetReg(c, b, mm + @"\Tasks\Games", "Priority", 6, RegistryValueKind.DWord, "Games: CPU priority 6");
            SetReg(c, b, mm + @"\Tasks\Games", "Scheduling Category", "High", RegistryValueKind.String, "Games: scheduling category High");
            SetReg(c, b, mm + @"\Tasks\Games", "SFIO Priority", "High", RegistryValueKind.String, "Games: disk priority High");
            if (!laptop)
            {
                SetReg(c, b, @"HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26, RegistryValueKind.DWord, "Foreground program (the game) gets longer CPU slices");
                string cur = ActivePlan();
                if (cur.Length > 0 && !cur.Equals(HighPerf, StringComparison.OrdinalIgnoreCase)) b.Add("P\t" + cur);
                string r = Pc(c, "/setactive " + HighPerf);
                if (r.IndexOf("not exist", StringComparison.OrdinalIgnoreCase) >= 0 || r.IndexOf("Invalid", StringComparison.OrdinalIgnoreCase) >= 0 || r.IndexOf("does not", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Pc(c, "/duplicatescheme " + HighPerf + " " + HighPerf);
                    Pc(c, "/setactive " + HighPerf);
                }
                c.Info("  Power plan: High performance", "Games");
            }
            else
            {
                SetAc(c, b, "SUB_PROCESSOR", "PROCTHROTTLEMIN", 100, "On the charger: CPU never drops below full speed");
                SetAc(c, b, "SUB_PROCESSOR", "SYSCOOLPOL", 1, "On the charger: active cooling (fan first)");
                SetAc(c, b, "SUB_PCIE", "ASPM", 0, "On the charger: PCIe power saving off (GPU)");
                SetAc(c, b, "SUB_SLEEP", "STANDBYIDLE", 0, "On the charger: no automatic sleep while playing");
                Pc(c, "/setactive SCHEME_CURRENT");
                c.Info("  Battery settings are left as they are, so battery life is not affected.", "Games");
            }
            b.Save();
            c.Info("Done. Restart Windows once so every change is active. You can undo all of it with \"Restore previous settings\".", "Games");
        }

        public static void Restore(WorkCtx c)
        {
            Bak b = new Bak();
            if (b.Lines.Count == 0) { c.Info("Nothing to restore - the game settings were not changed by Rock Toolbox.", "Games"); return; }
            int n = 0;
            foreach (string l in b.Lines)
            {
                try
                {
                    string[] p = l.Split('\t');
                    if (p[0] == "R")
                    {
                        if (p[3] == "-") Reg.DeleteValue(p[1], p[2]);
                        else if (p[3] == "D") Reg.Set(p[1], p[2], Convert.ToInt32(p[4]), RegistryValueKind.DWord);
                        else Reg.Set(p[1], p[2], p[4], RegistryValueKind.String);
                        n++;
                    }
                    else if (p[0] == "P") { Pc(c, "/setactive " + p[1]); n++; }
                    else if (p[0] == "A") { Pc(c, "/setacvalueindex SCHEME_CURRENT " + p[1] + " " + p[2] + " " + p[3]); n++; }
                }
                catch (Exception ex) { c.Warn("Could not restore one value: " + ex.Message, "Games"); }
            }
            Pc(c, "/setactive SCHEME_CURRENT");
            try { File.Delete(BakFile); } catch { }
            c.Info("Restored " + n + " settings to how they were before. Restart Windows to finish.", "Games");
        }

        // ---------- Game Loop / Tencent Gaming Buddy ----------
        private static readonly string[] EmuExe = { "AndroidEmulator.exe", "AndroidEmulatorEn.exe", "AndroidEmulatorEx.exe", "aow_exe.exe", "AowExe.exe", "TBSWebRenderer.exe", "AppMarket.exe", "GameLoop.exe", "Gameloop.exe", "GLSystemService.exe" };

        private static List<string> FindEmulator()
        {
            List<string> roots = new List<string>();
            string[] pf = { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("SystemDrive") + "\\" };
            foreach (string p in pf) { if (string.IsNullOrEmpty(p)) continue; foreach (string n in new string[] { "TxGameAssistant", "Tencent\\TxGameAssistant", "Tencent\\GameLoop", "GameLoop", "TGB", "Tencent Gaming Buddy", "Tencent" }) { string f = Path.Combine(p, n); if (Directory.Exists(f) && !roots.Contains(f)) roots.Add(f); } }
            string[] keys = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            foreach (string k in keys)
            {
                try
                {
                    using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(k))
                        if (rk != null) foreach (string sn in rk.GetSubKeyNames())
                            using (RegistryKey sk = rk.OpenSubKey(sn))
                            {
                                if (sk == null) continue;
                                string dn = Convert.ToString(sk.GetValue("DisplayName"));
                                if (dn.IndexOf("Gameloop", StringComparison.OrdinalIgnoreCase) >= 0 || dn.IndexOf("Game Loop", StringComparison.OrdinalIgnoreCase) >= 0 || dn.IndexOf("Tencent Gaming Buddy", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    string loc = Convert.ToString(sk.GetValue("InstallLocation"));
                                    if (string.IsNullOrEmpty(loc)) { string di = Convert.ToString(sk.GetValue("DisplayIcon")); if (!string.IsNullOrEmpty(di)) { try { loc = Path.GetDirectoryName(di.Split(',')[0].Trim('"')); } catch { } } }
                                    if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc) && !roots.Contains(loc)) roots.Add(loc);
                                }
                            }
                }
                catch { }
            }
            List<string> exes = new List<string>();
            foreach (string r in roots) Scan(r, 0, exes);
            return exes;
        }

        private static void Scan(string dir, int depth, List<string> res)
        {
            if (depth > 4) return;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*.exe"))
                    foreach (string n in EmuExe) if (string.Equals(Path.GetFileName(f), n, StringComparison.OrdinalIgnoreCase) && !res.Contains(f)) res.Add(f);
                foreach (string s in Directory.GetDirectories(dir)) Scan(s, depth + 1, res);
            }
            catch { }
        }

        public static void ApplyGameLoop(WorkCtx c)
        {
            Device d = Detect();
            c.Info("This PC: " + (d.Laptop ? "laptop" : "desktop") + ", " + d.Cpu + ", " + d.Cores + " threads, " + d.RamGb.ToString("0") + " GB RAM, GPU: " + d.Gpu, "GameLoop");
            List<string> exes = FindEmulator();
            if (exes.Count == 0) { c.Error("Game Loop / Tencent Gaming Buddy was not found on this PC. Install it first (or open it once), then try again.", "GameLoop"); return; }
            Bak b = new Bak();
            foreach (string exe in exes)
            {
                string fn = Path.GetFileName(exe);
                SetReg(c, b, @"HKCU:\Software\Microsoft\DirectX\UserGpuPreferences", exe, "GpuPreference=2;", RegistryValueKind.String, fn + ": uses the high-performance GPU");
                SetReg(c, b, @"HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", exe, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE", RegistryValueKind.String, fn + ": fullscreen optimizations off");
                SetReg(c, b, @"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + fn + @"\PerfOptions", "CpuPriorityClass", 3, RegistryValueKind.DWord, fn + ": runs with High CPU priority");
            }
            SetReg(c, b, @"HKCU:\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord, "Windows Game Mode: on");
            SetReg(c, b, @"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord, "Background game recording: off");
            if (d.Laptop)
            {
                SetAc(c, b, "SUB_PROCESSOR", "PROCTHROTTLEMIN", 100, "On the charger: CPU at full speed");
                SetAc(c, b, "SUB_PCIE", "ASPM", 0, "On the charger: PCIe power saving off");
                Pc(c, "/setactive SCHEME_CURRENT");
            }
            b.Save();
            int fps = 60; string eng = "Smart / Standard";
            if (d.Cores >= 8 && d.RamGb >= 15 && d.Discrete) fps = 120;
            else if (d.Cores >= 6 && d.RamGb >= 7) fps = 90;
            if (d.Discrete) eng = "DirectX+ (or Vulkan if it is offered)"; else eng = "OpenGL+";
            c.Info("Best in-game choice for this PC: Game Loop > Settings > Engine: " + eng + ", Memory: " + Math.Min(8192, Math.Max(2048, (int)(d.RamGb * 1024 / 2))) + " MB, CPU cores: " + Math.Max(2, Math.Min(6, d.Cores / 2)) + ".", "GameLoop");
            c.Info("PUBG Mobile > Settings > Graphics: Smooth + frame rate " + (fps == 120 ? "Extreme/120 FPS (60 if the game stutters)" : fps + " FPS") + ". The FPS can only be picked inside the game.", "GameLoop");
            c.Info("Done. Close and reopen Game Loop for the settings to take effect. \"Restore previous settings\" undoes them.", "GameLoop");
        }
    }
}
