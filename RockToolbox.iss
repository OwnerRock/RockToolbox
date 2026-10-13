; Rock Toolbox - Inno Setup script
; Compile: "C:\Users\Rock\AppData\Local\Programs\Inno Setup 6\ISCC.exe" RockToolbox.iss

#define MyAppName "Rock Toolbox"
#define MyAppVersion "1.0"
#define MyAppPublisher "Rock Tech"

[Setup]
AppId={{8F4B0C6E-2A71-4E55-9C3D-7A1B5E0D9F42}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\RockToolbox
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=Setup-RockToolbox
SetupIconFile=RockInstaller.ico
UninstallDisplayIcon={app}\RockLogo.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "RockToolbox.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Start-RockToolbox.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "RockT???.ps1"; Excludes: "RockToolbox.ps1,RockToolbox-Installer.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "RockLogo.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "RockInstaller.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userdesktop}\{#MyAppName}"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -STA -File ""{app}\RockToolbox.ps1"""; \
  WorkingDir: "{app}"; IconFilename: "{app}\RockLogo.ico"; Tasks: desktopicon
Name: "{userprograms}\{#MyAppName}"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -STA -File ""{app}\RockToolbox.ps1"""; \
  WorkingDir: "{app}"; IconFilename: "{app}\RockLogo.ico"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -STA -File ""{app}\RockToolbox.ps1"""; \
  WorkingDir: "{app}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
