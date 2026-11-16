using System;
using System.IO;
using System.Text;

namespace RockToolbox.Native
{
    // A fresh Windows often has no winget (App Installer) and no Chocolatey. These helpers install them on demand,
    // so the Install page works on a clean PC without the user doing anything.
    internal static class PkgBootstrap
    {
        private const string Gh = "https://github.com/microsoft/winget-cli/releases/latest/download/";

        private static string Ps { get { return Tool.System32(@"WindowsPowerShell\v1.0\powershell.exe"); } }

        private static int RunPs(WorkCtx c, string script)
        {
            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; " + script));
            return c.Tool(Ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, delegate(string t) { foreach (string l in t.Split('\n')) { string s = l.Trim(); if (s.Length > 2) c.Info("  " + s, "Winget"); } }, Encoding.UTF8);
        }

        private static bool Get(WorkCtx c, string url, string file)
        {
            try
            {
                c.Info("  downloading " + Path.GetFileName(file) + " ...", "Winget");
                long last = 0;
                Net.Download(url, file, delegate(long got, long total) { if (got - last > 8 * 1024 * 1024) { last = got; c.Info("  " + (got / 1048576) + (total > 0 ? " / " + (total / 1048576) : "") + " MB", "Winget"); } }, delegate { return Work.CancelRequested; });
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { c.Warn("  download failed: " + Net.FriendlyError(ex), "Winget"); return false; }
        }

        // downloads App Installer (winget) + its libraries and installs them for the current user
        public static string InstallWinget(WorkCtx c)
        {
            c.Info("winget is missing on this PC - installing App Installer (this can take a few minutes)...", "Winget");
            string dir = Path.Combine(Path.GetTempPath(), "rock_winget_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            try
            {
                Directory.CreateDirectory(dir);
                string bundle = Path.Combine(dir, "AppInstaller.msixbundle"), deps = Path.Combine(dir, "deps.zip");
                if (!Get(c, Gh + "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle", bundle)) return null;
                string depList = "";
                if (Get(c, Gh + "DesktopAppInstaller_Dependencies.zip", deps))
                {
                    string x = Path.Combine(dir, "deps");
                    RunPs(c, "Expand-Archive -LiteralPath '" + deps + "' -DestinationPath '" + x + "' -Force");
                    string arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                    string d = Path.Combine(x, arch);
                    if (Directory.Exists(d))
                        foreach (string f in Directory.GetFiles(d, "*.appx")) depList += (depList.Length > 0 ? "," : "") + "'" + f + "'";
                }
                if (depList.Length == 0)
                {
                    string vc = Path.Combine(dir, "VCLibs.appx");
                    if (Get(c, "https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx", vc)) depList = "'" + vc + "'";
                }
                string cmd = "Add-AppxPackage -Path '" + bundle + "'" + (depList.Length > 0 ? " -DependencyPath @(" + depList + ")" : "") + " -ForceApplicationShutdown";
                int code = RunPs(c, cmd);
                if (code != 0) c.Warn("  Add-AppxPackage exit code " + code, "Winget");
                for (int i = 0; i < 20; i++)
                {
                    string wg = Removers.FindWinget();
                    if (wg != null) { c.Info("winget is ready.", "Winget"); return wg; }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { c.Warn("Could not install winget: " + ex.Message, "Winget"); }
            finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
            return null;
        }

        public static string ChocoPath()
        {
            string p = Path.Combine(Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData", @"chocolatey\bin\choco.exe");
            return File.Exists(p) ? p : null;
        }

        // official installer script from chocolatey.org
        public static string InstallChoco(WorkCtx c)
        {
            c.Info("Chocolatey is missing on this PC - installing it...", "Pkg");
            try
            {
                int code = RunPs(c, "Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))");
                if (code != 0) c.Warn("  Chocolatey installer exit code " + code, "Pkg");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { c.Warn("Could not install Chocolatey: " + ex.Message, "Pkg"); }
            return ChocoPath();
        }
    }
}
