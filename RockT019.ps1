function Set-ToggleState {
    param($T, [bool]$On)
    if ($T.Custom) { if ($On) { & $T.Custom.On } else { & $T.Custom.Off }; return }
    $fail = 0; $last = ''
    foreach ($v in $T.Values) {
        $val  = if ($On) { $v.On } else { $v.Off }
        $type = if ($v.T) { $v.T } else { 'DWord' }
        try { Set-Reg $v.P $v.N $val $type } catch { $fail++; $last = $_.Exception.Message }
    }
    if ($fail -ge @($T.Values).Count) { throw $last }
    if ($fail -gt 0) { Log ("  " + $fail + " value(s) of '" + $T.Name + "' could not be written (protected key) - the rest were applied.") 'WARN' 'Customize' }
}

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
           Log 'Running SpotX in this console...' 'INFO' 'SpotX'
           # bring the console to the front while SpotX runs
           try {
               $hwnd = [WinToolboxNative]::GetConsoleWindow()
               if ($hwnd -ne [IntPtr]::Zero) {
                   [WinToolboxNative]::ShowWindow($hwnd, 5) | Out-Null
                   [WinToolboxNative]::SetForegroundWindow($hwnd) | Out-Null
               }
           } catch { }
           Write-Host ''
           Write-Host '========== SpotX ==========' -ForegroundColor Cyan
           iex "& { $(iwr -useb 'https://raw.githubusercontent.com/SpotX-Official/SpotX/refs/heads/main/run.ps1') } -new_theme"
           Write-Host '========== SpotX finished ==========' -ForegroundColor Cyan
           Write-Host ''
           Log 'SpotX finished.' 'INFO' 'SpotX'
           # return focus to the Rock Toolbox GUI window
           try {
               $helper = New-Object System.Windows.Interop.WindowInteropHelper($window)
               $guiHwnd = $helper.Handle
               if ($guiHwnd -ne [IntPtr]::Zero) {
                   [WinToolboxNative]::ShowWindow($guiHwnd, 5) | Out-Null
                   [WinToolboxNative]::SetForegroundWindow($guiHwnd) | Out-Null
               }
           } catch { }
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

# ------------------------------------------------------------------
# Build UI: Install
