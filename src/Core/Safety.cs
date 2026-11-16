<<<<<<< HEAD
﻿using System;
using System.Management;

namespace RockToolbox.Native
{
    // Automatic restore point before risky actions (WMI SystemRestore - no PowerShell).
    internal static class Safety
    {
        private static DateTime _last = DateTime.MinValue;
        private const string Key = @"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";

        // true = fine to continue (restore point made, skipped by setting, or the user chose to go on)
        public static bool NewRestorePoint(WorkCtx c, string reason)
        {
            if (!Ui.Settings.GetBool("AutoRestore", true)) return true;
            if ((DateTime.Now - _last).TotalMinutes < 20) return true;
            c.Info("Creating a restore point before " + reason + " (this can take a minute)...", "Safety");
            c.Progress(-1, "Creating a restore point...");
            string err = CreateNow("Rock Toolbox - before " + reason);
            if (err.Length == 0)
            {
                _last = DateTime.Now;
                c.Info("Restore point created.", "Safety");
                return true;
            }
            c.Warn("Could not create a restore point: " + err, "Safety");
            return Ui.Confirm("Rock Toolbox could not create a restore point:\n\n" + err + "\n\nContinue without one?");
        }

        // creates a restore point right now; returns "" on success or the error text
        public static string CreateNow(string description)
        {
            string err = "";
            object old = Reg.Get(Key, "SystemRestorePointCreationFrequency");
            try
            {
                Reg.SetDword(Key, "SystemRestorePointCreationFrequency", 0);
                try
                {
                    ManagementClass mc0 = new ManagementClass(@"\\.\root\default:SystemRestore");
                    mc0.InvokeMethod("Enable", new object[] { Environment.GetEnvironmentVariable("SystemDrive") + "\\", true });
                }
                catch { }
                ManagementClass mc = new ManagementClass(@"\\.\root\default:SystemRestore");
                object r = mc.InvokeMethod("CreateRestorePoint", new object[] { description, 12, 100 });
                int code = Convert.ToInt32(r);
                if (code != 0) err = "error code " + code;
            }
            catch (Exception ex) { err = ex.Message; }
            finally
            {
                try
                {
                    if (old != null) Reg.SetDword(Key, "SystemRestorePointCreationFrequency", Convert.ToInt32(old));
                    else Reg.DeleteValue(Key, "SystemRestorePointCreationFrequency");
                }
                catch { }
            }
            return err;
        }
    }

}
=======
﻿using System;
using System.Management;

namespace RockToolbox.Native
{
    // Automatic restore point before risky actions (WMI SystemRestore - no PowerShell).
    internal static class Safety
    {
        private static DateTime _last = DateTime.MinValue;
        private const string Key = @"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";

        // true = fine to continue (restore point made, skipped by setting, or the user chose to go on)
        public static bool NewRestorePoint(WorkCtx c, string reason)
        {
            if (!Ui.Settings.GetBool("AutoRestore", true)) return true;
            if ((DateTime.Now - _last).TotalMinutes < 20) return true;
            c.Info("Creating a restore point before " + reason + " (this can take a minute)...", "Safety");
            c.Progress(-1, "Creating a restore point...");
            string err = CreateNow("Rock Toolbox - before " + reason);
            if (err.Length == 0)
            {
                _last = DateTime.Now;
                c.Info("Restore point created.", "Safety");
                return true;
            }
            c.Warn("Could not create a restore point: " + err, "Safety");
            return Ui.Confirm("Rock Toolbox could not create a restore point:\n\n" + err + "\n\nContinue without one?");
        }

        // creates a restore point right now; returns "" on success or the error text
        public static string CreateNow(string description)
        {
            string err = "";
            object old = Reg.Get(Key, "SystemRestorePointCreationFrequency");
            try
            {
                Reg.SetDword(Key, "SystemRestorePointCreationFrequency", 0);
                try
                {
                    ManagementClass mc0 = new ManagementClass(@"\\.\root\default:SystemRestore");
                    mc0.InvokeMethod("Enable", new object[] { Environment.GetEnvironmentVariable("SystemDrive") + "\\", true });
                }
                catch { }
                ManagementClass mc = new ManagementClass(@"\\.\root\default:SystemRestore");
                object r = mc.InvokeMethod("CreateRestorePoint", new object[] { description, 12, 100 });
                int code = Convert.ToInt32(r);
                if (code != 0) err = "error code " + code;
            }
            catch (Exception ex) { err = ex.Message; }
            finally
            {
                try
                {
                    if (old != null) Reg.SetDword(Key, "SystemRestorePointCreationFrequency", Convert.ToInt32(old));
                    else Reg.DeleteValue(Key, "SystemRestorePointCreationFrequency");
                }
                catch { }
            }
            return err;
        }
    }

}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
