using System;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    // Registry helpers. Paths use the PowerShell style ("HKLM:\SOFTWARE\...") the old tweak data used.
    internal static class Reg
    {
        private static RegistryKey Root(string path, out string sub)
        {
            string p = path.Replace("/", "\\");
            int i = p.IndexOf('\\');
            string hive = (i < 0 ? p : p.Substring(0, i)).TrimEnd(':').ToUpperInvariant();
            sub = i < 0 ? "" : p.Substring(i + 1);
            RegistryHive h;
            switch (hive)
            {
                case "HKLM": case "HKEY_LOCAL_MACHINE": h = RegistryHive.LocalMachine; break;
                case "HKCU": case "HKEY_CURRENT_USER": h = RegistryHive.CurrentUser; break;
                case "HKCR": case "HKEY_CLASSES_ROOT": h = RegistryHive.ClassesRoot; break;
                case "HKU": case "HKEY_USERS": h = RegistryHive.Users; break;
                default: throw new ArgumentException("Unknown registry hive: " + path);
            }
            return RegistryKey.OpenBaseKey(h, RegistryView.Registry64);
        }

        public static void Set(string path, string name, object value, RegistryValueKind kind)
        {
            try
            {
                string sub;
                using (RegistryKey root = Root(path, out sub))
                using (RegistryKey k = root.CreateSubKey(sub))
                    k.SetValue(name, value, kind);
            }
            catch (Exception ex)
            {
                if (!(ex is UnauthorizedAccessException) && !(ex is System.Security.SecurityException)) throw;
                if (!SetViaRegExe(path, name, value, kind) && !SetAsSystem(path, name, value, kind)) throw;
            }
        }

        // Some keys (Widgets / news policies, parts of the taskbar) refuse even an administrator. Windows lets SYSTEM write them,
        // so the value is written by a one-time scheduled task that runs as SYSTEM, then the task is removed. Checked by reading it back.
        private static bool _sysTried;

        // reg.exe is a different program, so a protection that blocks this one sometimes lets it through (fast: no waiting)
        private static bool SetViaRegExe(string path, string name, object value, RegistryValueKind kind)
        {
            try
            {
                string p = path.Replace("/", "\\"); int i = p.IndexOf('\\');
                string hive = (i < 0 ? p : p.Substring(0, i)).TrimEnd(':').ToUpperInvariant();
                string full = (hive == "HKCU" ? "HKCU" : hive == "HKLM" ? "HKLM" : "") + "\\" + (i < 0 ? "" : p.Substring(i + 1));
                if (full.StartsWith("\\")) return false;
                string t = kind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                string d = kind == RegistryValueKind.DWord ? unchecked((uint)Convert.ToInt32(value)).ToString() : Convert.ToString(value);
                int code;
                Tool.Capture("reg.exe", "add \"" + full + "\" /v \"" + name + "\" /t " + t + " /d \"" + d + "\" /f /reg:64", out code);
                return code == 0;
            }
            catch (Exception) { return false; }
        }

        private static bool SetAsSystem(string path, string name, object value, RegistryValueKind kind)
        {
            if (_sysTried) return false;       // it did not work once: do not make every further change wait again
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rock-reg-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string task = "RockToolboxReg" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                string p = path.Replace("/", "\\");
                int i = p.IndexOf('\\');
                string hive = (i < 0 ? p : p.Substring(0, i)).TrimEnd(':').ToUpperInvariant(), sub2 = i < 0 ? "" : p.Substring(i + 1);
                string full;
                if (hive == "HKCU") full = "HKU\\" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value + "\\" + sub2;
                else if (hive == "HKLM") full = "HKLM\\" + sub2;
                else return false;
                string t, d;
                if (kind == RegistryValueKind.DWord) { t = "REG_DWORD"; d = unchecked((uint)Convert.ToInt32(value)).ToString(); }
                else { t = "REG_SZ"; d = Convert.ToString(value); }
                string cmd = "@echo off\r\nreg add \"" + full + "\" /v \"" + name + "\" /t " + t + " /d \"" + d + "\" /f /reg:64\r\n";
                System.IO.File.WriteAllText(tmp + ".cmd", cmd);
                int code;
                Tool.Capture("schtasks.exe", "/create /tn " + task + " /tr \"\\\"" + tmp + ".cmd\\\"\" /sc once /st 00:00 /ru SYSTEM /rl HIGHEST /f", out code);
                if (code != 0) return false;
                Tool.Capture("schtasks.exe", "/run /tn " + task, out code);
                for (int n = 0; n < 12; n++)
                {
                    System.Threading.Thread.Sleep(250);
                    object now = Get(path, name);
                    if (now != null && Convert.ToString(now) == Convert.ToString(value)) return true;
                    if (now != null && kind == RegistryValueKind.DWord) { try { if (Convert.ToInt32(now) == Convert.ToInt32(value)) return true; } catch (Exception) { } }
                }
                _sysTried = true;
                return false;
            }
            catch (Exception) { _sysTried = true; return false; }
            finally
            {
                try { int c2; Tool.Capture("schtasks.exe", "/delete /tn " + task + " /f", out c2); } catch (Exception) { }
                try { System.IO.File.Delete(tmp + ".cmd"); } catch (Exception) { }
            }
        }

        public static void SetDword(string path, string name, int value) { Set(path, name, value, RegistryValueKind.DWord); }
        public static void SetString(string path, string name, string value) { Set(path, name, value, RegistryValueKind.String); }

        public static object Get(string path, string name)
        {
            try
            {
                string sub;
                using (RegistryKey root = Root(path, out sub))
                using (RegistryKey k = root.OpenSubKey(sub))
                    return k == null ? null : k.GetValue(name);
            }
            catch { return null; }
        }

        public static int GetInt(string path, string name, int fallback)
        {
            object o = Get(path, name);
            if (o == null) return fallback;
            try { return Convert.ToInt32(o); } catch { return fallback; }
        }

        public static void DeleteValue(string path, string name)
        {
            try
            {
                string sub;
                using (RegistryKey root = Root(path, out sub))
                using (RegistryKey k = root.OpenSubKey(sub, true))
                    if (k != null) k.DeleteValue(name, false);
            }
            catch { }
        }

        public static void DeleteKey(string path)
        {
            try
            {
                string sub;
                using (RegistryKey root = Root(path, out sub))
                    root.DeleteSubKeyTree(sub, false);
            }
            catch { }
        }

        public static bool KeyExists(string path)
        {
            try
            {
                string sub;
                using (RegistryKey root = Root(path, out sub))
                using (RegistryKey k = root.OpenSubKey(sub))
                    return k != null;
            }
            catch { return false; }
        }
    }
}
