<<<<<<< HEAD
﻿# ==============================================================================
# MODULE: src\data\Config.ps1
# ==============================================================================
# Module: data/Config - DATA: Config tab
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# DATA: Config tab
# ------------------------------------------------------------------
$Features = @(
    @{ Name='.NET Framework 3.5 (2, 3) - Enable';              Features=@('NetFx3') },
    @{ Name='Hyper-V - Enable';                                Features=@('Microsoft-Hyper-V-All') },
    @{ Name='Legacy F8 Boot Recovery - Enable';                Cmd=@{ File='bcdedit.exe'; Arg='/set {current} bootmenupolicy legacy' } },
    @{ Name='Legacy F8 Boot Recovery - Disable';               Cmd=@{ File='bcdedit.exe'; Arg='/set {current} bootmenupolicy standard' } },
    @{ Name='Legacy Media Components (WMP, DirectPlay) - Enable'; Features=@('MediaPlayback', 'WindowsMediaPlayer', 'LegacyComponents', 'DirectPlay') },
    @{ Name='Network File System (NFS) - Enable';              Features=@('ServicesForNFS-ClientOnly', 'ClientForNFS-Infrastructure', 'NFS-Administration') },
    @{ Name='Windows Sandbox - Enable';                        Features=@('Containers-DisposableClientVM') },
    @{ Name='Windows Subsystem for Linux (WSL) - Enable';      Features=@('Microsoft-Windows-Subsystem-Linux', 'VirtualMachinePlatform') }
)

# SpotX launcher script (written to disk and run in its OWN PowerShell window).
# SpotX's run.ps1 asks questions (Read-Host / ReadKey), clears the screen and calls Exit: run inside the toolbox it froze or closed the app.
# The official link is tried first; the mirrors are only fallbacks.
$script:SpotXLauncher = @'
$Host.UI.RawUI.WindowTitle = 'SpotX'
$ProgressPreference = 'SilentlyContinue'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }
$sources = @(
    @{ Url = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/refs/heads/main/run.ps1';         Extra = @() },
    @{ Url = 'https://raw.githubusercontent.com/SpotX-Official/spotx-official.github.io/main/run.ps1'; Extra = @() },
    @{ Url = 'https://spotx-official.github.io/run.ps1';                                              Extra = @('-m') }
)
$tmp = Join-Path $env:TEMP 'SpotX_run.ps1'
$done = $false
foreach ($s in $sources) {
    try {
        Write-Host ('Downloading SpotX: ' + $s.Url) -ForegroundColor Cyan
        $txt = [string](Invoke-WebRequest -UseBasicParsing -Uri $s.Url -TimeoutSec 30).Content
        if ([string]::IsNullOrWhiteSpace($txt) -or $txt.Length -lt 2000) { throw 'the download is empty or not the SpotX script' }
        [IO.File]::WriteAllText($tmp, $txt, (New-Object System.Text.UTF8Encoding($true)))
        $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $tmp, '-new_theme') + $s.Extra
        & powershell.exe @a
        $done = $true
        break
    } catch { Write-Host ('  failed: ' + $_.Exception.Message) -ForegroundColor Yellow }
}
if (-not $done) { Write-Host 'Could not get SpotX from any source. Check the internet connection / antivirus / DNS and try again.' -ForegroundColor Red }
Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
Write-Host ''
Read-Host 'SpotX window finished - press Enter to close'
'@

$Fixes = @(
    @{ Name='AutoLogon - Run'
       Action={ Start-Process 'netplwiz.exe'; Log 'Opened netplwiz (untick "Users must enter a user name and password").' 'INFO' 'Fix' } },
    @{ Name='Network - Reset'
       Action={
           Log 'Resetting network stack...' 'INFO' 'Fix'
           [void](Invoke-External 'netsh.exe' 'winsock reset')
           [void](Invoke-External 'netsh.exe' 'int ip reset')
           [void](Invoke-External 'ipconfig.exe' '/flushdns')
           Log 'Network reset done. Restart Windows to finish.' 'INFO' 'Fix'
       } },
    @{ Name='NTP Server - Enable (sync time)'
       Action={
           Set-Service w32time -StartupType Automatic -ErrorAction SilentlyContinue
           Start-Service w32time -ErrorAction SilentlyContinue
           [void](Invoke-External 'w32tm.exe' '/config /syncfromflags:manual /manualpeerlist:"time.windows.com pool.ntp.org" /update')
           [void](Invoke-External 'w32tm.exe' '/resync /force')
           Log 'Time service configured and resynced.' 'INFO' 'Fix'
       } },
    @{ Name='System Corruption Scan - Run (DISM + SFC)'
       Action={
           Log 'Running DISM RestoreHealth (this can take a while)...' 'INFO' 'Fix'
           [void](Invoke-External 'dism.exe' '/Online /Cleanup-Image /RestoreHealth')
           Log 'Running sfc /scannow...' 'INFO' 'Fix'
           [void](Invoke-External 'sfc.exe' '/scannow')
           Log 'Scan finished.' 'INFO' 'Fix'
       } },
    @{ Name='Windows Update - Reset'
       Action={ Reset-WindowsUpdate } },
    @{ Name='WinGet - Reinstall'
       Action={
           Log 'Re-registering App Installer (winget)...' 'INFO' 'Fix'
           Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
           Log 'Done. Open a new terminal and run: winget --version' 'INFO' 'Fix'
       } },
    @{ Name='SPOTX (Spotify adblock + theme)'
       Action={
           Log 'Opening SpotX in its own PowerShell window...' 'INFO' 'SpotX'
           $f = Join-Path $env:ProgramData 'RockToolbox_SpotX.ps1'
           Set-Content -LiteralPath $f -Value $script:SpotXLauncher -Encoding UTF8
           $psArgs = '-NoProfile -ExecutionPolicy Bypass -File "' + $f + '"'
           $started = $false
           # Spotify and SpotX do not like an elevated window: open it with a normal (de-elevated) token when possible
           $isAdmin = $false
           try { $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) } catch { }
           if ($isAdmin) {
               try {
                   $rp = Start-Process -FilePath 'runas.exe' -ArgumentList ('/trustlevel:0x20000 "powershell.exe ' + $psArgs.Replace('"', '\"') + '"') -WindowStyle Hidden -PassThru
                   if ($rp.WaitForExit(6000)) { if ($rp.ExitCode -eq 0) { $started = $true } } else { $started = $true }
               } catch { }
           }
           if (-not $started) { Start-Process -FilePath 'powershell.exe' -ArgumentList $psArgs }
           Log 'SpotX window opened - follow the steps in it. Rock Toolbox stays usable meanwhile.' 'INFO' 'SpotX'
       } }
)

$LegacyPanels = @(
    @{ Name='Computer Management';       File='compmgmt.msc';  Arg='' },
    @{ Name='Control Panel';             File='control.exe';   Arg='' },
    @{ Name='Mouse Properties';          File='control.exe';   Arg='main.cpl' },
    @{ Name='Network Connections';       File='control.exe';   Arg='ncpa.cpl' },
    @{ Name='Power Panel';               File='control.exe';   Arg='powercfg.cpl' },
    @{ Name='Printer Panel';             File='control.exe';   Arg='printers' },
    @{ Name='Programs and Features';     File='control.exe';   Arg='appwiz.cpl' },
    @{ Name='Region';                    File='control.exe';   Arg='intl.cpl' },
    @{ Name='Security and Maintenance';  File='control.exe';   Arg='wscui.cpl' },
    @{ Name='Sound Settings';            File='control.exe';   Arg='mmsys.cpl' },
    @{ Name='System Properties';         File='control.exe';   Arg='sysdm.cpl' },
    @{ Name='Time and Date';             File='control.exe';   Arg='timedate.cpl' },
    @{ Name='Windows Defender Firewall'; File='control.exe';   Arg='firewall.cpl' },
    @{ Name='Windows Restore';           File='rstrui.exe';    Arg='' }
)


=======
﻿# ==============================================================================
# MODULE: src\data\Config.ps1
# ==============================================================================
# Module: data/Config - DATA: Config tab
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# DATA: Config tab
# ------------------------------------------------------------------
$Features = @(
    @{ Name='.NET Framework 3.5 (2, 3) - Enable';              Features=@('NetFx3') },
    @{ Name='Hyper-V - Enable';                                Features=@('Microsoft-Hyper-V-All') },
    @{ Name='Legacy F8 Boot Recovery - Enable';                Cmd=@{ File='bcdedit.exe'; Arg='/set {current} bootmenupolicy legacy' } },
    @{ Name='Legacy F8 Boot Recovery - Disable';               Cmd=@{ File='bcdedit.exe'; Arg='/set {current} bootmenupolicy standard' } },
    @{ Name='Legacy Media Components (WMP, DirectPlay) - Enable'; Features=@('MediaPlayback', 'WindowsMediaPlayer', 'LegacyComponents', 'DirectPlay') },
    @{ Name='Network File System (NFS) - Enable';              Features=@('ServicesForNFS-ClientOnly', 'ClientForNFS-Infrastructure', 'NFS-Administration') },
    @{ Name='Windows Sandbox - Enable';                        Features=@('Containers-DisposableClientVM') },
    @{ Name='Windows Subsystem for Linux (WSL) - Enable';      Features=@('Microsoft-Windows-Subsystem-Linux', 'VirtualMachinePlatform') }
)

# SpotX launcher script (written to disk and run in its OWN PowerShell window).
# SpotX's run.ps1 asks questions (Read-Host / ReadKey), clears the screen and calls Exit: run inside the toolbox it froze or closed the app.
# The official link is tried first; the mirrors are only fallbacks.
$script:SpotXLauncher = @'
$Host.UI.RawUI.WindowTitle = 'SpotX'
$ProgressPreference = 'SilentlyContinue'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }
$sources = @(
    @{ Url = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/refs/heads/main/run.ps1';         Extra = @() },
    @{ Url = 'https://raw.githubusercontent.com/SpotX-Official/spotx-official.github.io/main/run.ps1'; Extra = @() },
    @{ Url = 'https://spotx-official.github.io/run.ps1';                                              Extra = @('-m') }
)
$tmp = Join-Path $env:TEMP 'SpotX_run.ps1'
$done = $false
foreach ($s in $sources) {
    try {
        Write-Host ('Downloading SpotX: ' + $s.Url) -ForegroundColor Cyan
        $txt = [string](Invoke-WebRequest -UseBasicParsing -Uri $s.Url -TimeoutSec 30).Content
        if ([string]::IsNullOrWhiteSpace($txt) -or $txt.Length -lt 2000) { throw 'the download is empty or not the SpotX script' }
        [IO.File]::WriteAllText($tmp, $txt, (New-Object System.Text.UTF8Encoding($true)))
        $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $tmp, '-new_theme') + $s.Extra
        & powershell.exe @a
        $done = $true
        break
    } catch { Write-Host ('  failed: ' + $_.Exception.Message) -ForegroundColor Yellow }
}
if (-not $done) { Write-Host 'Could not get SpotX from any source. Check the internet connection / antivirus / DNS and try again.' -ForegroundColor Red }
Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
Write-Host ''
Read-Host 'SpotX window finished - press Enter to close'
'@

$Fixes = @(
    @{ Name='AutoLogon - Run'
       Action={ Start-Process 'netplwiz.exe'; Log 'Opened netplwiz (untick "Users must enter a user name and password").' 'INFO' 'Fix' } },
    @{ Name='Network - Reset'
       Action={
           Log 'Resetting network stack...' 'INFO' 'Fix'
           [void](Invoke-External 'netsh.exe' 'winsock reset')
           [void](Invoke-External 'netsh.exe' 'int ip reset')
           [void](Invoke-External 'ipconfig.exe' '/flushdns')
           Log 'Network reset done. Restart Windows to finish.' 'INFO' 'Fix'
       } },
    @{ Name='NTP Server - Enable (sync time)'
       Action={
           Set-Service w32time -StartupType Automatic -ErrorAction SilentlyContinue
           Start-Service w32time -ErrorAction SilentlyContinue
           [void](Invoke-External 'w32tm.exe' '/config /syncfromflags:manual /manualpeerlist:"time.windows.com pool.ntp.org" /update')
           [void](Invoke-External 'w32tm.exe' '/resync /force')
           Log 'Time service configured and resynced.' 'INFO' 'Fix'
       } },
    @{ Name='System Corruption Scan - Run (DISM + SFC)'
       Action={
           Log 'Running DISM RestoreHealth (this can take a while)...' 'INFO' 'Fix'
           [void](Invoke-External 'dism.exe' '/Online /Cleanup-Image /RestoreHealth')
           Log 'Running sfc /scannow...' 'INFO' 'Fix'
           [void](Invoke-External 'sfc.exe' '/scannow')
           Log 'Scan finished.' 'INFO' 'Fix'
       } },
    @{ Name='Windows Update - Reset'
       Action={ Reset-WindowsUpdate } },
    @{ Name='WinGet - Reinstall'
       Action={
           Log 'Re-registering App Installer (winget)...' 'INFO' 'Fix'
           Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
           Log 'Done. Open a new terminal and run: winget --version' 'INFO' 'Fix'
       } },
    @{ Name='SPOTX (Spotify adblock + theme)'
       Action={
           Log 'Opening SpotX in its own PowerShell window...' 'INFO' 'SpotX'
           $f = Join-Path $env:ProgramData 'RockToolbox_SpotX.ps1'
           Set-Content -LiteralPath $f -Value $script:SpotXLauncher -Encoding UTF8
           $psArgs = '-NoProfile -ExecutionPolicy Bypass -File "' + $f + '"'
           $started = $false
           # Spotify and SpotX do not like an elevated window: open it with a normal (de-elevated) token when possible
           $isAdmin = $false
           try { $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) } catch { }
           if ($isAdmin) {
               try {
                   $rp = Start-Process -FilePath 'runas.exe' -ArgumentList ('/trustlevel:0x20000 "powershell.exe ' + $psArgs.Replace('"', '\"') + '"') -WindowStyle Hidden -PassThru
                   if ($rp.WaitForExit(6000)) { if ($rp.ExitCode -eq 0) { $started = $true } } else { $started = $true }
               } catch { }
           }
           if (-not $started) { Start-Process -FilePath 'powershell.exe' -ArgumentList $psArgs }
           Log 'SpotX window opened - follow the steps in it. Rock Toolbox stays usable meanwhile.' 'INFO' 'SpotX'
       } }
)

$LegacyPanels = @(
    @{ Name='Computer Management';       File='compmgmt.msc';  Arg='' },
    @{ Name='Control Panel';             File='control.exe';   Arg='' },
    @{ Name='Mouse Properties';          File='control.exe';   Arg='main.cpl' },
    @{ Name='Network Connections';       File='control.exe';   Arg='ncpa.cpl' },
    @{ Name='Power Panel';               File='control.exe';   Arg='powercfg.cpl' },
    @{ Name='Printer Panel';             File='control.exe';   Arg='printers' },
    @{ Name='Programs and Features';     File='control.exe';   Arg='appwiz.cpl' },
    @{ Name='Region';                    File='control.exe';   Arg='intl.cpl' },
    @{ Name='Security and Maintenance';  File='control.exe';   Arg='wscui.cpl' },
    @{ Name='Sound Settings';            File='control.exe';   Arg='mmsys.cpl' },
    @{ Name='System Properties';         File='control.exe';   Arg='sysdm.cpl' },
    @{ Name='Time and Date';             File='control.exe';   Arg='timedate.cpl' },
    @{ Name='Windows Defender Firewall'; File='control.exe';   Arg='firewall.cpl' },
    @{ Name='Windows Restore';           File='rstrui.exe';    Arg='' }
)


>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
