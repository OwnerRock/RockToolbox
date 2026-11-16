<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;

namespace RockToolbox.Native
{
    // OneDrive / Microsoft Edge / Internet Explorer: complete removal and restore (C# port of the PowerShell helpers).
    internal static class Removers
    {
        private static string Pf86 { get { return Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? ""; } }
        private static string Pf { get { return Environment.GetEnvironmentVariable("ProgramFiles") ?? ""; } }
        private static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        private static string Win { get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); } }
        private static string ProgData { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); } }

        // ---------- shared helpers ----------
        public static void RemovePathForce(WorkCtx c, string p)
        {
            if (!File.Exists(p) && !Directory.Exists(p)) return;
            FastFs.DeleteAny(p);
            if (!File.Exists(p) && !Directory.Exists(p)) return;
            c.Tool(Tool.System32("takeown.exe"), "/F \"" + p + "\" /R /D Y");
            c.Tool(Tool.System32("icacls.exe"), "\"" + p + "\" /grant *S-1-5-32-544:F /T /C /Q");
            FastFs.DeleteAny(p);
            if (Directory.Exists(p)) c.Tool(Environment.GetEnvironmentVariable("ComSpec"), "/c rd /s /q \"" + p + "\"");
        }

        public static void RemoveShortcutsNamed(string pattern)
        {
            List<string> dirs = new List<string>();
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"));
            foreach (string d in dirs)
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    foreach (string f in Directory.GetFiles(d, pattern, SearchOption.AllDirectories))
                        try { File.Delete(f); } catch { }
                }
                catch { }
            }
        }

        public static void DeleteService(WorkCtx c, string name)
        {
            c.Tool(Tool.System32("sc.exe"), "stop " + name);
            c.Tool(Tool.System32("sc.exe"), "delete " + name);
        }

        // ---------- winget ----------
        public static string FindWinget()
        {
            string p = Path.Combine(Local, @"Microsoft\WindowsApps\winget.exe");
            if (File.Exists(p)) return p;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string d in path.Split(';'))
            {
                try { string f = Path.Combine(d.Trim(), "winget.exe"); if (d.Trim().Length > 0 && File.Exists(f)) return f; } catch { }
            }
            return null;
        }

        // ---------- OneDrive ----------
        public static bool OneDriveInstalled()
        {
            string[] ps = {
                Path.Combine(Local, @"Microsoft\OneDrive\OneDrive.exe"), Path.Combine(Pf, @"Microsoft OneDrive\OneDrive.exe"),
                Path.Combine(Pf86, @"Microsoft OneDrive\OneDrive.exe"), Path.Combine(Win, @"SysWOW64\OneDriveSetup.exe"),
                Path.Combine(Win, @"System32\OneDriveSetup.exe") };
            foreach (string p in ps) if (File.Exists(p)) return true;
            return false;
        }

        public static void RemoveOneDrive(WorkCtx c)
        {
            c.Info("Removing OneDrive completely...", "OneDrive");
            foreach (string n in new string[] { "OneDrive", "OneDriveSetup", "FileCoAuth", "FileSyncHelper" }) SysUtil.KillByName(n);
            Reg.SetDword(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1);
            List<string> setups = new List<string>();
            foreach (string p in new string[] { Path.Combine(Win, @"SysWOW64\OneDriveSetup.exe"), Path.Combine(Win, @"System32\OneDriveSetup.exe") })
                if (File.Exists(p)) setups.Add(p);
            foreach (string b in new string[] { Path.Combine(Local, @"Microsoft\OneDrive"), Path.Combine(Pf, "Microsoft OneDrive"), Path.Combine(Pf86, "Microsoft OneDrive") })
            {
                try { if (Directory.Exists(b)) foreach (string f in Directory.GetFiles(b, "OneDriveSetup.exe", SearchOption.AllDirectories)) if (!setups.Contains(f)) setups.Add(f); } catch { }
            }
            foreach (string e in setups)
            {
                c.Info("  running " + e + " /uninstall", "OneDrive");
                c.Tool(e, "/uninstall");
                c.Tool(e, "/uninstall /allusers");
            }
            string wg = FindWinget();
            if (wg != null) c.Tool(wg, "uninstall --id Microsoft.OneDrive -e --silent --disable-interactivity --accept-source-agreements");
            foreach (string n in new string[] { "OneDrive", "OneDriveSetup" }) SysUtil.KillByName(n);
            foreach (string d in new string[] { Path.Combine(Local, @"Microsoft\OneDrive"), Path.Combine(ProgData, "Microsoft OneDrive"),
                Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", "OneDriveTemp"), Path.Combine(Pf, "Microsoft OneDrive"), Path.Combine(Pf86, "Microsoft OneDrive") })
                if (Directory.Exists(d)) { c.Info("  deleting " + d, "OneDrive"); RemovePathForce(c, d); }
            RemoveShortcutsNamed("OneDrive*.lnk");
            TaskSched.DeleteByPrefix("OneDrive");
            foreach (string rk in new string[] { @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" })
                foreach (string vn in new string[] { "OneDrive", "OneDriveSetup" }) Reg.DeleteValue(rk, vn);
            foreach (string k in new string[] { @"HKCR:\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", @"HKCR:\Wow6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}" })
                if (Reg.KeyExists(k)) { try { Reg.SetDword(k, "System.IsPinnedToNameSpaceTree", 0); } catch { } }
            Reg.DeleteKey(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{018D5C66-4533-4307-9B53-224DE2ED1FE6}");
            Reg.DeleteKey(@"HKCU:\Software\Microsoft\OneDrive");
            string hive = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", @"Users\Default\NTUSER.DAT");
            if (File.Exists(hive))
            {
                int code = c.Tool(Tool.System32("reg.exe"), "load HKU\\RockDefaultUser \"" + hive + "\"");
                if (code == 0)
                {
                    c.Tool(Tool.System32("reg.exe"), "delete \"HKU\\RockDefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v OneDriveSetup /f");
                    GC.Collect(); GC.WaitForPendingFinalizers();
                    c.Tool(Tool.System32("reg.exe"), "unload HKU\\RockDefaultUser");
                }
            }
            foreach (string p in new string[] { Path.Combine(Win, @"SysWOW64\OneDriveSetup.exe"), Path.Combine(Win, @"System32\OneDriveSetup.exe") })
                if (File.Exists(p)) { c.Info("  deleting " + p, "OneDrive"); RemovePathForce(c, p); }
            // the user's OneDrive folder holds YOUR files: only deleted when empty
            List<string> od = new List<string>();
            foreach (string v in new string[] { Environment.GetEnvironmentVariable("OneDrive"), Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"), Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "OneDrive") })
                if (!string.IsNullOrEmpty(v) && !od.Contains(v)) od.Add(v);
            foreach (string d in od)
            {
                if (!Directory.Exists(d)) continue;
                if (Directory.GetFileSystemEntries(d).Length == 0) { try { Directory.Delete(d); } catch { } }
                else c.Warn("  kept " + d + " - it still holds your files (move them out, then delete the folder yourself).", "OneDrive");
            }
            if (OneDriveInstalled()) c.Warn("OneDrive is still partly present - restart Windows and run this again.", "OneDrive");
            else c.Info("OneDrive was removed completely.", "OneDrive");
        }

        public static void RestoreOneDrive(WorkCtx c)
        {
            Reg.DeleteValue(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC");
            string wg = FindWinget();
            if (wg != null) c.Tool(wg, "install --id Microsoft.OneDrive -e --silent --accept-source-agreements --accept-package-agreements");
            else c.Warn("winget is not available - install OneDrive from https://www.microsoft.com/microsoft-365/onedrive/download", "OneDrive");
        }

        // ---------- Edge ----------
        private static readonly string[] EdgeGuids = {
            "{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", "{2CD8A007-E189-409D-A2C8-9AF4EF3C72AA}",
            "{0D50BFEC-CD6A-4F9A-964C-C7416E3ACB10}", "{65C35B14-6C1D-4122-AC46-7148CC9D6497}" };

        public static bool EdgeInstalled()
        {
            return File.Exists(Path.Combine(Pf86, @"Microsoft\Edge\Application\msedge.exe")) || File.Exists(Path.Combine(Pf, @"Microsoft\Edge\Application\msedge.exe"));
        }

        private static List<string> FindEdgeSetups(string baseDir)
        {
            List<string> r = new List<string>();
            try
            {
                if (Directory.Exists(baseDir))
                    foreach (string f in Directory.GetFiles(baseDir, "setup.exe", SearchOption.AllDirectories))
                        if (f.EndsWith(@"\Installer\setup.exe", StringComparison.OrdinalIgnoreCase)) r.Add(f);
            }
            catch { }
            r.Sort(); r.Reverse();
            return r;
        }

        public static void RemoveEdge(WorkCtx c)
        {
            c.Info("Removing Microsoft Edge completely...", "Edge");
            bool webView = Directory.Exists(Path.Combine(Pf86, @"Microsoft\EdgeWebView")) || Directory.Exists(Path.Combine(Pf, @"Microsoft\EdgeWebView"));
            if (webView) c.Info("  the WebView2 runtime is kept (many apps need it).", "Edge");
            foreach (string n in new string[] { "msedge", "MicrosoftEdge", "MicrosoftEdgeCP", "MicrosoftEdgeSH", "msedge_proxy", "pwahelper", "cookie_exporter", "notification_helper" }) SysUtil.KillByName(n);
            Reg.SetDword(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev", "AllowUninstall", 1);
            List<string> setups = new List<string>();
            setups.AddRange(FindEdgeSetups(Path.Combine(Pf86, @"Microsoft\Edge\Application")));
            setups.AddRange(FindEdgeSetups(Path.Combine(Pf, @"Microsoft\Edge\Application")));
            for (int i = 0; i < setups.Count && i < 2; i++)
            {
                c.Info("  running the Edge uninstaller...", "Edge");
                c.Tool(setups[i], "--uninstall --system-level --verbose-logging --force-uninstall");
                if (!EdgeInstalled()) break;
            }
            foreach (string e in FindEdgeSetups(Path.Combine(Local, @"Microsoft\Edge\Application")))
                c.Tool(e, "--uninstall --force-uninstall");
            foreach (string n in new string[] { "msedge", "MicrosoftEdge" }) SysUtil.KillByName(n);
            List<string> dirs = new List<string> {
                Path.Combine(Pf86, @"Microsoft\Edge"), Path.Combine(Pf, @"Microsoft\Edge"), Path.Combine(Pf86, @"Microsoft\EdgeCore"), Path.Combine(Pf, @"Microsoft\EdgeCore"),
                Path.Combine(Local, @"Microsoft\Edge"), Path.Combine(ProgData, @"Microsoft\Edge") };
            if (!webView) { dirs.Add(Path.Combine(Pf86, @"Microsoft\EdgeUpdate")); dirs.Add(Path.Combine(Pf, @"Microsoft\EdgeUpdate")); dirs.Add(Path.Combine(ProgData, @"Microsoft\EdgeUpdate")); }
            foreach (string d in dirs) if (Directory.Exists(d)) { c.Info("  deleting " + d, "Edge"); RemovePathForce(c, d); }
            DeleteService(c, "MicrosoftEdgeElevationService");
            if (!webView) { DeleteService(c, "edgeupdate"); DeleteService(c, "edgeupdatem"); TaskSched.DeleteByPrefix("MicrosoftEdgeUpdate"); }
            List<string> reg = new List<string> {
                @"HKLM:\SOFTWARE\Microsoft\Edge", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Edge", @"HKCU:\SOFTWARE\Microsoft\Edge",
                @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge",
                @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe" };
            foreach (string g in EdgeGuids)
                foreach (string root in new string[] { @"HKLM:\SOFTWARE\Microsoft\EdgeUpdate", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate" })
                { reg.Add(root + @"\Clients\" + g); reg.Add(root + @"\ClientState\" + g); }
            if (!webView) { reg.Add(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdate"); reg.Add(@"HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate"); }
            foreach (string k in reg) Reg.DeleteKey(k);
            RemoveShortcutsNamed("Microsoft Edge*.lnk");
            try
            {
                foreach (AppxInfo p in Appx.List())
                    if (p.Name == "Microsoft.MicrosoftEdge.Stable" || p.Name == "Microsoft.MicrosoftEdge" || p.Name == "Microsoft.MicrosoftEdgeDevToolsClient")
                        try { Appx.Remove(p.FullName); } catch { }
            }
            catch { }
            string pol = @"HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate";
            foreach (string g in EdgeGuids) { Reg.SetDword(pol, "Install" + g, 0); Reg.SetDword(pol, "Update" + g, 0); }
            Reg.SetDword(pol, "DoNotUpdateToEdgeWithChromium", 1);
            if (EdgeInstalled()) c.Warn("Edge is still partly present - restart Windows and run this again.", "Edge");
            else c.Info("Microsoft Edge was removed completely.", "Edge");
        }

        public static void RestoreEdge(WorkCtx c)
        {
            string pol = @"HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate";
            foreach (string g in EdgeGuids) { Reg.DeleteValue(pol, "Install" + g); Reg.DeleteValue(pol, "Update" + g); }
            Reg.DeleteValue(pol, "DoNotUpdateToEdgeWithChromium");
            Reg.DeleteValue(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdate", "DoNotUpdateToEdgeWithChromium");
            Reg.DeleteValue(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev", "AllowUninstall");
            string wg = FindWinget();
            if (wg != null) c.Tool(wg, "install --id Microsoft.Edge -e --silent --accept-source-agreements --accept-package-agreements --force");
            else c.Warn("winget is not available - download Edge from https://www.microsoft.com/edge", "Edge");
        }

        // ---------- Internet Explorer ----------
        private static string IeFeature()
        {
            string a = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
            if (a == "ARM64") return "Internet-Explorer-Optional-arm64";
            if (a == "x86") return "Internet-Explorer-Optional-x86";
            return "Internet-Explorer-Optional-amd64";
        }

        public static void RemoveIE(WorkCtx c)
        {
            c.Info("Removing Internet Explorer completely...", "IE");
            SysUtil.KillByName("iexplore"); SysUtil.KillByName("ielowutil");
            bool reboot = false;
            string dism = Tool.System32("dism.exe");
            foreach (string f in new string[] { IeFeature(), "Internet-Explorer-Optional-x86" })
            {
                c.Info("  disabling feature " + f, "IE");
                if (c.Tool(dism, "/Online /Disable-Feature /FeatureName:" + f + " /NoRestart /Quiet") == 3010) reboot = true;
                if (f == "Internet-Explorer-Optional-x86" && IeFeature() == f) break;
            }
            c.Info("  removing the Internet Explorer capability", "IE");
            if (c.Tool(dism, "/Online /Remove-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0 /NoRestart /Quiet") == 3010) reboot = true;
            foreach (string b in new string[] { Path.Combine(Pf, "Internet Explorer"), Path.Combine(Pf86, "Internet Explorer") })
            {
                string exe = Path.Combine(b, "iexplore.exe");
                if (File.Exists(exe)) { c.Info("  deleting " + exe, "IE"); RemovePathForce(c, exe); }
                try { if (Directory.Exists(b) && Directory.GetFileSystemEntries(b).Length == 0) RemovePathForce(c, b); } catch { }
            }
            RemoveShortcutsNamed("Internet Explorer*.lnk");
            if (reboot) c.Warn("Restart Windows to finish removing Internet Explorer.", "IE");
            else c.Info("Internet Explorer was removed (the MSHTML engine is part of Windows and stays, Edge IE-mode needs it).", "IE");
        }

        public static void RestoreIE(WorkCtx c)
        {
            string dism = Tool.System32("dism.exe");
            c.Tool(dism, "/Online /Enable-Feature /FeatureName:" + IeFeature() + " /All /NoRestart /Quiet");
            c.Tool(dism, "/Online /Add-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0 /NoRestart /Quiet");
            c.Info("Internet Explorer was restored - restart Windows to finish.", "IE");
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.IO;

namespace RockToolbox.Native
{
    // OneDrive / Microsoft Edge / Internet Explorer: complete removal and restore (C# port of the PowerShell helpers).
    internal static class Removers
    {
        private static string Pf86 { get { return Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? ""; } }
        private static string Pf { get { return Environment.GetEnvironmentVariable("ProgramFiles") ?? ""; } }
        private static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        private static string Win { get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); } }
        private static string ProgData { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); } }

        // ---------- shared helpers ----------
        public static void RemovePathForce(WorkCtx c, string p)
        {
            if (!File.Exists(p) && !Directory.Exists(p)) return;
            FastFs.DeleteAny(p);
            if (!File.Exists(p) && !Directory.Exists(p)) return;
            c.Tool(Tool.System32("takeown.exe"), "/F \"" + p + "\" /R /D Y");
            c.Tool(Tool.System32("icacls.exe"), "\"" + p + "\" /grant *S-1-5-32-544:F /T /C /Q");
            FastFs.DeleteAny(p);
            if (Directory.Exists(p)) c.Tool(Environment.GetEnvironmentVariable("ComSpec"), "/c rd /s /q \"" + p + "\"");
        }

        public static void RemoveShortcutsNamed(string pattern)
        {
            List<string> dirs = new List<string>();
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"));
            foreach (string d in dirs)
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    foreach (string f in Directory.GetFiles(d, pattern, SearchOption.AllDirectories))
                        try { File.Delete(f); } catch { }
                }
                catch { }
            }
        }

        public static void DeleteService(WorkCtx c, string name)
        {
            c.Tool(Tool.System32("sc.exe"), "stop " + name);
            c.Tool(Tool.System32("sc.exe"), "delete " + name);
        }

        // ---------- winget ----------
        public static string FindWinget()
        {
            string p = Path.Combine(Local, @"Microsoft\WindowsApps\winget.exe");
            if (File.Exists(p)) return p;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string d in path.Split(';'))
            {
                try { string f = Path.Combine(d.Trim(), "winget.exe"); if (d.Trim().Length > 0 && File.Exists(f)) return f; } catch { }
            }
            return null;
        }

        // ---------- OneDrive ----------
        public static bool OneDriveInstalled()
        {
            string[] ps = {
                Path.Combine(Local, @"Microsoft\OneDrive\OneDrive.exe"), Path.Combine(Pf, @"Microsoft OneDrive\OneDrive.exe"),
                Path.Combine(Pf86, @"Microsoft OneDrive\OneDrive.exe"), Path.Combine(Win, @"SysWOW64\OneDriveSetup.exe"),
                Path.Combine(Win, @"System32\OneDriveSetup.exe") };
            foreach (string p in ps) if (File.Exists(p)) return true;
            return false;
        }

        public static void RemoveOneDrive(WorkCtx c)
        {
            c.Info("Removing OneDrive completely...", "OneDrive");
            foreach (string n in new string[] { "OneDrive", "OneDriveSetup", "FileCoAuth", "FileSyncHelper" }) SysUtil.KillByName(n);
            Reg.SetDword(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1);
            List<string> setups = new List<string>();
            foreach (string p in new string[] { Path.Combine(Win, @"SysWOW64\OneDriveSetup.exe"), Path.Combine(Win, @"System32\OneDriveSetup.exe") })
                if (File.Exists(p)) setups.Add(p);
            foreach (string b in new string[] { Path.Combine(Local, @"Microsoft\OneDrive"), Path.Combine(Pf, "Microsoft OneDrive"), Path.Combine(Pf86, "Microsoft OneDrive") })
            {
                try { if (Directory.Exists(b)) foreach (string f in Directory.GetFiles(b, "OneDriveSetup.exe", SearchOption.AllDirectories)) if (!setups.Contains(f)) setups.Add(f); } catch { }
            }
            foreach (string e in setups)
            {
                c.Info("  running " + e + " /uninstall", "OneDrive");
                c.Tool(e, "/uninstall");
                c.Tool(e, "/uninstall /allusers");
            }
            string wg = FindWinget();
            if (wg != null) c.Tool(wg, "uninstall --id Microsoft.OneDrive -e --silent --disable-interactivity --accept-source-agreements");
            foreach (string n in new string[] { "OneDrive", "OneDriveSetup" }) SysUtil.KillByName(n);
            foreach (string d in new string[] { Path.Combine(Local, @"Microsoft\OneDrive"), Path.Combine(ProgData, "Microsoft OneDrive"),
                Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", "OneDriveTemp"), Path.Combine(Pf, "Microsoft OneDrive"), Path.Combine(Pf86, "Microsoft OneDrive") })
                if (Directory.Exists(d)) { c.Info("  deleting " + d, "OneDrive"); RemovePathForce(c, d); }
            RemoveShortcutsNamed("OneDrive*.lnk");
            TaskSched.DeleteByPrefix("OneDrive");
            foreach (string rk in new string[] { @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" })
                foreach (string vn in new string[] { "OneDrive", "OneDriveSetup" }) Reg.DeleteValue(rk, vn);
            foreach (string k in new string[] { @"HKCR:\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", @"HKCR:\Wow6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}" })
                if (Reg.KeyExists(k)) { try { Reg.SetDword(k, "System.IsPinnedToNameSpaceTree", 0); } catch { } }
            Reg.DeleteKey(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{018D5C66-4533-4307-9B53-224DE2ED1FE6}");
            Reg.DeleteKey(@"HKCU:\Software\Microsoft\OneDrive");
            string hive = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", @"Users\Default\NTUSER.DAT");
            if (File.Exists(hive))
            {
                int code = c.Tool(Tool.System32("reg.exe"), "load HKU\\RockDefaultUser \"" + hive + "\"");
                if (code == 0)
                {
                    c.Tool(Tool.System32("reg.exe"), "delete \"HKU\\RockDefaultUser\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v OneDriveSetup /f");
                    GC.Collect(); GC.WaitForPendingFinalizers();
                    c.Tool(Tool.System32("reg.exe"), "unload HKU\\RockDefaultUser");
                }
            }
            foreach (string p in new string[] { Path.Combine(Win, @"SysWOW64\OneDriveSetup.exe"), Path.Combine(Win, @"System32\OneDriveSetup.exe") })
                if (File.Exists(p)) { c.Info("  deleting " + p, "OneDrive"); RemovePathForce(c, p); }
            // the user's OneDrive folder holds YOUR files: only deleted when empty
            List<string> od = new List<string>();
            foreach (string v in new string[] { Environment.GetEnvironmentVariable("OneDrive"), Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"), Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "OneDrive") })
                if (!string.IsNullOrEmpty(v) && !od.Contains(v)) od.Add(v);
            foreach (string d in od)
            {
                if (!Directory.Exists(d)) continue;
                if (Directory.GetFileSystemEntries(d).Length == 0) { try { Directory.Delete(d); } catch { } }
                else c.Warn("  kept " + d + " - it still holds your files (move them out, then delete the folder yourself).", "OneDrive");
            }
            if (OneDriveInstalled()) c.Warn("OneDrive is still partly present - restart Windows and run this again.", "OneDrive");
            else c.Info("OneDrive was removed completely.", "OneDrive");
        }

        public static void RestoreOneDrive(WorkCtx c)
        {
            Reg.DeleteValue(@"HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC");
            string wg = FindWinget();
            if (wg != null) c.Tool(wg, "install --id Microsoft.OneDrive -e --silent --accept-source-agreements --accept-package-agreements");
            else c.Warn("winget is not available - install OneDrive from https://www.microsoft.com/microsoft-365/onedrive/download", "OneDrive");
        }

        // ---------- Edge ----------
        private static readonly string[] EdgeGuids = {
            "{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", "{2CD8A007-E189-409D-A2C8-9AF4EF3C72AA}",
            "{0D50BFEC-CD6A-4F9A-964C-C7416E3ACB10}", "{65C35B14-6C1D-4122-AC46-7148CC9D6497}" };

        public static bool EdgeInstalled()
        {
            return File.Exists(Path.Combine(Pf86, @"Microsoft\Edge\Application\msedge.exe")) || File.Exists(Path.Combine(Pf, @"Microsoft\Edge\Application\msedge.exe"));
        }

        private static List<string> FindEdgeSetups(string baseDir)
        {
            List<string> r = new List<string>();
            try
            {
                if (Directory.Exists(baseDir))
                    foreach (string f in Directory.GetFiles(baseDir, "setup.exe", SearchOption.AllDirectories))
                        if (f.EndsWith(@"\Installer\setup.exe", StringComparison.OrdinalIgnoreCase)) r.Add(f);
            }
            catch { }
            r.Sort(); r.Reverse();
            return r;
        }

        public static void RemoveEdge(WorkCtx c)
        {
            c.Info("Removing Microsoft Edge completely...", "Edge");
            bool webView = Directory.Exists(Path.Combine(Pf86, @"Microsoft\EdgeWebView")) || Directory.Exists(Path.Combine(Pf, @"Microsoft\EdgeWebView"));
            if (webView) c.Info("  the WebView2 runtime is kept (many apps need it).", "Edge");
            foreach (string n in new string[] { "msedge", "MicrosoftEdge", "MicrosoftEdgeCP", "MicrosoftEdgeSH", "msedge_proxy", "pwahelper", "cookie_exporter", "notification_helper" }) SysUtil.KillByName(n);
            Reg.SetDword(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev", "AllowUninstall", 1);
            List<string> setups = new List<string>();
            setups.AddRange(FindEdgeSetups(Path.Combine(Pf86, @"Microsoft\Edge\Application")));
            setups.AddRange(FindEdgeSetups(Path.Combine(Pf, @"Microsoft\Edge\Application")));
            for (int i = 0; i < setups.Count && i < 2; i++)
            {
                c.Info("  running the Edge uninstaller...", "Edge");
                c.Tool(setups[i], "--uninstall --system-level --verbose-logging --force-uninstall");
                if (!EdgeInstalled()) break;
            }
            foreach (string e in FindEdgeSetups(Path.Combine(Local, @"Microsoft\Edge\Application")))
                c.Tool(e, "--uninstall --force-uninstall");
            foreach (string n in new string[] { "msedge", "MicrosoftEdge" }) SysUtil.KillByName(n);
            List<string> dirs = new List<string> {
                Path.Combine(Pf86, @"Microsoft\Edge"), Path.Combine(Pf, @"Microsoft\Edge"), Path.Combine(Pf86, @"Microsoft\EdgeCore"), Path.Combine(Pf, @"Microsoft\EdgeCore"),
                Path.Combine(Local, @"Microsoft\Edge"), Path.Combine(ProgData, @"Microsoft\Edge") };
            if (!webView) { dirs.Add(Path.Combine(Pf86, @"Microsoft\EdgeUpdate")); dirs.Add(Path.Combine(Pf, @"Microsoft\EdgeUpdate")); dirs.Add(Path.Combine(ProgData, @"Microsoft\EdgeUpdate")); }
            foreach (string d in dirs) if (Directory.Exists(d)) { c.Info("  deleting " + d, "Edge"); RemovePathForce(c, d); }
            DeleteService(c, "MicrosoftEdgeElevationService");
            if (!webView) { DeleteService(c, "edgeupdate"); DeleteService(c, "edgeupdatem"); TaskSched.DeleteByPrefix("MicrosoftEdgeUpdate"); }
            List<string> reg = new List<string> {
                @"HKLM:\SOFTWARE\Microsoft\Edge", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Edge", @"HKCU:\SOFTWARE\Microsoft\Edge",
                @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge",
                @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe" };
            foreach (string g in EdgeGuids)
                foreach (string root in new string[] { @"HKLM:\SOFTWARE\Microsoft\EdgeUpdate", @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate" })
                { reg.Add(root + @"\Clients\" + g); reg.Add(root + @"\ClientState\" + g); }
            if (!webView) { reg.Add(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdate"); reg.Add(@"HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate"); }
            foreach (string k in reg) Reg.DeleteKey(k);
            RemoveShortcutsNamed("Microsoft Edge*.lnk");
            try
            {
                foreach (AppxInfo p in Appx.List())
                    if (p.Name == "Microsoft.MicrosoftEdge.Stable" || p.Name == "Microsoft.MicrosoftEdge" || p.Name == "Microsoft.MicrosoftEdgeDevToolsClient")
                        try { Appx.Remove(p.FullName); } catch { }
            }
            catch { }
            string pol = @"HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate";
            foreach (string g in EdgeGuids) { Reg.SetDword(pol, "Install" + g, 0); Reg.SetDword(pol, "Update" + g, 0); }
            Reg.SetDword(pol, "DoNotUpdateToEdgeWithChromium", 1);
            if (EdgeInstalled()) c.Warn("Edge is still partly present - restart Windows and run this again.", "Edge");
            else c.Info("Microsoft Edge was removed completely.", "Edge");
        }

        public static void RestoreEdge(WorkCtx c)
        {
            string pol = @"HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate";
            foreach (string g in EdgeGuids) { Reg.DeleteValue(pol, "Install" + g); Reg.DeleteValue(pol, "Update" + g); }
            Reg.DeleteValue(pol, "DoNotUpdateToEdgeWithChromium");
            Reg.DeleteValue(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdate", "DoNotUpdateToEdgeWithChromium");
            Reg.DeleteValue(@"HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev", "AllowUninstall");
            string wg = FindWinget();
            if (wg != null) c.Tool(wg, "install --id Microsoft.Edge -e --silent --accept-source-agreements --accept-package-agreements --force");
            else c.Warn("winget is not available - download Edge from https://www.microsoft.com/edge", "Edge");
        }

        // ---------- Internet Explorer ----------
        private static string IeFeature()
        {
            string a = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
            if (a == "ARM64") return "Internet-Explorer-Optional-arm64";
            if (a == "x86") return "Internet-Explorer-Optional-x86";
            return "Internet-Explorer-Optional-amd64";
        }

        public static void RemoveIE(WorkCtx c)
        {
            c.Info("Removing Internet Explorer completely...", "IE");
            SysUtil.KillByName("iexplore"); SysUtil.KillByName("ielowutil");
            bool reboot = false;
            string dism = Tool.System32("dism.exe");
            foreach (string f in new string[] { IeFeature(), "Internet-Explorer-Optional-x86" })
            {
                c.Info("  disabling feature " + f, "IE");
                if (c.Tool(dism, "/Online /Disable-Feature /FeatureName:" + f + " /NoRestart /Quiet") == 3010) reboot = true;
                if (f == "Internet-Explorer-Optional-x86" && IeFeature() == f) break;
            }
            c.Info("  removing the Internet Explorer capability", "IE");
            if (c.Tool(dism, "/Online /Remove-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0 /NoRestart /Quiet") == 3010) reboot = true;
            foreach (string b in new string[] { Path.Combine(Pf, "Internet Explorer"), Path.Combine(Pf86, "Internet Explorer") })
            {
                string exe = Path.Combine(b, "iexplore.exe");
                if (File.Exists(exe)) { c.Info("  deleting " + exe, "IE"); RemovePathForce(c, exe); }
                try { if (Directory.Exists(b) && Directory.GetFileSystemEntries(b).Length == 0) RemovePathForce(c, b); } catch { }
            }
            RemoveShortcutsNamed("Internet Explorer*.lnk");
            if (reboot) c.Warn("Restart Windows to finish removing Internet Explorer.", "IE");
            else c.Info("Internet Explorer was removed (the MSHTML engine is part of Windows and stays, Edge IE-mode needs it).", "IE");
        }

        public static void RestoreIE(WorkCtx c)
        {
            string dism = Tool.System32("dism.exe");
            c.Tool(dism, "/Online /Enable-Feature /FeatureName:" + IeFeature() + " /All /NoRestart /Quiet");
            c.Tool(dism, "/Online /Add-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0 /NoRestart /Quiet");
            c.Info("Internet Explorer was restored - restart Windows to finish.", "IE");
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
