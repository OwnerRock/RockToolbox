<<<<<<< HEAD
﻿using System.Collections.Generic;

namespace RockToolbox.Native
{
    internal sealed class BloatApp
    {
        public string Cat, Name, Label; public bool Rec;
        public BloatApp(string cat, string name, string label, bool rec) { Cat = cat; Name = name; Label = label; Rec = rec; }
    }

    // DATA: debloat list (generated). Name may be a wildcard (*Disney*) or @OneDrive for the full OneDrive removal.
    internal static class BloatData
    {
        public static readonly string[] Cats = { "Microsoft Apps", "Microsoft Ecosystem", "Utilities & Productivity", "Bing & Web Services", "Developer Tools", "Xbox & Gaming", "Promoted & Third-party", "Legacy & Retired" };

        public static readonly List<BloatApp> All = new List<BloatApp>
        {
            new BloatApp("Microsoft Apps", "Microsoft.WindowsFeedbackHub", "Feedback Hub", true),
            new BloatApp("Microsoft Apps", "Microsoft.GetHelp", "Get Help", true),
            new BloatApp("Microsoft Apps", "Microsoft.Getstarted", "Tips / Get Started", true),
            new BloatApp("Microsoft Apps", "Microsoft.MicrosoftOfficeHub", "Microsoft 365", true),
            new BloatApp("Microsoft Apps", "MSTeams", "Microsoft Teams", true),
            new BloatApp("Microsoft Apps", "MicrosoftTeams", "Microsoft Teams (old)", true),
            new BloatApp("Microsoft Apps", "Microsoft.OutlookForWindows", "Outlook for Windows", false),
            new BloatApp("Microsoft Apps", "Microsoft.People", "People", true),
            new BloatApp("Microsoft Apps", "Microsoft.WindowsMaps", "Maps", true),
            new BloatApp("Microsoft Ecosystem", "MicrosoftWindows.CrossDevice", "Mobile Devices", false),
            new BloatApp("Microsoft Ecosystem", "Microsoft.YourPhone", "Phone Link", false),
            new BloatApp("Microsoft Ecosystem", "@OneDrive", "OneDrive (full removal)", false),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsCalculator", "Calculator", false),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsCamera", "Camera", false),
            new BloatApp("Utilities & Productivity", "Clipchamp.Clipchamp", "Clipchamp", true),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsAlarms", "Clock", false),
            new BloatApp("Utilities & Productivity", "Microsoft.ZuneMusic", "Media Player", false),
            new BloatApp("Utilities & Productivity", "Microsoft.ZuneVideo", "Movies & TV", true),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsNotepad", "Notepad", false),
            new BloatApp("Utilities & Productivity", "Microsoft.Paint", "Paint", false),
            new BloatApp("Utilities & Productivity", "Microsoft.Windows.Photos", "Photos", false),
            new BloatApp("Utilities & Productivity", "MicrosoftCorporationII.QuickAssist", "Quick Assist", false),
            new BloatApp("Utilities & Productivity", "Microsoft.ScreenSketch", "Snipping Tool", false),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsSoundRecorder", "Sound Recorder", false),
            new BloatApp("Utilities & Productivity", "Microsoft.MicrosoftStickyNotes", "Sticky Notes", false),
            new BloatApp("Utilities & Productivity", "Microsoft.Todos", "To Do", false),
            new BloatApp("Bing & Web Services", "Microsoft.BingSearch", "Bing Search", true),
            new BloatApp("Bing & Web Services", "*Copilot*", "Copilot", true),
            new BloatApp("Bing & Web Services", "Microsoft.BingNews", "News", true),
            new BloatApp("Bing & Web Services", "Microsoft.StartExperiencesApp", "Start Experiences App", false),
            new BloatApp("Bing & Web Services", "Microsoft.BingWeather", "Weather", false),
            new BloatApp("Bing & Web Services", "Microsoft.BingFinance", "Finance", true),
            new BloatApp("Bing & Web Services", "Microsoft.BingSports", "Sports", true),
            new BloatApp("Developer Tools", "Microsoft.Windows.DevHome", "Dev Home", true),
            new BloatApp("Developer Tools", "Microsoft.PowerAutomateDesktop", "Power Automate", true),
            new BloatApp("Xbox & Gaming", "Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", true),
            new BloatApp("Xbox & Gaming", "Microsoft.GamingApp", "Xbox App", false),
            new BloatApp("Xbox & Gaming", "Microsoft.XboxGamingOverlay", "Xbox Game Bar", false),
            new BloatApp("Xbox & Gaming", "Microsoft.XboxIdentityProvider", "Xbox Identity Provider", false),
            new BloatApp("Xbox & Gaming", "Microsoft.XboxSpeechToTextOverlay", "Xbox Speech To Text Overlay", false),
            new BloatApp("Xbox & Gaming", "Microsoft.Xbox.TCUI", "Xbox TCUI", false),
            new BloatApp("Promoted & Third-party", "*CandyCrush*", "Candy Crush (all)", true),
            new BloatApp("Promoted & Third-party", "*Disney*", "Disney+", true),
            new BloatApp("Promoted & Third-party", "*TikTok*", "TikTok", true),
            new BloatApp("Promoted & Third-party", "*Netflix*", "Netflix", false),
            new BloatApp("Promoted & Third-party", "*Facebook*", "Facebook", true),
            new BloatApp("Promoted & Third-party", "*Instagram*", "Instagram", true),
            new BloatApp("Promoted & Third-party", "*Spotify*", "Spotify (Store version)", false),
            new BloatApp("Legacy & Retired", "Microsoft.549981C3F5F10", "Cortana", true),
            new BloatApp("Legacy & Retired", "Microsoft.MixedReality.Portal", "Mixed Reality Portal", true),
            new BloatApp("Legacy & Retired", "Microsoft.Microsoft3DViewer", "3D Viewer", true),
            new BloatApp("Legacy & Retired", "Microsoft.3DBuilder", "3D Builder", true),
            new BloatApp("Legacy & Retired", "Microsoft.SkypeApp", "Skype", true),
            new BloatApp("Legacy & Retired", "Microsoft.WindowsCommunicationsApps", "Mail and Calendar (old)", false),
        };
    }
}
=======
﻿using System.Collections.Generic;

namespace RockToolbox.Native
{
    internal sealed class BloatApp
    {
        public string Cat, Name, Label; public bool Rec;
        public BloatApp(string cat, string name, string label, bool rec) { Cat = cat; Name = name; Label = label; Rec = rec; }
    }

    // DATA: debloat list (generated). Name may be a wildcard (*Disney*) or @OneDrive for the full OneDrive removal.
    internal static class BloatData
    {
        public static readonly string[] Cats = { "Microsoft Apps", "Microsoft Ecosystem", "Utilities & Productivity", "Bing & Web Services", "Developer Tools", "Xbox & Gaming", "Promoted & Third-party", "Legacy & Retired" };

        public static readonly List<BloatApp> All = new List<BloatApp>
        {
            new BloatApp("Microsoft Apps", "Microsoft.WindowsFeedbackHub", "Feedback Hub", true),
            new BloatApp("Microsoft Apps", "Microsoft.GetHelp", "Get Help", true),
            new BloatApp("Microsoft Apps", "Microsoft.Getstarted", "Tips / Get Started", true),
            new BloatApp("Microsoft Apps", "Microsoft.MicrosoftOfficeHub", "Microsoft 365", true),
            new BloatApp("Microsoft Apps", "MSTeams", "Microsoft Teams", true),
            new BloatApp("Microsoft Apps", "MicrosoftTeams", "Microsoft Teams (old)", true),
            new BloatApp("Microsoft Apps", "Microsoft.OutlookForWindows", "Outlook for Windows", false),
            new BloatApp("Microsoft Apps", "Microsoft.People", "People", true),
            new BloatApp("Microsoft Apps", "Microsoft.WindowsMaps", "Maps", true),
            new BloatApp("Microsoft Ecosystem", "MicrosoftWindows.CrossDevice", "Mobile Devices", false),
            new BloatApp("Microsoft Ecosystem", "Microsoft.YourPhone", "Phone Link", false),
            new BloatApp("Microsoft Ecosystem", "@OneDrive", "OneDrive (full removal)", false),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsCalculator", "Calculator", false),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsCamera", "Camera", false),
            new BloatApp("Utilities & Productivity", "Clipchamp.Clipchamp", "Clipchamp", true),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsAlarms", "Clock", false),
            new BloatApp("Utilities & Productivity", "Microsoft.ZuneMusic", "Media Player", false),
            new BloatApp("Utilities & Productivity", "Microsoft.ZuneVideo", "Movies & TV", true),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsNotepad", "Notepad", false),
            new BloatApp("Utilities & Productivity", "Microsoft.Paint", "Paint", false),
            new BloatApp("Utilities & Productivity", "Microsoft.Windows.Photos", "Photos", false),
            new BloatApp("Utilities & Productivity", "MicrosoftCorporationII.QuickAssist", "Quick Assist", false),
            new BloatApp("Utilities & Productivity", "Microsoft.ScreenSketch", "Snipping Tool", false),
            new BloatApp("Utilities & Productivity", "Microsoft.WindowsSoundRecorder", "Sound Recorder", false),
            new BloatApp("Utilities & Productivity", "Microsoft.MicrosoftStickyNotes", "Sticky Notes", false),
            new BloatApp("Utilities & Productivity", "Microsoft.Todos", "To Do", false),
            new BloatApp("Bing & Web Services", "Microsoft.BingSearch", "Bing Search", true),
            new BloatApp("Bing & Web Services", "*Copilot*", "Copilot", true),
            new BloatApp("Bing & Web Services", "Microsoft.BingNews", "News", true),
            new BloatApp("Bing & Web Services", "Microsoft.StartExperiencesApp", "Start Experiences App", false),
            new BloatApp("Bing & Web Services", "Microsoft.BingWeather", "Weather", false),
            new BloatApp("Bing & Web Services", "Microsoft.BingFinance", "Finance", true),
            new BloatApp("Bing & Web Services", "Microsoft.BingSports", "Sports", true),
            new BloatApp("Developer Tools", "Microsoft.Windows.DevHome", "Dev Home", true),
            new BloatApp("Developer Tools", "Microsoft.PowerAutomateDesktop", "Power Automate", true),
            new BloatApp("Xbox & Gaming", "Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", true),
            new BloatApp("Xbox & Gaming", "Microsoft.GamingApp", "Xbox App", false),
            new BloatApp("Xbox & Gaming", "Microsoft.XboxGamingOverlay", "Xbox Game Bar", false),
            new BloatApp("Xbox & Gaming", "Microsoft.XboxIdentityProvider", "Xbox Identity Provider", false),
            new BloatApp("Xbox & Gaming", "Microsoft.XboxSpeechToTextOverlay", "Xbox Speech To Text Overlay", false),
            new BloatApp("Xbox & Gaming", "Microsoft.Xbox.TCUI", "Xbox TCUI", false),
            new BloatApp("Promoted & Third-party", "*CandyCrush*", "Candy Crush (all)", true),
            new BloatApp("Promoted & Third-party", "*Disney*", "Disney+", true),
            new BloatApp("Promoted & Third-party", "*TikTok*", "TikTok", true),
            new BloatApp("Promoted & Third-party", "*Netflix*", "Netflix", false),
            new BloatApp("Promoted & Third-party", "*Facebook*", "Facebook", true),
            new BloatApp("Promoted & Third-party", "*Instagram*", "Instagram", true),
            new BloatApp("Promoted & Third-party", "*Spotify*", "Spotify (Store version)", false),
            new BloatApp("Legacy & Retired", "Microsoft.549981C3F5F10", "Cortana", true),
            new BloatApp("Legacy & Retired", "Microsoft.MixedReality.Portal", "Mixed Reality Portal", true),
            new BloatApp("Legacy & Retired", "Microsoft.Microsoft3DViewer", "3D Viewer", true),
            new BloatApp("Legacy & Retired", "Microsoft.3DBuilder", "3D Builder", true),
            new BloatApp("Legacy & Retired", "Microsoft.SkypeApp", "Skype", true),
            new BloatApp("Legacy & Retired", "Microsoft.WindowsCommunicationsApps", "Mail and Calendar (old)", false),
        };
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
