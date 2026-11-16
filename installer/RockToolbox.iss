; ============================================================================================================
;  Rock Toolbox - installer script (Inno Setup 6.6 or newer recommended)
;
;  HOW TO BUILD:  open this file in Inno Setup and press Compile (F9).  The finished setup is written to  ..\out
;
;  This script also compiles the program for you: while it is being compiled it runs the C# compiler that is built
;  into Windows (csc.exe, .NET Framework 4) on the sources in ..\src and ..\daily. The temporary build output goes to
;  %TEMP%\RockToolboxBuild  (log: build.log there). If that step fails, the compile stops with an error.
;  Nothing else is needed - no Visual Studio, no .NET SDK.
; ============================================================================================================

#define AppName      "Rock Toolbox"
#define AppVersion   "4.1.0"
#define AppPublisher "Rock Tech"
#define AppURL       "https://github.com/OwnerRock/RockToolbox"
#define AppExe       "RockToolbox.exe"

; ---------- 1) build RockToolbox.exe and RockToolboxDaily.exe (compile-time, via the preprocessor) ----------
#define RootDir  RemoveBackslash(ExtractFilePath(RemoveBackslash(SourcePath)))
#define BuildDir GetEnv("TEMP") + "\RockToolboxBuild"
#define FwDir    GetEnv("WINDIR") + "\Microsoft.NET\Framework64\v4.0.30319"
#if !FileExists(FwDir + "\csc.exe")
  #undef FwDir
  #define FwDir  GetEnv("WINDIR") + "\Microsoft.NET\Framework\v4.0.30319"
#endif
#define Csc      FwDir + "\csc.exe"
#if !FileExists(Csc)
  #error The C# compiler was not found. Windows 10/11 include it in C:\Windows\Microsoft.NET\Framework64\v4.0.30319
#endif

#define Refs '/lib:"' + FwDir + '\WPF" /reference:PresentationFramework.dll /reference:PresentationCore.dll /reference:WindowsBase.dll /reference:System.Xaml.dll /reference:System.dll /reference:System.Xml.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.ServiceProcess.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll'
#define MainCmd  '/c ""' + Csc + '" /nologo /target:winexe /optimize+ /out:"' + BuildDir + '\RockToolbox.exe" /win32icon:"' + RootDir + '\assets\RockToolbox.ico" /win32manifest:"' + RootDir + '\app.manifest" ' + Refs + ' "' + RootDir + '\src\Program.cs" "' + RootDir + '\src\Core\*.cs" "' + RootDir + '\src\Data\*.cs" "' + RootDir + '\src\Pages\*.cs" > "' + BuildDir + '\build.log" 2>&1"'
#define RdlCmd   '/c ""' + Csc + '" /nologo /target:winexe /optimize+ /out:"' + BuildDir + '\RDownloader.exe" /win32icon:"' + RootDir + '\RDownloader\assets\RDownloader.ico" /win32manifest:"' + RootDir + '\RDownloader\app.manifest" ' + Refs + ' "' + RootDir + '\RDownloader\src\Program.cs" "' + RootDir + '\RDownloader\src\Core\*.cs" "' + RootDir + '\RDownloader\src\Pages\*.cs" > "' + BuildDir + '\build-rdl.log" 2>&1"'
#define DailyCmd '/c ""' + Csc + '" /nologo /target:winexe /optimize+ /out:"' + BuildDir + '\RockToolboxDaily.exe" /win32icon:"' + RootDir + '\assets\RockToolbox.ico" /reference:System.Web.Extensions.dll /reference:System.Core.dll /reference:Microsoft.CSharp.dll "' + RootDir + '\daily\Daily.cs" > "' + BuildDir + '\build-daily.log" 2>&1"'

#expr Exec("cmd.exe", '/c if not exist "' + BuildDir + '" mkdir "' + BuildDir + '"', RootDir, 1, SW_HIDE)
#expr Exec("cmd.exe", '/c del /q "' + BuildDir + '\*.exe" 2>nul', RootDir, 1, SW_HIDE)
#expr Exec("cmd.exe", MainCmd, RootDir, 1, SW_HIDE)
#expr Exec("cmd.exe", DailyCmd, RootDir, 1, SW_HIDE)
#expr Exec("cmd.exe", RdlCmd, RootDir, 1, SW_HIDE)

; ---------- winget (App Installer) + Chocolatey, installed quietly by the setup (files come from the redist folder) ----------
#define RedistDir  RootDir + "\redist"
#define WgBundle   RedistDir + "\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle"
#define WgDepsZip  RedistDir + "\DesktopAppInstaller_Dependencies.zip"
#if FileExists(WgBundle)
  #if FileExists(WgDepsZip)
    #expr Exec("cmd.exe", '/c rmdir /s /q "' + BuildDir + '\wgdeps" 2>nul & mkdir "' + BuildDir + '\wgdeps" & tar -xf "' + WgDepsZip + '" -C "' + BuildDir + '\wgdeps" > "' + BuildDir + '\build-redist.log" 2>&1', RootDir, 1, SW_HIDE)
    #define HaveWinget
  #else
    #pragma warning "redist\DesktopAppInstaller_Dependencies.zip is missing - winget will NOT be bundled."
  #endif
#else
  #pragma warning "redist\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle is missing - winget will NOT be bundled."
#endif
; the package keeps its version in the file name (chocolatey.2.x.y.nupkg): copy it to a fixed name first
#expr Exec("cmd.exe", '/c del /q "' + BuildDir + '\chocolatey.nupkg" 2>nul & for %f in ("' + RedistDir + '\chocolatey*.nupkg") do copy /y "%f" "' + BuildDir + '\chocolatey.nupkg" >nul 2>nul', RootDir, 1, SW_HIDE)
#if FileExists(BuildDir + "\chocolatey.nupkg")
  #expr Exec("cmd.exe", '/c rmdir /s /q "' + BuildDir + '\choco" 2>nul & mkdir "' + BuildDir + '\choco" & tar -xf "' + BuildDir + '\chocolatey.nupkg" -C "' + BuildDir + '\choco" >> "' + BuildDir + '\build-redist.log" 2>&1', RootDir, 1, SW_HIDE)
  #if FileExists(BuildDir + "\choco\tools\chocolateyInstall\choco.exe")
    #define HaveChoco
  #else
    #pragma warning "The Chocolatey package in redist could not be unpacked (see build-redist.log) - Chocolatey will NOT be bundled."
  #endif
#else
  #pragma warning "redist\chocolatey*.nupkg is missing - Chocolatey will NOT be bundled."
#endif
; keep a copy of the compiler output next to the project so it is easy to find / send
#expr Exec("cmd.exe", '/c copy /y "' + BuildDir + '\build.log" "' + RootDir + '\build.log" >nul 2>nul & copy /y "' + BuildDir + '\build-daily.log" "' + RootDir + '\build-daily.log" >nul 2>nul', RootDir, 1, SW_HIDE)
#if !FileExists(BuildDir + "\RockToolbox.exe")
  #error Compiling RockToolbox.exe failed. Open build.log in the project folder (copy: %TEMP%\RockToolboxBuild\build.log) to see the errors.
#endif
#if !FileExists(BuildDir + "\RDownloader.exe")
  #error Compiling RDownloader.exe failed. Open %TEMP%\RockToolboxBuild\build-rdl.log to see the errors.
#endif
#if !FileExists(BuildDir + "\RockToolboxDaily.exe")
  #error Compiling RockToolboxDaily.exe failed. Open build-daily.log in the project folder to see the errors.
#endif

; ---------- 2) the installer ----------
[Setup]
AppId={{6F2C9B1E-4A57-4D3B-9E0A-5C8D17B2A4F3}
ChangesAssociations=yes
ChangesEnvironment=yes
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
MinVersion=10.0
OutputDir=..\out
OutputBaseFilename=RockToolbox-Setup-{#AppVersion}
SetupIconFile=..\assets\RockToolbox.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=yes
UsePreviousLanguage=no
#if Ver >= 0x06060000
; Inno Setup 6.6+: the installer follows the Windows light / dark mode
WizardStyle=modern dynamic
#else
WizardStyle=modern
#endif
#if Ver >= 0x06030000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#else
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic";  MessagesFile: "compiler:Default.isl,Arabic.isl"

[CustomMessages]
english.TaskPin=Pin to the &taskbar (Windows Explorer restarts once)
english.TaskStartup=Start Rock Toolbox &with Windows (runs quietly in the tray)
english.TaskGroupStartup=Startup:
english.SettingUpPkg=Setting up the package managers (winget)...
english.TaskRdl=Install &R-Downloader (a separate, fast download manager with a browser extension)
english.TaskGroupRdl=R-Downloader:
english.TaskRdlRlist=Open .Rlist files with R-Downloader
english.TaskRdlStartup=Start R-Downloader with Windows (runs quietly in the tray)
english.TaskRdlDesktop=R-Downloader desktop icon
english.DeleteData=Also delete Rock Toolbox settings and downloaded data?
arabic.TaskPin=تثبيت في &شريط المهام (سيُعاد تشغيل مستكشف ويندوز مرة واحدة)
arabic.TaskStartup=تشغيل Rock Toolbox &مع بدء ويندوز (يعمل بهدوء في الخلفية بجوار الساعة)
arabic.TaskGroupStartup=بدء التشغيل:
arabic.SettingUpPkg=جاري تجهيز مديري الحزم (winget)...
arabic.TaskRdl=تثبيت &R-Downloader (برنامج تحميل سريع منفصل مع إضافة للمتصفح)
arabic.TaskGroupRdl=R-Downloader:
arabic.TaskRdlRlist=فتح ملفات .Rlist بواسطة R-Downloader
arabic.TaskRdlStartup=تشغيل R-Downloader مع بدء ويندوز (يعمل بهدوء بجوار الساعة)
arabic.TaskRdlDesktop=أيقونة R-Downloader على سطح المكتب
arabic.DeleteData=هل تريد أيضاً حذف إعدادات Rock Toolbox والبيانات المحمّلة؟

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "taskbarpin";  Description: "{cm:TaskPin}";            GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup";     Description: "{cm:TaskStartup}";        GroupDescription: "{cm:TaskGroupStartup}"; Flags: unchecked
Name: "rdl";         Description: "{cm:TaskRdl}";            GroupDescription: "{cm:TaskGroupRdl}"; Flags: unchecked
Name: "rdl\rlist";   Description: "{cm:TaskRdlRlist}";       Flags: unchecked
Name: "rdl\startup"; Description: "{cm:TaskRdlStartup}";     Flags: unchecked
Name: "rdl\desktop"; Description: "{cm:TaskRdlDesktop}";     Flags: unchecked

[Files]
#ifdef HaveWinget
Source: "{#WgBundle}";                                   DestDir: "{tmp}"; DestName: "wg.msixbundle"; Flags: deleteafterinstall; Check: WingetMissing
Source: "{#BuildDir}\wgdeps\x64\*.appx";                DestDir: "{tmp}\wgdeps"; Flags: deleteafterinstall skipifsourcedoesntexist; Check: WingetMissing
#endif
#ifdef HaveChoco
Source: "{#BuildDir}\choco\tools\chocolateyInstall\*";  DestDir: "{commonappdata}\chocolatey"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: ChocoMissing
Source: "{#BuildDir}\choco\tools\chocolateyInstall\choco.exe"; DestDir: "{commonappdata}\chocolatey\bin"; Flags: ignoreversion; Check: ChocoMissing
#endif
; ----- R-Downloader (optional, installed into its own folder; built from ..\RDownloader\src by the compile step above) -----
Source: "{#BuildDir}\RDownloader.exe";       DestDir: "{autopf}\R-Downloader"; Flags: ignoreversion; Tasks: rdl
Source: "..\RDownloader\assets\*";         DestDir: "{autopf}\R-Downloader"; Flags: ignoreversion; Tasks: rdl
Source: "..\RDownloader\lang\*";           DestDir: "{autopf}\R-Downloader\lang"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: rdl
Source: "..\RDownloader\ui\*";             DestDir: "{autopf}\R-Downloader\ui"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: rdl
Source: "..\RDownloader\extension\*";      DestDir: "{autopf}\R-Downloader\extension"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: rdl
Source: "{#BuildDir}\{#AppExe}";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\RockToolboxDaily.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\RockToolbox.ico";        DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\CursorPack.zip";         DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\lang\*";                        DestDir: "{app}\lang"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ui\*";                          DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
#ifdef HaveChoco
Name: "{commonappdata}\chocolatey\bin"; Check: ChocoMissing
Name: "{commonappdata}\chocolatey\lib"; Check: ChocoMissing
#endif

[Registry]
#ifdef HaveChoco
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "ChocolateyInstall"; ValueData: "{commonappdata}\chocolatey"; Check: ChocoMissing
#endif
; .Rlist = a playlist of download links; double-click opens it in R-Downloader (removed on uninstall)
Root: HKA; Subkey: "Software\Classes\.Rlist"; ValueType: string; ValueName: ""; ValueData: "RDownloader.Rlist"; Flags: uninsdeletevalue; Tasks: rdl\rlist
Root: HKA; Subkey: "Software\Classes\.Rlist\OpenWithProgids"; ValueType: string; ValueName: "RDownloader.Rlist"; ValueData: ""; Flags: uninsdeletevalue; Tasks: rdl\rlist
Root: HKA; Subkey: "Software\Classes\RDownloader.Rlist"; ValueType: string; ValueName: ""; ValueData: "R-Downloader download list"; Flags: uninsdeletekey; Tasks: rdl\rlist
Root: HKA; Subkey: "Software\Classes\RDownloader.Rlist\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{autopf}\R-Downloader\RRlist.ico"; Tasks: rdl\rlist
Root: HKA; Subkey: "Software\Classes\RDownloader.Rlist\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{autopf}\R-Downloader\RDownloader.exe"" ""%1"""; Tasks: rdl\rlist
Root: HKA; Subkey: "Software\Classes\RDownloader.Rlist\shell\edit"; ValueType: string; ValueName: ""; ValueData: "Edit in Notepad"; Tasks: rdl\rlist
Root: HKA; Subkey: "Software\Classes\RDownloader.Rlist\shell\edit\command"; ValueType: string; ValueName: ""; ValueData: """{win}\notepad.exe"" ""%1"""; Tasks: rdl\rlist

[Icons]
Name: "{group}\R-Downloader";                       Filename: "{autopf}\R-Downloader\RDownloader.exe"; IconFilename: "{autopf}\R-Downloader\RDownloader.ico"; Tasks: rdl
Name: "{autodesktop}\R-Downloader";                 Filename: "{autopf}\R-Downloader\RDownloader.exe"; IconFilename: "{autopf}\R-Downloader\RDownloader.ico"; Tasks: rdl\desktop
Name: "{group}\{#AppName}";                          Filename: "{app}\{#AppExe}"; IconFilename: "{app}\RockToolbox.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}";    Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";                    Filename: "{app}\{#AppExe}"; IconFilename: "{app}\RockToolbox.ico"; Tasks: desktopicon

[Run]
#ifdef HaveWinget
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""$ErrorActionPreference='SilentlyContinue'; $d=@(Get-ChildItem '{tmp}\wgdeps' -Filter *.appx | ForEach-Object {{ $_.FullName }}); Add-AppxProvisionedPackage -Online -PackagePath '{tmp}\wg.msixbundle' -DependencyPackagePath $d -SkipLicense | Out-Null; Add-AppxPackage -Path '{tmp}\wg.msixbundle' -DependencyPath $d"""; StatusMsg: "{cm:SettingUpPkg}"; Flags: runhidden waituntilterminated; Check: WingetMissing
#endif
; the program itself creates the scheduled task / pin (it runs elevated like this installer, so there is no extra prompt)
Filename: "{app}\{#AppExe}"; Parameters: "--enable-startup"; Flags: runhidden waituntilterminated; Tasks: startup
Filename: "{app}\{#AppExe}"; Parameters: "--pin-taskbar";    Flags: runhidden waituntilterminated; Tasks: taskbarpin
Filename: "{autopf}\R-Downloader\RDownloader.exe"; Parameters: "--enable-startup"; Flags: runhidden waituntilterminated; Tasks: rdl\startup
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "{autopf}\R-Downloader\RDownloader.exe"; Parameters: "--quit";            Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "RdlQuit"
Filename: "{autopf}\R-Downloader\RDownloader.exe"; Parameters: "--disable-startup"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "RdlStartupOff"
Filename: "{app}\{#AppExe}"; Parameters: "--quit";            Flags: runhidden waituntilterminated; RunOnceId: "RockQuit"
Filename: "{app}\{#AppExe}"; Parameters: "--disable-startup"; Flags: runhidden waituntilterminated; RunOnceId: "RockStartupOff"
Filename: "{app}\{#AppExe}"; Parameters: "--unpin-taskbar";   Flags: runhidden waituntilterminated; RunOnceId: "RockUnpin"

[Code]
// winget (App Installer) / Chocolatey are only set up when they are not there yet
function WingetMissing(): Boolean;
begin
  Result := not FileExists(ExpandConstant('{localappdata}\Microsoft\WindowsApps\winget.exe'));
end;

function ChocoMissing(): Boolean;
begin
  Result := not FileExists(ExpandConstant('{commonappdata}\chocolatey\choco.exe'));
end;

// adds the Chocolatey "bin" folder to the machine PATH (once)
procedure AddMachinePath(Dir: String);
var
  P: String;
  K: String;
begin
  K := 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  if not RegQueryStringValue(HKLM, K, 'Path', P) then P := '';
  if Pos(';' + Lowercase(Dir) + ';', ';' + Lowercase(P) + ';') > 0 then Exit;
  if (P <> '') and (P[Length(P)] <> ';') then P := P + ';';
  RegWriteExpandStringValue(HKLM, K, 'Path', P + Dir);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
#ifdef HaveChoco
    if FileExists(ExpandConstant('{commonappdata}\chocolatey\bin\choco.exe')) then
      AddMachinePath(ExpandConstant('{commonappdata}\chocolatey\bin'));
#endif
  end;
end;

// Rock Toolbox keeps running in the tray when its window is closed, so ask it to quit before files are replaced / removed
procedure StopRunningApp();
var
  Code: Integer;
  Exe: String;
begin
  Exe := ExpandConstant('{app}\{#AppExe}');
  if FileExists(Exe) then
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(1500);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

// after uninstalling, offer to remove the settings / downloaded data kept in %LOCALAPPDATA%\RockToolbox
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    if not UninstallSilent then
      if MsgBox(CustomMessage('DeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        begin
          DelTree(ExpandConstant('{localappdata}\RockToolbox'), True, True, True);
          DelTree(ExpandConstant('{localappdata}\RDownloader'), True, True, True);
        end;
end;
