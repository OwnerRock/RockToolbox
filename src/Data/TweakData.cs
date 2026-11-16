using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace RockToolbox.Native
{
    internal sealed class TOp
    {
        public string P, N; public object V, U; public bool Rm, IsString;
        public TOp(string p, string n, object v, object u, bool rm, bool isString) { P = p; N = n; V = v; U = u; Rm = rm; IsString = isString; }
    }

    internal sealed class Tweak
    {
        public string Group, Name, Desc, OS; public bool Rec;
        public TOp[] Ops; public Action<WorkCtx> ApplyFn, UndoFn;
        public bool CanUndo { get { return Ops != null || UndoFn != null; } }

        public void Apply(WorkCtx c)
        {
            if (Ops != null)
            {
                foreach (TOp o in Ops)
                    Reg.Set(o.P, o.N, o.V, o.IsString ? RegistryValueKind.String : RegistryValueKind.DWord);
                return;
            }
            if (ApplyFn == null) throw new InvalidOperationException("this tweak is not available");
            ApplyFn(c);
        }

        public void Undo(WorkCtx c)
        {
            if (Ops != null)
            {
                foreach (TOp o in Ops)
                {
                    if (o.U != null) Reg.Set(o.P, o.N, o.U, o.IsString ? RegistryValueKind.String : RegistryValueKind.DWord);
                    else if (o.Rm) Reg.DeleteValue(o.P, o.N);
                }
                return;
            }
            if (UndoFn != null) UndoFn(c);
        }
    }

    // DATA: tweaks (generated from the original table; script-based ones are in TweakImpl)
    internal static class TweakData
    {
        private static Tweak Ops(string group, string name, bool rec, string desc, TOp[] ops)
        {
            Tweak t = new Tweak(); t.Group = group; t.Name = name; t.Rec = rec; t.Desc = desc; t.OS = ""; t.Ops = ops; return t;
        }

        private static Tweak Scr(string group, string name, bool rec, string desc, string os)
        {
            Tweak t = new Tweak(); t.Group = group; t.Name = name; t.Rec = rec; t.Desc = desc; t.OS = os;
            TweakImpl.Pair p = TweakImpl.Find(name);
            if (p != null) { t.ApplyFn = p.Apply; t.UndoFn = p.Undo; }
            return t;
        }

        public static List<Tweak> Build(bool win11)
        {
            string os = win11 ? "11" : "10";
            List<Tweak> all = new List<Tweak>
            {
            Scr("Advanced", "Windows 10 Extended Support (ESU) - Enable", false, "Windows 10 only. Sets the ConsumerESU flags for your user account (ESUEligibility = 2, ESUEligibilityResult = 1) so Windows treats this PC as eligible for Extended Security Updates. It does not enroll the PC by itself - finish the enrollment from Settings > Windows Update.", "10"),
            Scr("Essential", "Activity History - Disable", true, "Stops Windows from collecting and uploading your activity timeline.", ""),
            Scr("Essential", "Advertising ID - Disable", true, "Stops apps from using your advertising ID for targeted ads.", ""),
            Scr("Essential", "ConsumerFeatures - Disable", true, "Stops Windows from silently installing suggested apps (Candy Crush, etc).", ""),
            Scr("Essential", "Delivery Optimization - Disable", true, "Windows will not upload update files to other PCs on the internet (P2P).", ""),
            Scr("Essential", "End Task With Right Click - Enable", true, "Adds \"End task\" to the taskbar right-click menu (Windows 11).", "11"),
            Scr("Essential", "Hibernation - Disable", false, "Frees disk space equal to hiberfil.sys. Do not use on laptops if you rely on hibernate.", ""),
            Scr("Essential", "Location Tracking - Disable", false, "Denies location access system-wide.", ""),
            Scr("Essential", "Restore Point - Create", false, "Creates a System Restore point named \"Rock Toolbox\" (no undo).", ""),
            Scr("Essential", "Start Menu Ads / Tips / Suggestions - Disable", true, "Turns off suggested content, tips and promoted apps in Start and Settings.", ""),
            Scr("Essential", "Start Menu Bing Search - Disable", true, "Start search only looks at your PC, not the web.", ""),
            Scr("Essential", "Telemetry - Disable", true, "Turns off diagnostic data collection and the DiagTrack service.", ""),
            Scr("Essential", "Temporary Files - Remove", true, "Deletes the user Temp and Windows Temp folders (no undo).", ""),
            Scr("Advanced", "Copilot and Recall (AI) - Disable", false, "Policy-based switch-off for Windows Copilot and AI data analysis.", ""),
            Scr("Advanced", "Fast Startup - Disable", false, "Full shutdown every time (fixes weird driver/uptime issues, slightly slower boot).", ""),
            Scr("Advanced", "Game DVR / Game Bar Capture - Disable", false, "Turns off background game recording (small FPS gain on some PCs).", ""),
            Scr("Advanced", "IPv6 - Disable", false, "Unbinds IPv6 from all network adapters.", ""),
            Scr("Advanced", "Microsoft OneDrive - Remove", false, "Stops OneDrive, runs every OneDrive uninstaller, deletes its program files, caches, shortcuts, scheduled tasks, startup and Explorer sidebar entries, blocks it with a policy and removes the installer so new users do not get it back. Your OneDrive folder is only deleted when it is empty. Undo reinstalls OneDrive with winget.", ""),
            Scr("Advanced", "Microsoft Edge - Remove Completely", false, "Uninstalls Edge with its own uninstaller, then force-deletes every leftover (program files, profile data, services, tasks, registry, shortcuts) and adds policies so Windows Update does not install it again. The WebView2 runtime is kept because many apps need it. Bookmarks and passwords saved only in Edge are lost. Undo reinstalls Edge with winget.", ""),
            Scr("Advanced", "Internet Explorer - Remove Completely", false, "Disables the Internet Explorer Windows feature, removes the Internet Explorer capability, deletes iexplore.exe and its shortcuts. The MSHTML engine is part of Windows and stays (Edge IE-mode and some apps use it). A restart may be needed. Undo turns Internet Explorer back on.", ""),
            Scr("Advanced", "Reserved Storage - Disable", false, "Gives back the ~7 GB Windows reserves for updates.", ""),
            Scr("Advanced", "Startup Delay - Remove", false, "Desktop apps in Startup load immediately instead of after a delay.", ""),
            Scr("Advanced", "Storage Sense - Disable", false, "Stops automatic cleanup of temp files and recycle bin.", ""),
            Scr("Advanced", "Teredo - Disable", false, "Disables the Teredo IPv6 tunneling adapter.", ""),
            Scr("Advanced", "Visual Effects - Set to Best Performance", false, "Switches Windows visual effects to \"Adjust for best performance\". Sign out to apply.", ""),
            Scr("Essential", "Telemetry Scheduled Tasks - Disable", true, "Disables the compatibility appraiser, CEIP and disk-diagnostic data collection tasks.", ""),
            Scr("Essential", "Recycle Bin - Empty", false, "Empties the Recycle Bin on all drives (no undo).", ""),
            Scr("Essential", "Thumbnail Cache - Clear", false, "Deletes Explorer thumbnail caches (no undo, they rebuild automatically).", ""),
            Scr("Essential", "Windows Update Cache - Clean", false, "Clears downloaded update files (SoftwareDistribution\\Download). No undo.", ""),
            Scr("Advanced", "NTFS Last Access Time - Disable", false, "Reduces disk writes (fsutil disablelastaccess).", ""),
            Scr("Advanced", "SysMain (Superfetch) - Disable", false, "Good for SSD systems. Stops and disables the SysMain service.", ""),
            Scr("Advanced", "Windows Search Indexing - Disable", false, "Stops the indexer (start-menu/file search becomes slower).", ""),
            Scr("Advanced", "Print Spooler - Disable", false, "Only if you do not use printers (also closes the PrintNightmare attack surface).", ""),
            Scr("Advanced", "Xbox Services - Disable", false, "Disables Xbox Live auth, save, networking and accessory services.", ""),
            Scr("Advanced", "Unneeded Services - Disable (Fax, Maps, Retail Demo)", false, "Disables Fax, Downloaded Maps Manager and Retail Demo services.", ""),
            Scr("Advanced", "Memory Compression - Disable", false, "Only for PCs with plenty of RAM (16 GB+).", ""),
            Scr("Advanced", "USB Selective Suspend - Disable (plugged in)", false, "Prevents USB devices from sleeping while on AC power.", ""),
            Scr("Advanced", "Never Sleep When Plugged In", false, "Sets \"sleep after\" to Never on AC power.", ""),
            Scr("Advanced", "Component Store (WinSxS) - Cleanup", false, "Runs DISM StartComponentCleanup. Can take several minutes (no undo).", ""),
            Scr("Advanced", "SMBv1 - Disable", true, "Turns off the old, insecure SMB 1.0 protocol.", ""),
            Scr("Advanced", "Defender Cloud Sample Submission - Disable", false, "Windows Defender stops uploading samples (fails if Tamper Protection is on).", ""),
            Ops("Essential", "Cortana - Disable", true, "Turns Cortana off by policy.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "AllowCortana", 0, null, true, false) }),
            Ops("Essential", "Web Search in Windows Search - Disable", true, "Windows Search stops sending queries to Bing.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "DisableWebSearch", 1, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "ConnectedSearchUseWeb", 0, null, true, false) }),
            Ops("Essential", "Feedback Notifications - Disable", true, "No more \"How likely are you to recommend Windows\" popups.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "DoNotShowFeedbackNotifications", 1, null, true, false), new TOp("HKCU:\\Software\\Microsoft\\Siuf\\Rules", "NumberOfSIUFInPeriod", 0, null, true, false) }),
            Ops("Essential", "Tailored Experiences - Disable", true, "Stops Microsoft from using diagnostic data to personalize tips and ads.", new TOp[] { new TOp("HKCU:\\Software\\Policies\\Microsoft\\Windows\\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1, null, true, false) }),
            Ops("Essential", "Windows Spotlight - Disable", true, "Disables Spotlight suggestions on lock screen, Settings and Action Center.", new TOp[] { new TOp("HKCU:\\Software\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsSpotlightFeatures", 1, null, true, false), new TOp("HKCU:\\Software\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsSpotlightOnActionCenter", 1, null, true, false), new TOp("HKCU:\\Software\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsSpotlightOnSettings", 1, null, true, false), new TOp("HKCU:\\Software\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsSpotlightWindowsWelcomeExperience", 1, null, true, false) }),
            Ops("Essential", "Customer Experience Program (CEIP) - Disable", true, "Turns off the Customer Experience Improvement Program.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows", "CEIPEnable", 0, null, true, false) }),
            Ops("Essential", "Application Telemetry / Inventory - Disable", true, "Disables the inventory collector, Application Impact Telemetry and Steps Recorder.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat", "DisableInventory", 1, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat", "AITEnable", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat", "DisableUAR", 1, null, true, false) }),
            Ops("Essential", "Clipboard History and Cloud Sync - Disable", false, "Disables Win+V history and cross-device clipboard sync by policy.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "AllowClipboardHistory", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System", "AllowCrossDeviceClipboard", 0, null, true, false) }),
            Ops("Essential", "Typing and Inking Personalization - Disable", true, "Stops Windows from collecting what you type and ink to personalize suggestions.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\InputPersonalization", "RestrictImplicitInkCollection", 1, 0, false, false), new TOp("HKCU:\\Software\\Microsoft\\InputPersonalization", "RestrictImplicitTextCollection", 1, 0, false, false), new TOp("HKCU:\\Software\\Microsoft\\InputPersonalization\\TrainedDataStore", "HarvestContacts", 0, 1, false, false), new TOp("HKCU:\\Software\\Microsoft\\Personalization\\Settings", "AcceptedPrivacyPolicy", 0, 1, false, false) }),
            Ops("Essential", "Handwriting Data Sharing - Disable", false, "Blocks handwriting data and error report sharing.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC", "PreventHandwritingDataSharing", 1, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\HandwritingErrorReports", "PreventHandwritingErrorReports", 1, null, true, false) }),
            Ops("Essential", "Online Speech Recognition - Disable", false, "Declines the online speech recognition privacy policy.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy", "HasAccepted", 0, 1, false, false) }),
            Ops("Essential", "Remote Assistance - Disable", false, "Blocks incoming Remote Assistance requests.", new TOp[] { new TOp("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance", "fAllowToGetHelp", 0, 1, false, false) }),
            Ops("Essential", "Notification Toasts - Disable", false, "Turns off all toast notifications.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PushNotifications", "ToastEnabled", 0, 1, false, false) }),
            Ops("Essential", "Start Menu Recommended / Recent Items - Disable", true, "Hides recent files and the \"Recommended\" section in Start.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_TrackDocs", 0, 1, false, false), new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_TrackProgs", 0, 1, false, false), new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Start_IrisRecommendations", 0, 1, false, false) }),
            Ops("Essential", "Quick Access Recent / Frequent - Disable", false, "File Explorer stops listing recent and frequent files/folders.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer", "ShowRecent", 0, 1, false, false), new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer", "ShowFrequent", 0, 1, false, false), new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer", "ShowCloudFilesInQuickAccess", 0, 1, false, false) }),
            Ops("Essential", "Explorer Sync Provider Ads - Disable", true, "Removes OneDrive / Microsoft 365 promos from File Explorer.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowSyncProviderNotifications", 0, 1, false, false) }),
            Ops("Essential", "Search Highlights - Disable", true, "Removes the daily doodles / highlights from the search box.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings", "IsDynamicSearchBoxEnabled", 0, 1, false, false) }),
            Ops("Essential", "\"Finish Setting Up Your Device\" Nags - Disable", true, "Stops the \"Get even more out of Windows\" full-screen prompts.", new TOp[] { new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1, false, false) }),
            Ops("Essential", "Menu Show Delay - Remove", true, "Menus open instantly.", new TOp[] { new TOp("HKCU:\\Control Panel\\Desktop", "MenuShowDelay", "0", "400", false, true) }),
            Ops("Advanced", "Network Throttling and System Responsiveness - Tweak", false, "Disables multimedia network throttling and reserves less CPU for background tasks.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile", "NetworkThrottlingIndex", -1, 10, false, false), new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile", "SystemResponsiveness", 0, 20, false, false) }),
            Ops("Advanced", "Games Scheduling Priority - Boost", false, "Raises CPU/GPU scheduling priority for games.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "GPU Priority", 8, 8, false, false), new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "Priority", 6, 2, false, false), new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "Scheduling Category", "High", "Medium", false, true), new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "SFIO Priority", "High", "Normal", false, true) }),
            Ops("Advanced", "Foreground Apps Priority - Boost", false, "Gives the app you are using more CPU time slices (Win32PrioritySeparation = 38).", new TOp[] { new TOp("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl", "Win32PrioritySeparation", 38, 2, false, false) }),
            Ops("Advanced", "Hardware-Accelerated GPU Scheduling - Enable", false, "Needs a supported GPU and recent drivers. Restart required.", new TOp[] { new TOp("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", "HwSchMode", 2, 1, false, false) }),
            Ops("Advanced", "Faster Shutdown / Kill Hung Apps", false, "Shorter timeouts for hung apps and services at shutdown.", new TOp[] { new TOp("HKCU:\\Control Panel\\Desktop", "AutoEndTasks", "1", "0", false, true), new TOp("HKCU:\\Control Panel\\Desktop", "HungAppTimeout", "1000", "5000", false, true), new TOp("HKCU:\\Control Panel\\Desktop", "WaitToKillAppTimeout", "2000", "20000", false, true), new TOp("HKLM:\\SYSTEM\\CurrentControlSet\\Control", "WaitToKillServiceTimeout", "2000", "5000", false, true) }),
            Ops("Advanced", "Power Throttling - Disable", false, "Stops Windows from throttling background apps (desktop PCs).", new TOp[] { new TOp("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling", "PowerThrottlingOff", 1, null, true, false) }),
            Ops("Advanced", "Aero Shake - Disable", false, "Shaking a window no longer minimizes the others.", new TOp[] { new TOp("HKCU:\\Software\\Policies\\Microsoft\\Windows\\Explorer", "NoWindowMinimizingShortcuts", 1, null, true, false) }),
            Ops("Advanced", "Windows Insider / Preview Builds - Disable", false, "Blocks Insider preview builds by policy.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\PreviewBuilds", "AllowBuildPreview", 0, null, true, false) }),
            Ops("Advanced", "Date and Time - Set Time to UTC (dual boot)", false, "Makes Windows treat the hardware clock as UTC (fixes clock drift with Linux).", new TOp[] { new TOp("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\TimeZoneInformation", "RealTimeIsUniversal", 1, null, true, false) }),
            Ops("Advanced", "Microsoft Edge - Debloat (policies)", false, "Turns off Edge first-run, startup boost, background mode, shopping assistant, sidebar, rewards and follow.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "HideFirstRunExperience", 1, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "StartupBoostEnabled", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "BackgroundModeEnabled", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "EdgeShoppingAssistantEnabled", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "PersonalizationReportingEnabled", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "ShowMicrosoftRewards", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "EdgeFollowEnabled", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "HubsSidebarEnabled", 0, null, true, false), new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge", "DiscoverPageContextEnabled", 0, null, true, false) }),
            Ops("Advanced", "LLMNR - Disable", false, "Disables Link-Local Multicast Name Resolution (spoofing risk on public networks).", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient", "EnableMulticast", 0, null, true, false) }),
            Ops("Advanced", "AutoRun / AutoPlay - Disable", true, "Stops drives and USB sticks from auto-running or auto-playing.", new TOp[] { new TOp("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", "NoDriveTypeAutoRun", 255, null, true, false), new TOp("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\AutoplayHandlers", "DisableAutoplay", 1, 0, false, false) }),
            };
            return all.FindAll(delegate(Tweak t) { return t.OS.Length == 0 || t.OS == os; });
        }
    }
}
