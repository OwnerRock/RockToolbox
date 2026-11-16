<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Management;

namespace RockToolbox.Native
{
    // Hand-written actions of the tweaks that are not simple registry lists. Key = start of the tweak name.
    internal static class TweakImpl
    {
        public sealed class Pair { public Action<WorkCtx> Apply, Undo; }

        private static readonly Dictionary<string, Pair> Map = new Dictionary<string, Pair>();

        private static void Add(string key, Action<WorkCtx> apply, Action<WorkCtx> undo)
        {
            Pair p = new Pair(); p.Apply = apply; p.Undo = undo; Map[key] = p;
        }

        public static Pair Find(string tweakName)
        {
            foreach (KeyValuePair<string, Pair> kv in Map)
                if (tweakName.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        private static void D(string p, string n, int v) { Reg.SetDword(p, n, v); }
        private static void Rm(string p, string n) { Reg.DeleteValue(p, n); }
        private static void Exe(WorkCtx c, string exe, string args) { c.Tool(Tool.System32(exe), args); }

        private static readonly string[] SvcXbox = { "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc" };
        private static readonly string[] AdsIds = {
            "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled",
            "SubscribedContent-353696Enabled", "SubscribedContent-310093Enabled", "SystemPaneSuggestionsEnabled", "SilentInstalledAppsEnabled",
            "SoftLandingEnabled", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled", "ContentDeliveryAllowed" };
        private static readonly string[] TelemetryTasks = {
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser", @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator", @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector" };

        static TweakImpl()
        {
            string esu = @"HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows\ConsumerESU";
            Add("Windows 10 Extended Support", delegate(WorkCtx c)
            {
                if (Environment.OSVersion.Version.Build >= 22000) { c.Warn("This tweak is for Windows 10 only - skipped on Windows 11.", "Tweaks"); return; }
                D(esu, "ESUEligibility", 2); D(esu, "ESUEligibilityResult", 1);
            }, delegate { Rm(esu, "ESUEligibility"); Rm(esu, "ESUEligibilityResult"); });

            string sys = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\System";
            Add("Activity History", delegate { D(sys, "EnableActivityFeed", 0); D(sys, "PublishUserActivities", 0); D(sys, "UploadUserActivities", 0); },
                delegate { Rm(sys, "EnableActivityFeed"); Rm(sys, "PublishUserActivities"); Rm(sys, "UploadUserActivities"); });
            string adv = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo";
            Add("Advertising ID", delegate { D(adv, "Enabled", 0); }, delegate { D(adv, "Enabled", 1); });
            string cc = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
            Add("ConsumerFeatures", delegate { D(cc, "DisableWindowsConsumerFeatures", 1); }, delegate { Rm(cc, "DisableWindowsConsumerFeatures"); });
            string dop = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            Add("Delivery Optimization", delegate { D(dop, "DODownloadMode", 0); }, delegate { Rm(dop, "DODownloadMode"); });
            string tdev = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings";
            Add("End Task With Right Click", delegate { D(tdev, "TaskbarEndTask", 1); }, delegate { D(tdev, "TaskbarEndTask", 0); });
            Add("Hibernation", delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/h off"); }, delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/h on"); });
            string loc = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location";
            Add("Location Tracking", delegate { Reg.SetString(loc, "Value", "Deny"); }, delegate { Reg.SetString(loc, "Value", "Allow"); });
            Add("Restore Point", delegate(WorkCtx c)
            {
                string err = Safety.CreateNow("Rock Toolbox");
                if (err.Length > 0) throw new InvalidOperationException(err);
            }, null);
            string cdm = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            Add("Start Menu Ads", delegate { foreach (string i in AdsIds) D(cdm, i, 0); }, delegate { foreach (string i in AdsIds) D(cdm, i, 1); });
            string pe = @"HKCU:\Software\Policies\Microsoft\Windows\Explorer";
            Add("Start Menu Bing Search", delegate { D(pe, "DisableSearchBoxSuggestions", 1); }, delegate { Rm(pe, "DisableSearchBoxSuggestions"); });
            string dc1 = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
            string dc2 = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
            Add("Telemetry - Disable", delegate
            {
                D(dc1, "AllowTelemetry", 0); D(dc2, "AllowTelemetry", 0);
                SysUtil.DisableService("DiagTrack"); SysUtil.SetStartMode("dmwappushservice", "Disabled");
            }, delegate
            {
                Rm(dc1, "AllowTelemetry"); Rm(dc2, "AllowTelemetry");
                SysUtil.SetStartMode("DiagTrack", "Automatic"); SysUtil.StartService("DiagTrack"); SysUtil.SetStartMode("dmwappushservice", "Manual");
            });
            Add("Temporary Files", delegate
            {
                FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%TEMP%\*"));
                FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%windir%\Temp\*"));
            }, null);
            string cop = @"HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot";
            string ai = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
            Add("Copilot and Recall", delegate { D(cop, "TurnOffWindowsCopilot", 1); D(ai, "DisableAIDataAnalysis", 1); },
                delegate { Rm(cop, "TurnOffWindowsCopilot"); Rm(ai, "DisableAIDataAnalysis"); });
            string pw = @"HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power";
            Add("Fast Startup", delegate { D(pw, "HiberbootEnabled", 0); }, delegate { D(pw, "HiberbootEnabled", 1); });
            Add("Game DVR", delegate { D(@"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 0); D(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0); },
                delegate { D(@"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 1); D(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1); });
            string v6 = @"HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters";
            Add("IPv6", delegate(WorkCtx c) { D(v6, "DisabledComponents", 255); c.Info("IPv6 is disabled - restart Windows to apply.", "Tweaks"); },
                delegate(WorkCtx c) { Rm(v6, "DisabledComponents"); c.Info("IPv6 is enabled again - restart Windows to apply.", "Tweaks"); });
            Add("Microsoft OneDrive", delegate(WorkCtx c) { Removers.RemoveOneDrive(c); }, delegate(WorkCtx c) { Removers.RestoreOneDrive(c); });
            Add("Microsoft Edge - Remove", delegate(WorkCtx c) { Removers.RemoveEdge(c); }, delegate(WorkCtx c) { Removers.RestoreEdge(c); });
            Add("Internet Explorer", delegate(WorkCtx c) { Removers.RemoveIE(c); }, delegate(WorkCtx c) { Removers.RestoreIE(c); });
            Add("Reserved Storage", delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Set-ReservedStorageState /State:Disabled"); },
                delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Set-ReservedStorageState /State:Enabled"); });
            string ser = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
            Add("Startup Delay", delegate { D(ser, "StartupDelayInMSec", 0); }, delegate { Rm(ser, "StartupDelayInMSec"); });
            string ss = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy";
            Add("Storage Sense", delegate { D(ss, "01", 0); }, delegate { D(ss, "01", 1); });
            Add("Teredo", delegate(WorkCtx c) { Exe(c, "netsh.exe", "interface teredo set state disabled"); },
                delegate(WorkCtx c) { Exe(c, "netsh.exe", "interface teredo set state default"); });
            string vfx = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
            Add("Visual Effects", delegate { D(vfx, "VisualFXSetting", 2); }, delegate { D(vfx, "VisualFXSetting", 0); });

            Add("Telemetry Scheduled Tasks", delegate { foreach (string t in TelemetryTasks) TaskSched.SetTaskEnabled(t, false); },
                delegate { foreach (string t in TelemetryTasks) TaskSched.SetTaskEnabled(t, true); });
            Add("Recycle Bin", delegate { SysUtil.EmptyRecycleBin(); }, null);
            Add("Thumbnail Cache", delegate { FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db")); }, null);
            Add("Windows Update Cache", delegate
            {
                SysUtil.StopService("wuauserv");
                FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%windir%\SoftwareDistribution\Download\*"));
                SysUtil.StartService("wuauserv");
            }, null);
            Add("NTFS Last Access", delegate(WorkCtx c) { Exe(c, "fsutil.exe", "behavior set disablelastaccess 1"); },
                delegate(WorkCtx c) { Exe(c, "fsutil.exe", "behavior set disablelastaccess 2"); });
            Add("SysMain", delegate { SysUtil.DisableService("SysMain"); }, delegate { SysUtil.SetStartMode("SysMain", "Automatic"); SysUtil.StartService("SysMain"); });
            Add("Windows Search Indexing", delegate { SysUtil.DisableService("WSearch"); }, delegate { SysUtil.SetStartMode("WSearch", "Automatic"); SysUtil.StartService("WSearch"); });
            Add("Print Spooler", delegate { SysUtil.DisableService("Spooler"); }, delegate { SysUtil.SetStartMode("Spooler", "Automatic"); SysUtil.StartService("Spooler"); });
            Add("Xbox Services", delegate { foreach (string s in SvcXbox) SysUtil.DisableService(s); },
                delegate { foreach (string s in SvcXbox) SysUtil.SetStartMode(s, "Manual"); });
            Add("Unneeded Services", delegate { foreach (string s in new string[] { "Fax", "MapsBroker", "RetailDemo" }) SysUtil.DisableService(s); },
                delegate { SysUtil.SetStartMode("Fax", "Manual"); SysUtil.SetStartMode("MapsBroker", "Automatic"); SysUtil.SetStartMode("RetailDemo", "Manual"); });
            Add("USB Selective Suspend", delegate(WorkCtx c)
            {
                Exe(c, "powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                Exe(c, "powercfg.exe", "/setactive SCHEME_CURRENT");
            }, delegate(WorkCtx c)
            {
                Exe(c, "powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1");
                Exe(c, "powercfg.exe", "/setactive SCHEME_CURRENT");
            });
            Add("Never Sleep When Plugged In", delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/change standby-timeout-ac 0"); },
                delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/change standby-timeout-ac 30"); });
            Add("Component Store", delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Cleanup-Image /StartComponentCleanup"); }, null);
            Add("SMBv1", delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Disable-Feature /FeatureName:SMB1Protocol /NoRestart /Quiet"); },
                delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Enable-Feature /FeatureName:SMB1Protocol /NoRestart /Quiet"); });
            Add("Defender Cloud Sample", delegate { SetDefenderSamples(2); }, delegate { SetDefenderSamples(1); });
            // Memory compression has no registry / WMI switch: it is only exposed through the MMAgent PowerShell cmdlets.
            Add("Memory Compression", delegate(WorkCtx c) { c.Warn("Memory compression can only be changed with PowerShell cmdlets - skipped in the native version.", "Tweaks"); },
                delegate(WorkCtx c) { c.Warn("Memory compression can only be changed with PowerShell cmdlets - skipped in the native version.", "Tweaks"); });
        }

        private static void SetDefenderSamples(int v)
        {
            ManagementClass mc = new ManagementClass(@"root\Microsoft\Windows\Defender", "MSFT_MpPreference", null);
            ManagementBaseObject inp = mc.GetMethodParameters("Set");
            inp["SubmitSamplesConsent"] = (byte)v;
            mc.InvokeMethod("Set", inp, null);
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Management;

namespace RockToolbox.Native
{
    // Hand-written actions of the tweaks that are not simple registry lists. Key = start of the tweak name.
    internal static class TweakImpl
    {
        public sealed class Pair { public Action<WorkCtx> Apply, Undo; }

        private static readonly Dictionary<string, Pair> Map = new Dictionary<string, Pair>();

        private static void Add(string key, Action<WorkCtx> apply, Action<WorkCtx> undo)
        {
            Pair p = new Pair(); p.Apply = apply; p.Undo = undo; Map[key] = p;
        }

        public static Pair Find(string tweakName)
        {
            foreach (KeyValuePair<string, Pair> kv in Map)
                if (tweakName.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        private static void D(string p, string n, int v) { Reg.SetDword(p, n, v); }
        private static void Rm(string p, string n) { Reg.DeleteValue(p, n); }
        private static void Exe(WorkCtx c, string exe, string args) { c.Tool(Tool.System32(exe), args); }

        private static readonly string[] SvcXbox = { "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc" };
        private static readonly string[] AdsIds = {
            "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled",
            "SubscribedContent-353696Enabled", "SubscribedContent-310093Enabled", "SystemPaneSuggestionsEnabled", "SilentInstalledAppsEnabled",
            "SoftLandingEnabled", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled", "ContentDeliveryAllowed" };
        private static readonly string[] TelemetryTasks = {
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser", @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator", @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector" };

        static TweakImpl()
        {
            string esu = @"HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows\ConsumerESU";
            Add("Windows 10 Extended Support", delegate(WorkCtx c)
            {
                if (Environment.OSVersion.Version.Build >= 22000) { c.Warn("This tweak is for Windows 10 only - skipped on Windows 11.", "Tweaks"); return; }
                D(esu, "ESUEligibility", 2); D(esu, "ESUEligibilityResult", 1);
            }, delegate { Rm(esu, "ESUEligibility"); Rm(esu, "ESUEligibilityResult"); });

            string sys = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\System";
            Add("Activity History", delegate { D(sys, "EnableActivityFeed", 0); D(sys, "PublishUserActivities", 0); D(sys, "UploadUserActivities", 0); },
                delegate { Rm(sys, "EnableActivityFeed"); Rm(sys, "PublishUserActivities"); Rm(sys, "UploadUserActivities"); });
            string adv = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo";
            Add("Advertising ID", delegate { D(adv, "Enabled", 0); }, delegate { D(adv, "Enabled", 1); });
            string cc = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
            Add("ConsumerFeatures", delegate { D(cc, "DisableWindowsConsumerFeatures", 1); }, delegate { Rm(cc, "DisableWindowsConsumerFeatures"); });
            string dop = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            Add("Delivery Optimization", delegate { D(dop, "DODownloadMode", 0); }, delegate { Rm(dop, "DODownloadMode"); });
            string tdev = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings";
            Add("End Task With Right Click", delegate { D(tdev, "TaskbarEndTask", 1); }, delegate { D(tdev, "TaskbarEndTask", 0); });
            Add("Hibernation", delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/h off"); }, delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/h on"); });
            string loc = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location";
            Add("Location Tracking", delegate { Reg.SetString(loc, "Value", "Deny"); }, delegate { Reg.SetString(loc, "Value", "Allow"); });
            Add("Restore Point", delegate(WorkCtx c)
            {
                string err = Safety.CreateNow("Rock Toolbox");
                if (err.Length > 0) throw new InvalidOperationException(err);
            }, null);
            string cdm = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            Add("Start Menu Ads", delegate { foreach (string i in AdsIds) D(cdm, i, 0); }, delegate { foreach (string i in AdsIds) D(cdm, i, 1); });
            string pe = @"HKCU:\Software\Policies\Microsoft\Windows\Explorer";
            Add("Start Menu Bing Search", delegate { D(pe, "DisableSearchBoxSuggestions", 1); }, delegate { Rm(pe, "DisableSearchBoxSuggestions"); });
            string dc1 = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
            string dc2 = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
            Add("Telemetry - Disable", delegate
            {
                D(dc1, "AllowTelemetry", 0); D(dc2, "AllowTelemetry", 0);
                SysUtil.DisableService("DiagTrack"); SysUtil.SetStartMode("dmwappushservice", "Disabled");
            }, delegate
            {
                Rm(dc1, "AllowTelemetry"); Rm(dc2, "AllowTelemetry");
                SysUtil.SetStartMode("DiagTrack", "Automatic"); SysUtil.StartService("DiagTrack"); SysUtil.SetStartMode("dmwappushservice", "Manual");
            });
            Add("Temporary Files", delegate
            {
                FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%TEMP%\*"));
                FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%windir%\Temp\*"));
            }, null);
            string cop = @"HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot";
            string ai = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
            Add("Copilot and Recall", delegate { D(cop, "TurnOffWindowsCopilot", 1); D(ai, "DisableAIDataAnalysis", 1); },
                delegate { Rm(cop, "TurnOffWindowsCopilot"); Rm(ai, "DisableAIDataAnalysis"); });
            string pw = @"HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power";
            Add("Fast Startup", delegate { D(pw, "HiberbootEnabled", 0); }, delegate { D(pw, "HiberbootEnabled", 1); });
            Add("Game DVR", delegate { D(@"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 0); D(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0); },
                delegate { D(@"HKCU:\System\GameConfigStore", "GameDVR_Enabled", 1); D(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1); });
            string v6 = @"HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters";
            Add("IPv6", delegate(WorkCtx c) { D(v6, "DisabledComponents", 255); c.Info("IPv6 is disabled - restart Windows to apply.", "Tweaks"); },
                delegate(WorkCtx c) { Rm(v6, "DisabledComponents"); c.Info("IPv6 is enabled again - restart Windows to apply.", "Tweaks"); });
            Add("Microsoft OneDrive", delegate(WorkCtx c) { Removers.RemoveOneDrive(c); }, delegate(WorkCtx c) { Removers.RestoreOneDrive(c); });
            Add("Microsoft Edge - Remove", delegate(WorkCtx c) { Removers.RemoveEdge(c); }, delegate(WorkCtx c) { Removers.RestoreEdge(c); });
            Add("Internet Explorer", delegate(WorkCtx c) { Removers.RemoveIE(c); }, delegate(WorkCtx c) { Removers.RestoreIE(c); });
            Add("Reserved Storage", delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Set-ReservedStorageState /State:Disabled"); },
                delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Set-ReservedStorageState /State:Enabled"); });
            string ser = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
            Add("Startup Delay", delegate { D(ser, "StartupDelayInMSec", 0); }, delegate { Rm(ser, "StartupDelayInMSec"); });
            string ss = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy";
            Add("Storage Sense", delegate { D(ss, "01", 0); }, delegate { D(ss, "01", 1); });
            Add("Teredo", delegate(WorkCtx c) { Exe(c, "netsh.exe", "interface teredo set state disabled"); },
                delegate(WorkCtx c) { Exe(c, "netsh.exe", "interface teredo set state default"); });
            string vfx = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
            Add("Visual Effects", delegate { D(vfx, "VisualFXSetting", 2); }, delegate { D(vfx, "VisualFXSetting", 0); });

            Add("Telemetry Scheduled Tasks", delegate { foreach (string t in TelemetryTasks) TaskSched.SetTaskEnabled(t, false); },
                delegate { foreach (string t in TelemetryTasks) TaskSched.SetTaskEnabled(t, true); });
            Add("Recycle Bin", delegate { SysUtil.EmptyRecycleBin(); }, null);
            Add("Thumbnail Cache", delegate { FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db")); }, null);
            Add("Windows Update Cache", delegate
            {
                SysUtil.StopService("wuauserv");
                FastFs.DeletePattern(Environment.ExpandEnvironmentVariables(@"%windir%\SoftwareDistribution\Download\*"));
                SysUtil.StartService("wuauserv");
            }, null);
            Add("NTFS Last Access", delegate(WorkCtx c) { Exe(c, "fsutil.exe", "behavior set disablelastaccess 1"); },
                delegate(WorkCtx c) { Exe(c, "fsutil.exe", "behavior set disablelastaccess 2"); });
            Add("SysMain", delegate { SysUtil.DisableService("SysMain"); }, delegate { SysUtil.SetStartMode("SysMain", "Automatic"); SysUtil.StartService("SysMain"); });
            Add("Windows Search Indexing", delegate { SysUtil.DisableService("WSearch"); }, delegate { SysUtil.SetStartMode("WSearch", "Automatic"); SysUtil.StartService("WSearch"); });
            Add("Print Spooler", delegate { SysUtil.DisableService("Spooler"); }, delegate { SysUtil.SetStartMode("Spooler", "Automatic"); SysUtil.StartService("Spooler"); });
            Add("Xbox Services", delegate { foreach (string s in SvcXbox) SysUtil.DisableService(s); },
                delegate { foreach (string s in SvcXbox) SysUtil.SetStartMode(s, "Manual"); });
            Add("Unneeded Services", delegate { foreach (string s in new string[] { "Fax", "MapsBroker", "RetailDemo" }) SysUtil.DisableService(s); },
                delegate { SysUtil.SetStartMode("Fax", "Manual"); SysUtil.SetStartMode("MapsBroker", "Automatic"); SysUtil.SetStartMode("RetailDemo", "Manual"); });
            Add("USB Selective Suspend", delegate(WorkCtx c)
            {
                Exe(c, "powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                Exe(c, "powercfg.exe", "/setactive SCHEME_CURRENT");
            }, delegate(WorkCtx c)
            {
                Exe(c, "powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1");
                Exe(c, "powercfg.exe", "/setactive SCHEME_CURRENT");
            });
            Add("Never Sleep When Plugged In", delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/change standby-timeout-ac 0"); },
                delegate(WorkCtx c) { Exe(c, "powercfg.exe", "/change standby-timeout-ac 30"); });
            Add("Component Store", delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Cleanup-Image /StartComponentCleanup"); }, null);
            Add("SMBv1", delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Disable-Feature /FeatureName:SMB1Protocol /NoRestart /Quiet"); },
                delegate(WorkCtx c) { Exe(c, "dism.exe", "/Online /Enable-Feature /FeatureName:SMB1Protocol /NoRestart /Quiet"); });
            Add("Defender Cloud Sample", delegate { SetDefenderSamples(2); }, delegate { SetDefenderSamples(1); });
            // Memory compression has no registry / WMI switch: it is only exposed through the MMAgent PowerShell cmdlets.
            Add("Memory Compression", delegate(WorkCtx c) { c.Warn("Memory compression can only be changed with PowerShell cmdlets - skipped in the native version.", "Tweaks"); },
                delegate(WorkCtx c) { c.Warn("Memory compression can only be changed with PowerShell cmdlets - skipped in the native version.", "Tweaks"); });
        }

        private static void SetDefenderSamples(int v)
        {
            ManagementClass mc = new ManagementClass(@"root\Microsoft\Windows\Defender", "MSFT_MpPreference", null);
            ManagementBaseObject inp = mc.GetMethodParameters("Set");
            inp["SubmitSamplesConsent"] = (byte)v;
            mc.InvokeMethod("Set", inp, null);
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
