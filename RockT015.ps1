# ------------------------------------------------------------------
$Tweaks = @(
    @{ Group='Advanced'; Name='Windows 10 Extended Support (ESU) - Enable'; Rec=$false
       Desc='Windows 10 only. Sets the ConsumerESU flags for your user account (ESUEligibility = 2, ESUEligibilityResult = 1) so Windows treats this PC as eligible for Extended Security Updates. It does not enroll the PC by itself - finish the enrollment from Settings > Windows Update.'
       Apply={
           if ([Environment]::OSVersion.Version.Build -ge 22000) { Log 'This tweak is for Windows 10 only - skipped on Windows 11.' 'WARN' 'Tweaks'; return }
           $k = 'HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows\ConsumerESU'
           Set-Reg $k 'ESUEligibility' 2
           Set-Reg $k 'ESUEligibilityResult' 1
       }
       Undo={
           $k = 'HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows\ConsumerESU'
           Remove-Reg $k 'ESUEligibility'; Remove-Reg $k 'ESUEligibilityResult'
       } },
    @{ Group='Essential'; Name='Activity History - Disable'; Rec=$true
       Desc='Stops Windows from collecting and uploading your activity timeline.'
       Apply={
           $k = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System'
           Set-Reg $k 'EnableActivityFeed' 0; Set-Reg $k 'PublishUserActivities' 0; Set-Reg $k 'UploadUserActivities' 0
       }
       Undo={
           $k = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System'
           Remove-Reg $k 'EnableActivityFeed'; Remove-Reg $k 'PublishUserActivities'; Remove-Reg $k 'UploadUserActivities'
       } },
    @{ Group='Essential'; Name='Advertising ID - Disable'; Rec=$true
       Desc='Stops apps from using your advertising ID for targeted ads.'
       Apply={ Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 0 }
       Undo={  Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 1 } },
    @{ Group='Essential'; Name='ConsumerFeatures - Disable'; Rec=$true
       Desc='Stops Windows from silently installing suggested apps (Candy Crush, etc).'
       Apply={ Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' 'DisableWindowsConsumerFeatures' 1 }
       Undo={  Remove-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent' 'DisableWindowsConsumerFeatures' } },
    @{ Group='Essential'; Name='Delivery Optimization - Disable'; Rec=$true
       Desc='Windows will not upload update files to other PCs on the internet (P2P).'
       Apply={ Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization' 'DODownloadMode' 0 }
       Undo={  Remove-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization' 'DODownloadMode' } },
    @{ Group='Essential'; Name='End Task With Right Click - Enable'; Rec=$true
       Desc='Adds "End task" to the taskbar right-click menu (Windows 11).'
       Apply={ Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' 'TaskbarEndTask' 1 }
       Undo={  Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings' 'TaskbarEndTask' 0 } },
    @{ Group='Essential'; Name='Hibernation - Disable'; Rec=$false
       Desc='Frees disk space equal to hiberfil.sys. Do not use on laptops if you rely on hibernate.'
       Apply={ powercfg /h off }
       Undo={  powercfg /h on } },
    @{ Group='Essential'; Name='Location Tracking - Disable'; Rec=$false
       Desc='Denies location access system-wide.'
       Apply={ Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location' 'Value' 'Deny' 'String' }
       Undo={  Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location' 'Value' 'Allow' 'String' } },
    @{ Group='Essential'; Name='Restore Point - Create'; Rec=$false
       Desc='Creates a System Restore point named "Rock Toolbox" (no undo).'
       Apply={
           Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' 'SystemRestorePointCreationFrequency' 0
           Enable-ComputerRestore -Drive "$env:SystemDrive\"
           Checkpoint-Computer -Description 'Rock Toolbox' -RestorePointType MODIFY_SETTINGS
       }
       Undo=$null },
    @{ Group='Essential'; Name='Start Menu Ads / Tips / Suggestions - Disable'; Rec=$true
       Desc='Turns off suggested content, tips and promoted apps in Start and Settings.'
       Apply={
           $ids = 'SubscribedContent-338387Enabled','SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SubscribedContent-353694Enabled','SubscribedContent-353696Enabled','SubscribedContent-310093Enabled','SystemPaneSuggestionsEnabled','SilentInstalledAppsEnabled','SoftLandingEnabled','OemPreInstalledAppsEnabled','PreInstalledAppsEnabled','ContentDeliveryAllowed'
           foreach ($i in $ids) { Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' $i 0 }
       }
       Undo={
           $ids = 'SubscribedContent-338387Enabled','SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SubscribedContent-353694Enabled','SubscribedContent-353696Enabled','SubscribedContent-310093Enabled','SystemPaneSuggestionsEnabled','SilentInstalledAppsEnabled','SoftLandingEnabled','OemPreInstalledAppsEnabled','PreInstalledAppsEnabled','ContentDeliveryAllowed'
           foreach ($i in $ids) { Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' $i 1 }
       } },
    @{ Group='Essential'; Name='Start Menu Bing Search - Disable'; Rec=$true
       Desc='Start search only looks at your PC, not the web.'
       Apply={ Set-Reg 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions' 1 }
       Undo={  Remove-Reg 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions' } },
    @{ Group='Essential'; Name='Telemetry - Disable'; Rec=$true
       Desc='Turns off diagnostic data collection and the DiagTrack service.'
       Apply={
           Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry' 0
           Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' 'AllowTelemetry' 0
           Stop-Service DiagTrack -Force -ErrorAction SilentlyContinue
           Set-Service DiagTrack -StartupType Disabled -ErrorAction SilentlyContinue
           Set-Service dmwappushservice -StartupType Disabled -ErrorAction SilentlyContinue
       }
       Undo={
           Remove-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry'
           Remove-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' 'AllowTelemetry'
           Set-Service DiagTrack -StartupType Automatic -ErrorAction SilentlyContinue
           Start-Service DiagTrack -ErrorAction SilentlyContinue
           Set-Service dmwappushservice -StartupType Manual -ErrorAction SilentlyContinue
       } },
    @{ Group='Essential'; Name='Temporary Files - Remove'; Rec=$true
       Desc='Deletes the user Temp and Windows Temp folders (no undo).'
       Apply={
           Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
           Remove-Item "$env:windir\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
       }
       Undo=$null },
    @{ Group='Advanced'; Name='Copilot and Recall (AI) - Disable'; Rec=$false
       Desc='Policy-based switch-off for Windows Copilot and AI data analysis.'
       Apply={
           Set-Reg 'HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot' 'TurnOffWindowsCopilot' 1
           Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI' 'DisableAIDataAnalysis' 1
       }
       Undo={
           Remove-Reg 'HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot' 'TurnOffWindowsCopilot'
           Remove-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsAI' 'DisableAIDataAnalysis'
       } },
    @{ Group='Advanced'; Name='Fast Startup - Disable'; Rec=$false
       Desc='Full shutdown every time (fixes weird driver/uptime issues, slightly slower boot).'
       Apply={ Set-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 0 }
       Undo={  Set-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 1 } },
    @{ Group='Advanced'; Name='Game DVR / Game Bar Capture - Disable'; Rec=$false
       Desc='Turns off background game recording (small FPS gain on some PCs).'
       Apply={
           Set-Reg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled' 0
           Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled' 0
       }
       Undo={
           Set-Reg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled' 1
           Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled' 1
       } },
    @{ Group='Advanced'; Name='IPv6 - Disable'; Rec=$false
       Desc='Unbinds IPv6 from all network adapters.'
       Apply={ foreach ($a in @(Get-NetAdapter)) { Disable-NetAdapterBinding -Name $a.Name -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue } }
       Undo={  foreach ($a in @(Get-NetAdapter)) { Enable-NetAdapterBinding  -Name $a.Name -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue } } },
    @{ Group='Advanced'; Name='Microsoft OneDrive - Remove'; Rec=$false
       Desc='Uninstalls OneDrive. Undo reinstalls it with winget.'
       Apply={
           Stop-Process -Name OneDrive -Force -ErrorAction SilentlyContinue
           $exe = "$env:SystemRoot\SysWOW64\OneDriveSetup.exe"
           if (-not (Test-Path $exe)) { $exe = "$env:SystemRoot\System32\OneDriveSetup.exe" }
           [void](Invoke-External $exe '/uninstall')
       }
       Undo={
           $wg = Get-Winget
           if ($wg) { [void](Invoke-External $wg 'install --id Microsoft.OneDrive -e --silent --accept-source-agreements --accept-package-agreements') }
       } },
    @{ Group='Advanced'; Name='Reserved Storage - Disable'; Rec=$false
       Desc='Gives back the ~7 GB Windows reserves for updates.'
       Apply={ [void](Invoke-External 'dism.exe' '/Online /Set-ReservedStorageState /State:Disabled') }
       Undo={  [void](Invoke-External 'dism.exe' '/Online /Set-ReservedStorageState /State:Enabled') } },
    @{ Group='Advanced'; Name='Startup Delay - Remove'; Rec=$false
       Desc='Desktop apps in Startup load immediately instead of after a delay.'
       Apply={ Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize' 'StartupDelayInMSec' 0 }
       Undo={  Remove-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize' 'StartupDelayInMSec' } },
    @{ Group='Advanced'; Name='Storage Sense - Disable'; Rec=$false
       Desc='Stops automatic cleanup of temp files and recycle bin.'
       Apply={ Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' '01' 0 }
       Undo={  Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' '01' 1 } },
    @{ Group='Advanced'; Name='Teredo - Disable'; Rec=$false
       Desc='Disables the Teredo IPv6 tunneling adapter.'
       Apply={ [void](Invoke-External 'netsh.exe' 'interface teredo set state disabled') }
       Undo={  [void](Invoke-External 'netsh.exe' 'interface teredo set state default') } },
    @{ Group='Advanced'; Name='Visual Effects - Set to Best Performance'; Rec=$false
       Desc='Switches Windows visual effects to "Adjust for best performance". Sign out to apply.'
       Apply={ Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' 'VisualFXSetting' 2 }
       Undo={  Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' 'VisualFXSetting' 0 } }
)

# ------------------------------------------------------------------
# DATA: more tweaks  (Ops = registry list: V = apply value, U = undo value, Rm = remove value on undo)
