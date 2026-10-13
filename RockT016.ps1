# ------------------------------------------------------------------
$HKCU_Adv = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$HKLM_WS  = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search'
$HKCU_CC  = 'HKCU:\Software\Policies\Microsoft\Windows\CloudContent'
$HKLM_SP  = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile'
$HKCU_DSK = 'HKCU:\Control Panel\Desktop'
$EdgePol  = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'

$Tweaks += @(
    # ---------------- Privacy / telemetry ----------------
    @{ Group='Essential'; Name='Cortana - Disable'; Rec=$true; Desc='Turns Cortana off by policy.'
       Ops=@( @{P=$HKLM_WS;N='AllowCortana';V=0;Rm=$true} ) },
    @{ Group='Essential'; Name='Web Search in Windows Search - Disable'; Rec=$true; Desc='Windows Search stops sending queries to Bing.'
       Ops=@( @{P=$HKLM_WS;N='DisableWebSearch';V=1;Rm=$true}, @{P=$HKLM_WS;N='ConnectedSearchUseWeb';V=0;Rm=$true} ) },
    @{ Group='Essential'; Name='Feedback Notifications - Disable'; Rec=$true; Desc='No more "How likely are you to recommend Windows" popups.'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection';N='DoNotShowFeedbackNotifications';V=1;Rm=$true},
              @{P='HKCU:\Software\Microsoft\Siuf\Rules';N='NumberOfSIUFInPeriod';V=0;Rm=$true} ) },
    @{ Group='Essential'; Name='Tailored Experiences - Disable'; Rec=$true; Desc='Stops Microsoft from using diagnostic data to personalize tips and ads.'
       Ops=@( @{P=$HKCU_CC;N='DisableTailoredExperiencesWithDiagnosticData';V=1;Rm=$true} ) },
    @{ Group='Essential'; Name='Windows Spotlight - Disable'; Rec=$true; Desc='Disables Spotlight suggestions on lock screen, Settings and Action Center.'
       Ops=@( @{P=$HKCU_CC;N='DisableWindowsSpotlightFeatures';V=1;Rm=$true}, @{P=$HKCU_CC;N='DisableWindowsSpotlightOnActionCenter';V=1;Rm=$true},
              @{P=$HKCU_CC;N='DisableWindowsSpotlightOnSettings';V=1;Rm=$true}, @{P=$HKCU_CC;N='DisableWindowsSpotlightWindowsWelcomeExperience';V=1;Rm=$true} ) },
    @{ Group='Essential'; Name='Customer Experience Program (CEIP) - Disable'; Rec=$true; Desc='Turns off the Customer Experience Improvement Program.'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\SQMClient\Windows';N='CEIPEnable';V=0;Rm=$true} ) },
    @{ Group='Essential'; Name='Application Telemetry / Inventory - Disable'; Rec=$true; Desc='Disables the inventory collector, Application Impact Telemetry and Steps Recorder.'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat';N='DisableInventory';V=1;Rm=$true},
              @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat';N='AITEnable';V=0;Rm=$true},
              @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppCompat';N='DisableUAR';V=1;Rm=$true} ) },
    @{ Group='Essential'; Name='Telemetry Scheduled Tasks - Disable'; Rec=$true; Desc='Disables the compatibility appraiser, CEIP and disk-diagnostic data collection tasks.'
       Apply={
           foreach ($t in '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser', '\Microsoft\Windows\Application Experience\ProgramDataUpdater',
                          '\Microsoft\Windows\Customer Experience Improvement Program\Consolidator', '\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip',
                          '\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector') {
               Disable-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue | Out-Null
           }
       }
       Undo={
           foreach ($t in '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser', '\Microsoft\Windows\Application Experience\ProgramDataUpdater',
                          '\Microsoft\Windows\Customer Experience Improvement Program\Consolidator', '\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip',
                          '\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector') {
               Enable-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue | Out-Null
           }
       } },
    @{ Group='Essential'; Name='Clipboard History and Cloud Sync - Disable'; Rec=$false; Desc='Disables Win+V history and cross-device clipboard sync by policy.'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\System';N='AllowClipboardHistory';V=0;Rm=$true},
              @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\System';N='AllowCrossDeviceClipboard';V=0;Rm=$true} ) },
    @{ Group='Essential'; Name='Typing and Inking Personalization - Disable'; Rec=$true; Desc='Stops Windows from collecting what you type and ink to personalize suggestions.'
       Ops=@( @{P='HKCU:\Software\Microsoft\InputPersonalization';N='RestrictImplicitInkCollection';V=1;U=0},
              @{P='HKCU:\Software\Microsoft\InputPersonalization';N='RestrictImplicitTextCollection';V=1;U=0},
              @{P='HKCU:\Software\Microsoft\InputPersonalization\TrainedDataStore';N='HarvestContacts';V=0;U=1},
              @{P='HKCU:\Software\Microsoft\Personalization\Settings';N='AcceptedPrivacyPolicy';V=0;U=1} ) },
    @{ Group='Essential'; Name='Handwriting Data Sharing - Disable'; Rec=$false; Desc='Blocks handwriting data and error report sharing.'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\TabletPC';N='PreventHandwritingDataSharing';V=1;Rm=$true},
              @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\HandwritingErrorReports';N='PreventHandwritingErrorReports';V=1;Rm=$true} ) },
    @{ Group='Essential'; Name='Online Speech Recognition - Disable'; Rec=$false; Desc='Declines the online speech recognition privacy policy.'
       Ops=@( @{P='HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy';N='HasAccepted';V=0;U=1} ) },
    @{ Group='Essential'; Name='Remote Assistance - Disable'; Rec=$false; Desc='Blocks incoming Remote Assistance requests.'
       Ops=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance';N='fAllowToGetHelp';V=0;U=1} ) },
    @{ Group='Essential'; Name='Notification Toasts - Disable'; Rec=$false; Desc='Turns off all toast notifications.'
       Ops=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications';N='ToastEnabled';V=0;U=1} ) },
    @{ Group='Essential'; Name='Start Menu Recommended / Recent Items - Disable'; Rec=$true; Desc='Hides recent files and the "Recommended" section in Start.'
       Ops=@( @{P=$HKCU_Adv;N='Start_TrackDocs';V=0;U=1}, @{P=$HKCU_Adv;N='Start_TrackProgs';V=0;U=1}, @{P=$HKCU_Adv;N='Start_IrisRecommendations';V=0;U=1} ) },
    @{ Group='Essential'; Name='Quick Access Recent / Frequent - Disable'; Rec=$false; Desc='File Explorer stops listing recent and frequent files/folders.'
       Ops=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer';N='ShowRecent';V=0;U=1},
              @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer';N='ShowFrequent';V=0;U=1},
              @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer';N='ShowCloudFilesInQuickAccess';V=0;U=1} ) },
    @{ Group='Essential'; Name='Explorer Sync Provider Ads - Disable'; Rec=$true; Desc='Removes OneDrive / Microsoft 365 promos from File Explorer.'
       Ops=@( @{P=$HKCU_Adv;N='ShowSyncProviderNotifications';V=0;U=1} ) },
    @{ Group='Essential'; Name='Search Highlights - Disable'; Rec=$true; Desc='Removes the daily doodles / highlights from the search box.'
       Ops=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings';N='IsDynamicSearchBoxEnabled';V=0;U=1} ) },
    @{ Group='Essential'; Name='"Finish Setting Up Your Device" Nags - Disable'; Rec=$true; Desc='Stops the "Get even more out of Windows" full-screen prompts.'
       Ops=@( @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement';N='ScoobeSystemSettingEnabled';V=0;U=1} ) },
    @{ Group='Essential'; Name='Menu Show Delay - Remove'; Rec=$true; Desc='Menus open instantly.'
       Ops=@( @{P=$HKCU_DSK;N='MenuShowDelay';V='0';U='400';T='String'} ) },
    @{ Group='Essential'; Name='Recycle Bin - Empty'; Rec=$false; Desc='Empties the Recycle Bin on all drives (no undo).'
       Apply={ Clear-RecycleBin -Force -ErrorAction SilentlyContinue }
       Undo=$null },
    @{ Group='Essential'; Name='Thumbnail Cache - Clear'; Rec=$false; Desc='Deletes Explorer thumbnail caches (no undo, they rebuild automatically).'
       Apply={ Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db" -Force -ErrorAction SilentlyContinue }
       Undo=$null },
    @{ Group='Essential'; Name='Windows Update Cache - Clean'; Rec=$false; Desc='Clears downloaded update files (SoftwareDistribution\Download). No undo.'
       Apply={
           Stop-Service wuauserv -Force -ErrorAction SilentlyContinue
           Remove-Item "$env:windir\SoftwareDistribution\Download\*" -Recurse -Force -ErrorAction SilentlyContinue
           Start-Service wuauserv -ErrorAction SilentlyContinue
       }
       Undo=$null },

    # ---------------- Performance ----------------
    @{ Group='Advanced'; Name='Network Throttling and System Responsiveness - Tweak'; Rec=$false; Desc='Disables multimedia network throttling and reserves less CPU for background tasks.'
       Ops=@( @{P=$HKLM_SP;N='NetworkThrottlingIndex';V=-1;U=10}, @{P=$HKLM_SP;N='SystemResponsiveness';V=0;U=20} ) },
    @{ Group='Advanced'; Name='Games Scheduling Priority - Boost'; Rec=$false; Desc='Raises CPU/GPU scheduling priority for games.'
       Ops=@( @{P="$HKLM_SP\Tasks\Games";N='GPU Priority';V=8;U=8}, @{P="$HKLM_SP\Tasks\Games";N='Priority';V=6;U=2},
              @{P="$HKLM_SP\Tasks\Games";N='Scheduling Category';V='High';U='Medium';T='String'}, @{P="$HKLM_SP\Tasks\Games";N='SFIO Priority';V='High';U='Normal';T='String'} ) },
    @{ Group='Advanced'; Name='Foreground Apps Priority - Boost'; Rec=$false; Desc='Gives the app you are using more CPU time slices (Win32PrioritySeparation = 38).'
       Ops=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl';N='Win32PrioritySeparation';V=38;U=2} ) },
    @{ Group='Advanced'; Name='Hardware-Accelerated GPU Scheduling - Enable'; Rec=$false; Desc='Needs a supported GPU and recent drivers. Restart required.'
       Ops=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers';N='HwSchMode';V=2;U=1} ) },
    @{ Group='Advanced'; Name='Faster Shutdown / Kill Hung Apps'; Rec=$false; Desc='Shorter timeouts for hung apps and services at shutdown.'
       Ops=@( @{P=$HKCU_DSK;N='AutoEndTasks';V='1';U='0';T='String'}, @{P=$HKCU_DSK;N='HungAppTimeout';V='1000';U='5000';T='String'},
              @{P=$HKCU_DSK;N='WaitToKillAppTimeout';V='2000';U='20000';T='String'},
              @{P='HKLM:\SYSTEM\CurrentControlSet\Control';N='WaitToKillServiceTimeout';V='2000';U='5000';T='String'} ) },
    @{ Group='Advanced'; Name='Power Throttling - Disable'; Rec=$false; Desc='Stops Windows from throttling background apps (desktop PCs).'
       Ops=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling';N='PowerThrottlingOff';V=1;Rm=$true} ) },
    @{ Group='Advanced'; Name='NTFS Last Access Time - Disable'; Rec=$false; Desc='Reduces disk writes (fsutil disablelastaccess).'
       Apply={ [void](Invoke-External 'fsutil.exe' 'behavior set disablelastaccess 1') }
       Undo={  [void](Invoke-External 'fsutil.exe' 'behavior set disablelastaccess 2') } },
    @{ Group='Advanced'; Name='SysMain (Superfetch) - Disable'; Rec=$false; Desc='Good for SSD systems. Stops and disables the SysMain service.'
       Apply={ Stop-Service SysMain -Force -ErrorAction SilentlyContinue; Set-Service SysMain -StartupType Disabled }
       Undo={  Set-Service SysMain -StartupType Automatic; Start-Service SysMain -ErrorAction SilentlyContinue } },
    @{ Group='Advanced'; Name='Windows Search Indexing - Disable'; Rec=$false; Desc='Stops the indexer (start-menu/file search becomes slower).'
       Apply={ Stop-Service WSearch -Force -ErrorAction SilentlyContinue; Set-Service WSearch -StartupType Disabled }
       Undo={  Set-Service WSearch -StartupType Automatic; Start-Service WSearch -ErrorAction SilentlyContinue } },
    @{ Group='Advanced'; Name='Print Spooler - Disable'; Rec=$false; Desc='Only if you do not use printers (also closes the PrintNightmare attack surface).'
       Apply={ Stop-Service Spooler -Force -ErrorAction SilentlyContinue; Set-Service Spooler -StartupType Disabled }
       Undo={  Set-Service Spooler -StartupType Automatic; Start-Service Spooler -ErrorAction SilentlyContinue } },
    @{ Group='Advanced'; Name='Xbox Services - Disable'; Rec=$false; Desc='Disables Xbox Live auth, save, networking and accessory services.'
       Apply={ foreach ($s in 'XblAuthManager', 'XblGameSave', 'XboxNetApiSvc', 'XboxGipSvc') { Stop-Service $s -Force -ErrorAction SilentlyContinue; Set-Service $s -StartupType Disabled -ErrorAction SilentlyContinue } }
       Undo={  foreach ($s in 'XblAuthManager', 'XblGameSave', 'XboxNetApiSvc', 'XboxGipSvc') { Set-Service $s -StartupType Manual -ErrorAction SilentlyContinue } } },
    @{ Group='Advanced'; Name='Unneeded Services - Disable (Fax, Maps, Retail Demo)'; Rec=$false; Desc='Disables Fax, Downloaded Maps Manager and Retail Demo services.'
       Apply={ foreach ($s in 'Fax', 'MapsBroker', 'RetailDemo') { Stop-Service $s -Force -ErrorAction SilentlyContinue; Set-Service $s -StartupType Disabled -ErrorAction SilentlyContinue } }
       Undo={
           Set-Service Fax -StartupType Manual -ErrorAction SilentlyContinue
           Set-Service MapsBroker -StartupType Automatic -ErrorAction SilentlyContinue
           Set-Service RetailDemo -StartupType Manual -ErrorAction SilentlyContinue
       } },
    @{ Group='Advanced'; Name='Memory Compression - Disable'; Rec=$false; Desc='Only for PCs with plenty of RAM (16 GB+).'
       Apply={ Disable-MMAgent -MemoryCompression }
       Undo={  Enable-MMAgent -MemoryCompression } },
    @{ Group='Advanced'; Name='USB Selective Suspend - Disable (plugged in)'; Rec=$false; Desc='Prevents USB devices from sleeping while on AC power.'
       Apply={
           powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
           powercfg /setactive SCHEME_CURRENT
       }
       Undo={
           powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1
           powercfg /setactive SCHEME_CURRENT
       } },
    @{ Group='Advanced'; Name='Never Sleep When Plugged In'; Rec=$false; Desc='Sets "sleep after" to Never on AC power.'
       Apply={ powercfg /change standby-timeout-ac 0 }
       Undo={  powercfg /change standby-timeout-ac 30 } },
    @{ Group='Advanced'; Name='Component Store (WinSxS) - Cleanup'; Rec=$false; Desc='Runs DISM StartComponentCleanup. Can take several minutes (no undo).'
       Apply={ [void](Invoke-External 'dism.exe' '/Online /Cleanup-Image /StartComponentCleanup') }
       Undo=$null },
    @{ Group='Advanced'; Name='Aero Shake - Disable'; Rec=$false; Desc='Shaking a window no longer minimizes the others.'
       Ops=@( @{P='HKCU:\Software\Policies\Microsoft\Windows\Explorer';N='NoWindowMinimizingShortcuts';V=1;Rm=$true} ) },
    @{ Group='Advanced'; Name='Windows Insider / Preview Builds - Disable'; Rec=$false; Desc='Blocks Insider preview builds by policy.'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows\PreviewBuilds';N='AllowBuildPreview';V=0;Rm=$true} ) },
    @{ Group='Advanced'; Name='Date and Time - Set Time to UTC (dual boot)'; Rec=$false; Desc='Makes Windows treat the hardware clock as UTC (fixes clock drift with Linux).'
       Ops=@( @{P='HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation';N='RealTimeIsUniversal';V=1;Rm=$true} ) },
    @{ Group='Advanced'; Name='Microsoft Edge - Debloat (policies)'; Rec=$false; Desc='Turns off Edge first-run, startup boost, background mode, shopping assistant, sidebar, rewards and follow.'
       Ops=@( @{P=$EdgePol;N='HideFirstRunExperience';V=1;Rm=$true}, @{P=$EdgePol;N='StartupBoostEnabled';V=0;Rm=$true}, @{P=$EdgePol;N='BackgroundModeEnabled';V=0;Rm=$true},
              @{P=$EdgePol;N='EdgeShoppingAssistantEnabled';V=0;Rm=$true}, @{P=$EdgePol;N='PersonalizationReportingEnabled';V=0;Rm=$true}, @{P=$EdgePol;N='ShowMicrosoftRewards';V=0;Rm=$true},
              @{P=$EdgePol;N='EdgeFollowEnabled';V=0;Rm=$true}, @{P=$EdgePol;N='HubsSidebarEnabled';V=0;Rm=$true}, @{P=$EdgePol;N='DiscoverPageContextEnabled';V=0;Rm=$true} ) },

    # ---------------- Security ----------------
    @{ Group='Advanced'; Name='SMBv1 - Disable'; Rec=$true; Desc='Turns off the old, insecure SMB 1.0 protocol.'
       Apply={ [void](Invoke-External 'dism.exe' '/Online /Disable-Feature /FeatureName:SMB1Protocol /NoRestart /Quiet') }
       Undo={  [void](Invoke-External 'dism.exe' '/Online /Enable-Feature /FeatureName:SMB1Protocol /NoRestart /Quiet') } },
    @{ Group='Advanced'; Name='LLMNR - Disable'; Rec=$false; Desc='Disables Link-Local Multicast Name Resolution (spoofing risk on public networks).'
       Ops=@( @{P='HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient';N='EnableMulticast';V=0;Rm=$true} ) },
    @{ Group='Advanced'; Name='AutoRun / AutoPlay - Disable'; Rec=$true; Desc='Stops drives and USB sticks from auto-running or auto-playing.'
       Ops=@( @{P='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer';N='NoDriveTypeAutoRun';V=255;Rm=$true},
              @{P='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers';N='DisableAutoplay';V=1;U=0} ) },
    @{ Group='Advanced'; Name='Defender Cloud Sample Submission - Disable'; Rec=$false; Desc='Windows Defender stops uploading samples (fails if Tamper Protection is on).'
       Apply={ Set-MpPreference -SubmitSamplesConsent 2 }
       Undo={  Set-MpPreference -SubmitSamplesConsent 1 } }
)

# ------------------------------------------------------------------
# DATA: debloat list
