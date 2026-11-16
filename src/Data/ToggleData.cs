using System;
using System.Collections.Generic;

namespace RockToolbox.Native
{
    // one registry value of a switch: On/Off values (string or int), optional default when the value is missing
    internal sealed class TV
    {
        public string P, N; public object On, Off, Def; public bool IsString, NoCheck;
        public TV(string p, string n, object on, object off, bool isString, object def, bool noCheck)
        { P = p; N = n; On = on; Off = off; IsString = isString; Def = def; NoCheck = noCheck; }
    }

    internal sealed class Toggle
    {
        public string Name, Desc, OS, Group; public bool Restart; public TV[] Values;
        public Func<bool> CustomGet; public Action CustomOn, CustomOff;
        public bool IsDate;                                   // short-date format box
        public string ChoiceP, ChoiceN; public int ChoiceDef;  // drop-down stored in a registry value
        public string[] ChoiceNames; public int[] ChoiceValues;
        public bool IsChoice { get { return ChoiceNames != null; } }
        public Toggle(string name, string desc, string os, string group, bool restart, TV[] values)
        { Name = name; Desc = desc; OS = os; Group = group; Restart = restart; Values = values; }
    }

    // DATA: customize switches (generated from the original table). ON = feature enabled, applied immediately.
    internal static class ToggleData
    {
        public static List<Toggle> Build(bool win11)
        {
            string os = win11 ? "11" : "10";
            List<Toggle> all = new List<Toggle>
            {
            new Toggle("BSoD Verbose Mode", "Show technical details on blue screens.", "", "", false, new TV[] { new TV("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\CrashControl", "DisplayParameters", 1, 0, false, 0, false) }),
            new Toggle("Dark Theme for Windows", "Dark theme for Windows and apps.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", 0, 1, false, 1, false), new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "SystemUsesLightTheme", 0, 1, false, 1, false) }),
            new Toggle("Transparency Effects", "Acrylic / Mica transparency on Start, taskbar and windows.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "EnableTransparency", 1, 0, false, 1, false) }),
            new Toggle("Enable Long Paths", "Allow file paths longer than 260 characters.", "", "", false, new TV[] { new TV("HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem", "LongPathsEnabled", 1, 0, false, 0, false) }),
            new Toggle("File Explorer File Extensions", "Show .txt, .exe, .jpg ... in File Explorer.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "HideFileExt", 0, 1, false, 1, false) }),
            new Toggle("File Explorer Hidden Files", "Display hidden files and folders.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "Hidden", 1, 2, false, 2, false) }),
            new Toggle("File Explorer Opens to This PC", "Instead of Home / Quick access.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "LaunchTo", 1, 2, false, 3, false) }),
            new Toggle("Compact View in File Explorer", "Less spacing between items.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "UseCompactMode", 1, 0, false, 0, false) }),
            new Toggle("Game Mode", "Windows Game Mode.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\GameBar", "AutoGameModeEnabled", 1, 0, false, 1, false) }),
            new Toggle("Logon Verbose Mode", "Show detailed status messages during sign-in/out.", "", "", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "VerboseStatus", 1, 0, false, 0, false) }),
            new Toggle("Mouse Acceleration", "Enhance pointer precision (turn OFF for 1:1 mouse movement).", "", "", false, new TV[] { new TV("HKCU:\\Control Panel\\Mouse", "MouseSpeed", "1", "0", true, "1", false), new TV("HKCU:\\Control Panel\\Mouse", "MouseThreshold1", "6", "0", true, "6", false), new TV("HKCU:\\Control Panel\\Mouse", "MouseThreshold2", "10", "0", true, "10", false) }),
            new Toggle("Num Lock on Startup", "Turn Num Lock on at sign-in.", "", "", false, new TV[] { new TV("HKCU:\\Control Panel\\Keyboard", "InitialKeyboardIndicators", "2", "0", true, "0", false) }),
            new Toggle("Scrollbars Always Visible", "Do not auto-hide scrollbars.", "", "", false, new TV[] { new TV("HKCU:\\Control Panel\\Accessibility", "DynamicScrollbars", 0, 1, false, 1, false) }),
            new Toggle("Sticky Keys", "The Shift x5 popup (OFF = disabled).", "", "", false, new TV[] { new TV("HKCU:\\Control Panel\\Accessibility\\StickyKeys", "Flags", "510", "58", true, "510", false) }),
            new Toggle("Window Snapping", "Snap windows to screen edges.", "", "", false, new TV[] { new TV("HKCU:\\Control Panel\\Desktop", "WindowArrangementActive", "1", "0", true, "1", false) }),
            new Toggle("Show \"This PC\" Icon on Desktop", "Adds the This PC icon to the desktop.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\HideDesktopIcons\\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0, 1, false, 1, false) }),
            new Toggle("Taskbar Centered Icons (Windows 11)", "OFF = icons aligned to the left.", "11", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarAl", 1, 0, false, 1, false) }),
            new Toggle("Taskbar Task View Icon", "Task View button.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowTaskViewButton", 1, 0, false, 1, false) }),
            new Toggle("Taskbar Chat / Teams Icon", "Chat button.", "11", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarMn", 1, 0, false, 1, false) }),
            new Toggle("Taskbar Meet Now Icon", "Meet Now button in the taskbar notification area (Windows 10). Turn it off to hide it.", "10", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", "HideSCAMeetNow", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", "HideSCAMeetNow", 0, 1, false, 0, false) }),
            new Toggle("Taskbar Clock Seconds", "Clock shows HH:MM:SS.", "", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowSecondsInSystemClock", 1, 0, false, 0, false) }),
            new Toggle("Taskbar Battery Percentage", "Display battery percentage on taskbar (needs a recent Windows 11 build: 24H2 / 25H2 with the latest updates).", "11", "", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "IsBatteryPercentageEnabled", 1, 0, false, 0, false) }),
            new Toggle("Hide Taskbar Clock", "ON = hide the clock and date from the taskbar. Restart Explorer to apply.", "", "", true, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", "HideClock", 1, 0, false, 0, false) }),
            new Toggle("Ambient Lighting", "Dynamic lighting for RGB devices.", "11", "Windows Features", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Lighting", "AmbientLightingEnabled", 1, 0, false, 1, false) }),
            new Toggle("Automatic Driver Installation (Windows Update)", "OFF = exclude drivers from Windows Update and stop searching Windows Update for drivers.", "", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Update", "ExcludeWUDriversInQualityUpdate", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Microsoft\\PolicyManager\\default\\Update", "ExcludeWUDriversInQualityUpdate", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings", "ExcludeWUDriversInQualityUpdate", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Microsoft\\PolicyManager\\default\\Update\\ExcludeWUDriversInQualityUpdate", "value", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Device Metadata", "PreventDeviceMetadataFromNetwork", 0, 1, false, 0, false), new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching", "SearchOrderConfig", 0, 0, false, null, true), new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching", "DontSearchWindowsUpdate", 0, 1, false, 0, false) }),
            new Toggle("Background Apps", "Let Store apps run in the background.", "", "Windows Features", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications", "GlobalUserDisabled", 0, 1, false, 0, false) }),
            new Toggle("Error Reporting", "Windows Error Reporting.", "", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting", "Disabled", 0, 1, false, 0, false) }),
            new Toggle("Lock Screen", "Show the lock screen before sign-in (OFF = skip it).", "", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization", "NoLockScreen", 0, 1, false, 0, false) }),
            new Toggle("Snap Assist Flyout", "Layout flyout when hovering the maximize button (Windows 11).", "11", "Windows Features", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "EnableSnapAssistFlyout", 1, 0, false, 1, false) }),
            new Toggle("Snap Bar", "Snap layouts bar when dragging a window to the top.", "11", "Windows Features", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "EnableSnapBar", 1, 0, false, 1, false) }),
            new Toggle("Startup Sound", "The Windows startup chime.", "", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "DisableStartupSound", 0, 1, false, 0, false) }),
            new Toggle("Suggested Actions", "Smart actions for copied phone numbers / dates.", "", "Windows Features", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SmartActionPlatform\\SmartClipboard", "Disabled", 0, 1, false, 0, false) }),
            new Toggle("UAC - User Account Control (restart needed)", "WARNING: turning UAC off also breaks Store apps and is a security risk.", "", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "EnableLUA", 1, 0, false, 1, false) }),
            new Toggle("News and Interests on the taskbar", "ON = the weather / news button is shown on the taskbar. OFF = hidden (Windows 10).", "10", "Windows Features", true, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Feeds", "ShellFeedsTaskbarViewMode", 0, 2, false, 0, false), new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Feeds", "EnableFeeds", 1, 0, false, 1, true) }),
            new Toggle("Widgets button on the taskbar", "ON = the Widgets (news / weather) button is shown on the taskbar. OFF = hidden (Windows 11).", "11", "Windows Features", true, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarDa", 1, 0, false, 1, false), new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Dsh", "AllowNewsAndInterests", 1, 0, false, 1, true) }),
            new Toggle("Weather / news icon on the taskbar (Windows 11)", "ON = the weather / news button is shown on the taskbar. OFF = hidden (Windows 11, the weather icon next to the tray).", "11", "Windows Features", true, new TV[] { new TV("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Feeds", "ShellFeedsTaskbarViewMode", 0, 2, false, 0, false), new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Feeds", "EnableFeeds", 1, 0, false, 1, true), new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Dsh", "AllowNewsAndInterests", 1, 0, false, 1, true) }),
            new Toggle("Windows Ink Workspace", "Pen / Ink Workspace.", "", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsInkWorkspace", "AllowWindowsInkWorkspace", 1, 0, false, 1, false) }),
            new Toggle("Xbox Game Bar", "Game Bar startup panel, Nexus button and Game Mode notifications.", "", "Windows Features", false, new TV[] { new TV("HKCU:\\Software\\Microsoft\\GameBar", "UseNexusForGameBarEnabled", 1, 0, false, 1, false), new TV("HKCU:\\Software\\Microsoft\\GameBar", "ShowStartupPanel", 1, 0, false, 1, false), new TV("HKCU:\\Software\\Microsoft\\GameBar", "ShowGameModeNotifications", 1, 0, false, 1, false) }),
            new Toggle("Mobile Devices Settings (Phone Link page)", "OFF = lock the page (DisableWindowsConsumerFeatures).", "11", "Windows Features", false, new TV[] { new TV("HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent", "DisableWindowsConsumerFeatures", 0, 1, false, 0, false) }),
            };
            string clsid = @"HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
            Toggle classic = new Toggle("Classic Right-Click Menu (Windows 11)", "Skip the \"Show more options\" step.", "11", "", false, null);
            classic.CustomGet = delegate { return Reg.KeyExists(clsid + @"\InprocServer32"); };
            classic.CustomOn = delegate { Reg.SetString(clsid + @"\InprocServer32", "", ""); };
            classic.CustomOff = delegate { Reg.DeleteKey(clsid); };
            all.Insert(Math.Min(15, all.Count), classic);
            string gal = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
            Toggle gallery = new Toggle("File Explorer Gallery (navigation pane)", "Adds the Gallery entry (Gallery_Enabler_v2).", "11", "Windows Features", false, null);
            gallery.CustomGet = delegate { return Reg.KeyExists(gal); };
            gallery.CustomOn = delegate { Reg.SetString(gal, "", "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"); };
            gallery.CustomOff = delegate { Reg.DeleteKey(gal); };
            all.Add(gallery);
            Toggle date = new Toggle("Date Format", "Short date format shown in the taskbar clock. Use d = day, M = month, y = year, for example dd/MM/yyyy.", "", "", false, null);
            date.IsDate = true;
            int ci = all.FindIndex(delegate(Toggle x) { return x.Name == "Hide Taskbar Clock"; });
            all.Insert(ci >= 0 ? ci + 1 : all.Count, date);
            Toggle search = new Toggle("Taskbar Search", "How the Search button looks on the taskbar (same choices as Settings > Personalization > Taskbar).", "", "", false, null);
            search.ChoiceP = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Search"; search.ChoiceN = "SearchboxTaskbarMode"; search.ChoiceDef = 2;
            if (win11) { search.ChoiceNames = new string[] { "Hide", "Search icon only", "Search icon and label", "Search box" }; search.ChoiceValues = new int[] { 0, 1, 2, 3 }; }
            else { search.ChoiceNames = new string[] { "Hide", "Search icon only", "Search box" }; search.ChoiceValues = new int[] { 0, 1, 2 }; }
            int si = all.FindIndex(delegate(Toggle x) { return x.Name.StartsWith("Taskbar Centered"); });
            all.Insert(si >= 0 ? si + 1 : all.Count, search);
            return all.FindAll(delegate(Toggle t) { return t.OS.Length == 0 || t.OS == os; });
        }

        public static bool GetState(Toggle t)
        {
            try
            {
                if (t.CustomGet != null) return t.CustomGet();
                foreach (TV v in t.Values)
                {
                    if (v.NoCheck) continue;
                    object cur = Reg.Get(v.P, v.N);
                    if (cur == null && v.Def != null) cur = v.Def;
                    if (Convert.ToString(cur) != Convert.ToString(v.On)) return false;
                }
                return true;
            }
            catch { return false; }
        }

        // throws only when every value failed (protected keys); returns the number of failed values
        public static int SetState(Toggle t, bool on)
        {
            if (t.CustomGet != null) { if (on) t.CustomOn(); else t.CustomOff(); return 0; }
            int fail = 0; string last = "";
            System.Text.StringBuilder detail = new System.Text.StringBuilder();
            foreach (TV v in t.Values)
            {
                object val = on ? v.On : v.Off;
                try { Reg.Set(v.P, v.N, val, v.IsString ? Microsoft.Win32.RegistryValueKind.String : Microsoft.Win32.RegistryValueKind.DWord); }
                catch (Exception ex) { fail++; last = ex.Message; detail.Append(" [" + v.P.Replace("HKCU:\\", "HKCU\\").Replace("HKLM:\\", "HKLM\\") + " \\ " + v.N + ": " + ex.Message + "]"); }
            }
            if (fail >= t.Values.Length) throw new InvalidOperationException(last + detail.ToString());
            return fail;
        }
    }
}
