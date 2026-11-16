# Native (C#) Rock Toolbox - حالة النقل

النسخة دي **مفيهاش PowerShell خالص**: `RockToolbox.exe` بيحمّل نفس واجهات `ui/*.xaml` وكل المنطق C#.
الـ Setup بيتعمل من `installer\\RockToolbox.iss` فقط (بيبني الـ exe بنفسه). النسخة القديمة اتنقلت لـ `reference-powershell/` كمرجع.

## كل الصفحات والمميزات اتنقلت
| الجزء | الملفات |
|---|---|
| Install (كتالوج + أيقونات + winget/Chocolatey + Show Installed) | `Pages/InstallPage.cs`, `Data/AppData.cs` |
| Install: Package ID + Winget Upgrade | `Pages/InstallExtras.cs` |
| Tweaks + Customize + Power plan + DNS | `Pages/TweaksPage.cs`, `Data/*` |
| Debloat (+ OneDrive / Edge / IE) | `Pages/DebloatPage.cs`, `Core/Removers.cs` |
| Config (features, fixes, activation, SSH, cursors, shortcut) | `Pages/ConfigPage.cs`, `assets/CursorPack.zip` |
| Updates / Windows Update / Drivers / Uninstall Apps | `Pages/UpdatesPage.cs`, `WindowsUpdatePage.cs`, `DriversPage.cs`, `UninstallAppsPage.cs` |
| System Info (+ Live) / Cleanup / Startup / Repair / God Mode | `Pages/*.cs` |
| Downloads (محرك متعدد الاتصالات) | `Pages/DownloadsPage.cs`, `Core/DlEngine.cs` |
| Wallpapers (+ Bing يومي بدون PowerShell) | `Pages/WallpapersPage.cs`, `daily/Daily.cs` |
| Settings، الثيمات والألوان، صورة الخلفية (opacity + blur) | `Pages/SettingsPage.cs`, `Core/Theme.cs`, `Core/BgFx.cs` |
| اللغات (العربية جاهزة + RTL، والباقي بيتحمّل من الريبو) | `Core/Lang.cs`, `lang/ar.json` |
| الأنيميشن (انتقال الصفحات) + التمرير السلس | `Core/Anim.cs`, `Core/SmoothScroll.cs` |
| الخلفية (tray) + التشغيل مع ويندوز + نسخة واحدة فقط | `Core/AppLife.cs`, `Core/TaskSched.cs` |
| الملف الشخصي، تصدير/استيراد الاختيارات، شاشة الترحيب، تثبيت في شريط المهام | `Pages/ProfileUi.cs`, `Pages/SettingsPage.cs` |

## فروق بسيطة عن النسخة القديمة
* مظهر "Modern" المتدرّج موجود من 4.0.0 (Settings > Classic look بيرجّع الأزرق القديم).
* شاشة الترحيب (4.0.0): نافذة مستقلة أول تشغيل فيها أنيميشن Hello بلغات متعددة، اللغة بالأعلام، اختيار الشكل (Classic / Modern) و فاتح/داكن، الاسم والصورة.
* الترجمة بتغطي واجهات XAML وأسماء التابات والأقسام؛ رسائل اللوج بتفضل بالإنجليزي. باقي اللغات غير العربية بتتحمّل من `lang/<code>.json` في الريبو.
* تغيير الثيم/اللون/الخلفية/اللغة بيتطبّق بإعادة تشغيل البرنامج تلقائياً.
* تويك "Memory Compression" بيتخطّى (مفيش API غير PowerShell).
* IPv6 بالريجستري (محتاج Restart). زر SPOTX بيفتح صفحة المشروع.
* الكود اتكتب من غير كومبايلر: اعمل Compile للـ `.iss` وابعت أي خطأ من `%TEMP%\\RockToolboxBuild\\build.log`.
