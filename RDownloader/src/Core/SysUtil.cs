<<<<<<< HEAD
﻿using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace RDownloader.Native
{
    // Services, folders, processes: small system helpers shared by the pages.
    internal static class SysUtil
    {
        public static bool IsWin11 { get { return Environment.OSVersion.Version.Build >= 22000; } }

        public static void StopService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch { }
        }

        public static void StartService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch { }
        }


        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static void EmptyRecycleBin() { SHEmptyRecycleBin(IntPtr.Zero, null, 7); }

        // mode: "Automatic" | "Manual" | "Disabled"  (silently ignores services that do not exist)
        public static void SetStartMode(string name, string mode)
        {
            try
            {
                using (ManagementObject o = new ManagementObject("Win32_Service.Name='" + name + "'"))
                    o.InvokeMethod("ChangeStartMode", new object[] { mode });
            }
            catch { }
        }

        public static void DisableService(string name) { StopService(name); SetStartMode(name, "Disabled"); }

        // after a registry tweak: restart Explorer when "instant apply" is on
        public static void RefreshShell()
        {
            if (!Ui.Settings.GetBool("InstantApply", false)) return;
            KillByName("explorer");
        }

        public static void ForceDeleteDir(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                Directory.Delete(path, true);
            }
            catch { }
        }

        public static void KillByName(string name)
        {
            foreach (Process p in Process.GetProcessesByName(name))
                try { p.Kill(); p.WaitForExit(3000); } catch { }
        }

        // Reset-WindowsUpdate: stop the services, move SoftwareDistribution / catroot2 aside, start the services again
        public static void ResetWindowsUpdate(WorkCtx ctx)
        {
            ctx.Info("Resetting Windows Update components...", "Update");
            string[] svcs = { "wuauserv", "bits", "cryptsvc", "msiserver" };
            foreach (string s in svcs) StopService(s);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sd = Path.Combine(win, "SoftwareDistribution");
            string cr = Path.Combine(Path.Combine(win, "System32"), "catroot2");
            ForceDeleteDir(sd + ".bak");
            ForceDeleteDir(cr + ".bak");
            try { if (Directory.Exists(sd)) Directory.Move(sd, sd + ".bak"); } catch { }
            try { if (Directory.Exists(cr)) Directory.Move(cr, cr + ".bak"); } catch { }
            foreach (string s in svcs) StartService(s);
            ctx.Info("Update components reset.", "Update");
        }
    }
}
=======
﻿using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace RDownloader.Native
{
    // Services, folders, processes: small system helpers shared by the pages.
    internal static class SysUtil
    {
        public static bool IsWin11 { get { return Environment.OSVersion.Version.Build >= 22000; } }

        public static void StopService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch { }
        }

        public static void StartService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch { }
        }


        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static void EmptyRecycleBin() { SHEmptyRecycleBin(IntPtr.Zero, null, 7); }

        // mode: "Automatic" | "Manual" | "Disabled"  (silently ignores services that do not exist)
        public static void SetStartMode(string name, string mode)
        {
            try
            {
                using (ManagementObject o = new ManagementObject("Win32_Service.Name='" + name + "'"))
                    o.InvokeMethod("ChangeStartMode", new object[] { mode });
            }
            catch { }
        }

        public static void DisableService(string name) { StopService(name); SetStartMode(name, "Disabled"); }

        // after a registry tweak: restart Explorer when "instant apply" is on
        public static void RefreshShell()
        {
            if (!Ui.Settings.GetBool("InstantApply", false)) return;
            KillByName("explorer");
        }

        public static void ForceDeleteDir(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                Directory.Delete(path, true);
            }
            catch { }
        }

        public static void KillByName(string name)
        {
            foreach (Process p in Process.GetProcessesByName(name))
                try { p.Kill(); p.WaitForExit(3000); } catch { }
        }

        // Reset-WindowsUpdate: stop the services, move SoftwareDistribution / catroot2 aside, start the services again
        public static void ResetWindowsUpdate(WorkCtx ctx)
        {
            ctx.Info("Resetting Windows Update components...", "Update");
            string[] svcs = { "wuauserv", "bits", "cryptsvc", "msiserver" };
            foreach (string s in svcs) StopService(s);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sd = Path.Combine(win, "SoftwareDistribution");
            string cr = Path.Combine(Path.Combine(win, "System32"), "catroot2");
            ForceDeleteDir(sd + ".bak");
            ForceDeleteDir(cr + ".bak");
            try { if (Directory.Exists(sd)) Directory.Move(sd, sd + ".bak"); } catch { }
            try { if (Directory.Exists(cr)) Directory.Move(cr, cr + ".bak"); } catch { }
            foreach (string s in svcs) StartService(s);
            ctx.Info("Update components reset.", "Update");
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
