using System;
using System.Collections.Generic;
using System.Text;

namespace RockToolbox.Native
{
    // Fallback for removing Store apps: the Windows built-in "Get-AppxPackage / Remove-AppxPackage" commands.
    // Used by Debloat when the direct (WinRT) way finds or removes nothing, which happens on some Windows builds.
    internal static class AppxPs
    {
        private static string PsExe
        {
            get
            {
                string p = System.IO.Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", @"System32\WindowsPowerShell\v1.0\powershell.exe");
                return System.IO.File.Exists(p) ? p : "powershell.exe";
            }
        }

        // names of every Store package installed on the PC (any user)
        public static List<string> ListNames(WorkCtx c)
        {
            List<string> names = new List<string>();
            StringBuilder buf = new StringBuilder();
            string script = "$ErrorActionPreference='SilentlyContinue'; Get-AppxPackage -AllUsers | ForEach-Object { Write-Output ('N|' + $_.Name) }";
            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            c.Tool(PsExe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, delegate(string chunk) { buf.Append(chunk); }, Encoding.UTF8);
            foreach (string l in buf.ToString().Split('\n')) { string t = l.Trim(); if (t.StartsWith("N|")) names.Add(t.Substring(2)); }
            return names;
        }

        // patterns use * and ? like the Debloat list does. Returns the patterns that are gone (or were not installed) afterwards.
        public static List<string> RemoveMany(WorkCtx c, List<string> patterns, Dictionary<string, string> labels)
        {
            List<string> ok = new List<string>();
            if (patterns.Count == 0) return ok;
            StringBuilder arr = new StringBuilder();
            foreach (string p in patterns) { if (arr.Length > 0) arr.Append(','); arr.Append('\'').Append(p.Replace("'", "''")).Append('\''); }
            string script =
                "$ErrorActionPreference='SilentlyContinue'; $ProgressPreference='SilentlyContinue';" +
                "$pats=@(" + arr + ");" +
                "$all=@(Get-AppxPackage -AllUsers); $prov=@(Get-AppxProvisionedPackage -Online);" +
                "foreach($p in $pats){" +
                " $hit=@($all | Where-Object { $_.Name -like $p }); $ph=@($prov | Where-Object { $_.DisplayName -like $p });" +
                " if($hit.Count -eq 0 -and $ph.Count -eq 0){ Write-Output ('NONE|'+$p); continue }" +
                " foreach($x in $hit){ Remove-AppxPackage -Package $x.PackageFullName -AllUsers -ErrorAction SilentlyContinue; if($?){}else{ Remove-AppxPackage -Package $x.PackageFullName -ErrorAction SilentlyContinue } }" +
                " foreach($x in $ph){ Remove-AppxProvisionedPackage -Online -PackageName $x.PackageName -ErrorAction SilentlyContinue | Out-Null }" +
                " $left=@(Get-AppxPackage -AllUsers | Where-Object { $_.Name -like $p }).Count;" +
                " if($left -eq 0){ Write-Output ('OK|'+$p) } else { Write-Output ('LEFT|'+$p) } }";
            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            StringBuilder buf = new StringBuilder();
            Action<string> take = delegate(string chunk)
            {
                buf.Append(chunk);
                while (true)
                {
                    string all = buf.ToString(); int nl = all.IndexOf('\n');
                    if (nl < 0) break;
                    string line = all.Substring(0, nl).Trim(); buf.Remove(0, nl + 1);
                    int bar = line.IndexOf('|'); if (bar < 0) continue;
                    string kind = line.Substring(0, bar), pat = line.Substring(bar + 1);
                    string lab; if (labels == null || !labels.TryGetValue(pat, out lab)) lab = pat;
                    if (kind == "OK") { ok.Add(pat); c.Info("  " + lab + ": removed", "Debloat"); }
                    else if (kind == "NONE") { ok.Add(pat); c.Info("  " + lab + ": (not installed)", "Debloat"); }
                    else if (kind == "LEFT") c.Warn("  " + lab + ": could not be removed (Windows keeps it, or it is in use)", "Debloat");
                }
            };
            int code = c.Tool(PsExe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, take, Encoding.UTF8);
            if (code != 0 && ok.Count == 0) c.Warn("PowerShell finished with exit code " + code + ".", "Debloat");
            return ok;
        }
    }
}
