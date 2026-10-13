$BloatCats = @('Microsoft Apps', 'Microsoft Ecosystem', 'Utilities & Productivity', 'Bing & Web Services', 'Developer Tools', 'Xbox & Gaming', 'Promoted & Third-party', 'Legacy & Retired')

# ------------------------------------------------------------------
# DATA: customize toggles (ON = feature enabled; applied immediately)
# ------------------------------------------------------------------
$ExpAdv   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$Persona  = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
$ClsidKey = 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}'

$Toggles = @(
    @{ Name='BSoD Verbose Mode'; Desc='Show technical details on blue screens.'
       Values=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl';N='DisplayParameters';On=1;Off=0;Def=0} ) },
    @{ Name='Dark Theme for Windows'; Desc='Dark theme for Windows and apps.'
       Values=@( @{P=$Persona;N='AppsUseLightTheme';On=0;Off=1;Def=1}, @{P=$Persona;N='SystemUsesLightTheme';On=0;Off=1;Def=1} ) },
    @{ Name='Transparency Effects'; Desc='Acrylic / Mica transparency on Start, taskbar and windows.'
       Values=@( @{P=$Persona;N='EnableTransparency';On=1;Off=0;Def=1} ) },
    @{ Name='Enable Long Paths'; Desc='Allow file paths longer than 260 characters.'
       Values=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem';N='LongPathsEnabled';On=1;Off=0;Def=0} ) },
    @{ Name='File Explorer File Extensions'; Desc='Show .txt, .exe, .jpg ... in File Explorer.'
       Values=@( @{P=$ExpAdv;N='HideFileExt';On=0;Off=1;Def=1} ) },
    @{ Name='File Explorer Hidden Files'; Desc='Display hidden files and folders.'
       Values=@( @{P=$ExpAdv;N='Hidden';On=1;Off=2;Def=2} ) },
    @{ Name='File Explorer Opens to This PC'; Desc='Instead of Home / Quick access.'
       Values=@( @{P=$ExpAdv;N='LaunchTo';On=1;Off=2;Def=3} ) },
    @{ Name='Compact View in File Explorer'; Desc='Less spacing between items.'
       Values=@( @{P=$ExpAdv;N='UseCompactMode';On=1;Off=0;Def=0} ) },
    @{ Name='Game Mode'; Desc='Windows Game Mode.'
       Values=@( @{P='HKCU:\Software\Microsoft\GameBar';N='AutoGameModeEnabled';On=1;Off=0;Def=1} ) },
    @{ Name='Logon Verbose Mode'; Desc='Show detailed status messages during sign-in/out.'
       Values=@( @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System';N='VerboseStatus';On=1;Off=0;Def=0} ) },
    @{ Name='Mouse Acceleration'; Desc='Enhance pointer precision (turn OFF for 1:1 mouse movement).'
       Values=@( @{P='HKCU:\Control Panel\Mouse';N='MouseSpeed';On='1';Off='0';T='String';Def='1'},
                 @{P='HKCU:\Control Panel\Mouse';N='MouseThreshold1';On='6';Off='0';T='String';Def='6'},
                 @{P='HKCU:\Control Panel\Mouse';N='MouseThreshold2';On='10';Off='0';T='String';Def='10'} ) },
    @{ Name='Num Lock on Startup'; Desc='Turn Num Lock on at sign-in.'
       Values=@( @{P='HKCU:\Control Panel\Keyboard';N='InitialKeyboardIndicators';On='2';Off='0';T='String';Def='0'} ) },
    @{ Name='Scrollbars Always Visible'; Desc='Do not auto-hide scrollbars.'
       Values=@( @{P='HKCU:\Control Panel\Accessibility';N='DynamicScrollbars';On=0;Off=1;Def=1} ) },
    @{ Name='Sticky Keys'; Desc='The Shift x5 popup (OFF = disabled).'
       Values=@( @{P='HKCU:\Control Panel\Accessibility\StickyKeys';N='Flags';On='510';Off='58';T='String';Def='510'} ) },
    @{ Name='Window Snapping'; Desc='Snap windows to screen edges.'
       Values=@( @{P='HKCU:\Control Panel\Desktop';N='WindowArrangementActive';On='1';Off='0';T='String';Def='1'} ) },
    @{ Name='Classic Right-Click Menu (Windows 11)'; Desc='Skip the "Show more options" step.'
       Custom=@{
           Get={ Test-Path -LiteralPath "$ClsidKey\InprocServer32" }
           On={  New-Item -Path "$ClsidKey\InprocServer32" -Force -Value '' | Out-Null }
           Off={ Remove-Item -LiteralPath $ClsidKey -Recurse -Force -ErrorAction SilentlyContinue }
       } },
    @{ Name='Show "This PC" Icon on Desktop'; Desc='Adds the This PC icon to the desktop.'
       Values=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel';N='{20D04FE0-3AEA-1069-A2D8-08002B30309D}';On=0;Off=1;Def=1} ) },
    @{ Name='Taskbar Centered Icons (Windows 11)'; Desc='OFF = icons aligned to the left.'
       Values=@( @{P=$ExpAdv;N='TaskbarAl';On=1;Off=0;Def=1} ) },
    @{ Name='Taskbar Search'; Desc='How the Search button looks on the taskbar (same choices as Settings > Personalization > Taskbar).'
       Choice=@{ P='HKCU:\Software\Microsoft\Windows\CurrentVersion\Search'; N='SearchboxTaskbarMode'; Def=2
                 Options=[ordered]@{ 'Hide'=0; 'Search icon only'=1; 'Search icon and label'=2; 'Search box'=3 } } },
    @{ Name='Taskbar Task View Icon'; Desc='Task View button.'
       Values=@( @{P=$ExpAdv;N='ShowTaskViewButton';On=1;Off=0;Def=1} ) },
    @{ Name='Taskbar Chat / Teams Icon'; Desc='Chat button.'
       Values=@( @{P=$ExpAdv;N='TaskbarMn';On=1;Off=0;Def=1} ) },
    @{ Name='Taskbar Clock Seconds'; Desc='Clock shows HH:MM:SS.'
       Values=@( @{P=$ExpAdv;N='ShowSecondsInSystemClock';On=1;Off=0;Def=0} ) },
    # ---- Windows Features: embedded from Registry.rar (Enable/Disable .reg pairs merged into switches) ----
    @{ Group='Windows Features'; Name='Ambient Lighting'; Desc='Dynamic lighting for RGB devices.'
       Values=@( @{P='HKCU:\Software\Microsoft\Lighting';N='AmbientLightingEnabled';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Automatic Driver Installation (Windows Update)'; Desc='OFF = exclude drivers from Windows Update and stop searching Windows Update for drivers.'
       Values=@( @{P='HKLM:\SOFTWARE\Microsoft\PolicyManager\current\device\Update';N='ExcludeWUDriversInQualityUpdate';On=0;Off=1;Def=0},
                 @{P='HKLM:\SOFTWARE\Microsoft\PolicyManager\default\Update';N='ExcludeWUDriversInQualityUpdate';On=0;Off=1;Def=0},
                 @{P='HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings';N='ExcludeWUDriversInQualityUpdate';On=0;Off=1;Def=0},
                 @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate';N='ExcludeWUDriversInQualityUpdate';On=0;Off=1;Def=0},
                 @{P='HKLM:\SOFTWARE\Microsoft\PolicyManager\default\Update\ExcludeWUDriversInQualityUpdate';N='value';On=0;Off=1;Def=0},
                 @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata';N='PreventDeviceMetadataFromNetwork';On=0;Off=1;Def=0},
                 @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching';N='SearchOrderConfig';On=0;Off=0;NoCheck=$true},
                 @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching';N='DontSearchWindowsUpdate';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='Background Apps'; Desc='Let Store apps run in the background.'
       Values=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications';N='GlobalUserDisabled';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='Error Reporting'; Desc='Windows Error Reporting.'
       Values=@( @{P='HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting';N='Disabled';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='Lock Screen'; Desc='Show the lock screen before sign-in (OFF = skip it).'
       Values=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization';N='NoLockScreen';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='Snap Assist Flyout'; Desc='Layout flyout when hovering the maximize button (Windows 11).'
       Values=@( @{P=$ExpAdv;N='EnableSnapAssistFlyout';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Snap Bar'; Desc='Snap layouts bar when dragging a window to the top.'
       Values=@( @{P=$ExpAdv;N='EnableSnapBar';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Startup Sound'; Desc='The Windows startup chime.'
       Values=@( @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System';N='DisableStartupSound';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='Suggested Actions'; Desc='Smart actions for copied phone numbers / dates.'
       Values=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\SmartActionPlatform\SmartClipboard';N='Disabled';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='UAC - User Account Control (restart needed)'; Desc='WARNING: turning UAC off also breaks Store apps and is a security risk.'
       Values=@( @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System';N='EnableLUA';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Widgets (system policy)'; Desc='News and Interests / Widgets board.'
       Values=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Dsh';N='AllowNewsAndInterests';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Windows Ink Workspace'; Desc='Pen / Ink Workspace.'
       Values=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\WindowsInkWorkspace';N='AllowWindowsInkWorkspace';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Xbox Game Bar'; Desc='Game Bar startup panel, Nexus button and Game Mode notifications.'
       Values=@( @{P='HKCU:\Software\Microsoft\GameBar';N='UseNexusForGameBarEnabled';On=1;Off=0;Def=1},
                 @{P='HKCU:\Software\Microsoft\GameBar';N='ShowStartupPanel';On=1;Off=0;Def=1},
                 @{P='HKCU:\Software\Microsoft\GameBar';N='ShowGameModeNotifications';On=1;Off=0;Def=1} ) },
    @{ Group='Windows Features'; Name='Mobile Devices Settings (Phone Link page)'; Desc='OFF = lock the page (DisableWindowsConsumerFeatures).'
       Values=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent';N='DisableWindowsConsumerFeatures';On=0;Off=1;Def=0} ) },
    @{ Group='Windows Features'; Name='File Explorer Gallery (navigation pane)'; Desc='Adds the Gallery entry (Gallery_Enabler_v2).'
       Custom=@{
           Get={ Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}' }
           On={  New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}' -Force -Value '{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}' | Out-Null }
           Off={ Remove-Item -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}' -Recurse -Force -ErrorAction SilentlyContinue }
       } }
)

function Get-ToggleState {
    param($T)
    try {
        if ($T.Custom) { return [bool](& $T.Custom.Get) }
        foreach ($v in $T.Values) {
            if ($v.NoCheck) { continue }
            $cur = $null
            try { $cur = (Get-ItemProperty -LiteralPath $v.P -Name $v.N -ErrorAction Stop).($v.N) } catch { $cur = $null }
            if ($null -eq $cur -and $v.ContainsKey('Def')) { $cur = $v.Def }
            if ("$cur" -ne "$($v.On)") { return $false }
        }
        return $true
    } catch { return $false }
}
