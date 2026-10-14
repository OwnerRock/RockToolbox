# Module: pages/Drivers - Drivers page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Drivers page (Install / Uninstall)
# ------------------------------------------------------------------
$script:DrvMode = ''
$script:DrvInstLoaded = $false
$script:DrvUnLoaded = $false

$script:DrvScanScript = {
    param($st)
    $ErrorActionPreference = 'Stop'
    $session  = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $found    = $searcher.Search("IsInstalled=0 and Type='Driver' and IsHidden=0")
    foreach ($u in $found.Updates) {
        $cls = ''; $prov = ''; $date = ''
        try { $cls = [string]$u.DriverClass } catch { }
        try { $prov = [string]$u.DriverProvider } catch { }
        try { $mf = [string]$u.DriverManufacturer; if ($mf) { $prov = $mf } } catch { }
        try { $date = ([datetime]$u.DriverVerDate).ToString('yyyy-MM-dd') } catch { }
        $mb = 0.0
        try { $mb = [double]$u.MaxDownloadSize / 1MB } catch { }
        $size = '-'
        if ($mb -ge 1024) { $size = ('{0:0.0} GB' -f ($mb / 1024)) } elseif ($mb -ge 1) { $size = ('{0:0} MB' -f $mb) } elseif ($mb -gt 0) { $size = '< 1 MB' }
        [pscustomobject]@{ Kind = 'Update'; Title = [string]$u.Title; Class = $cls; Provider = $prov; Date = $date; Size = $size; Id = [string]$u.Identity.UpdateID }
    }
    try {
        foreach ($d in @(Get-CimInstance Win32_PnPEntity -Filter 'ConfigManagerErrorCode <> 0')) {
            $nm = [string]$d.Name
            if (-not $nm) { $nm = [string]$d.DeviceID }
            [pscustomobject]@{ Kind = 'Device'; Title = $nm; Class = [string]$d.PNPClass; Provider = ''; Date = ''; Size = ''; Id = [string]$d.ConfigManagerErrorCode }
        }
    } catch { }
}.ToString()

$script:DrvListScript = {
    param($st)
    $ErrorActionPreference = 'Stop'
    foreach ($d in @(Get-WindowsDriver -Online)) {
        if ($d.BootCritical) { continue }
        $leaf = ''
        try { $leaf = Split-Path -Leaf ([string]$d.OriginalFileName) } catch { }
        if (-not $leaf) { $leaf = [string]$d.Driver }
        $date = ''
        try { $date = ([datetime]$d.Date).ToString('yyyy-MM-dd') } catch { }
        [pscustomobject]@{ Title = $leaf; Provider = [string]$d.ProviderName; Class = [string]$d.ClassName; Version = [string]$d.Version; Date = $date; Inf = [string]$d.Driver; Path = [string]$d.OriginalFileName }
    }
}.ToString()

$script:DrvUninstallScript = {
    param($st, $infs)
    $ErrorActionPreference = 'Continue'
    $n = @($infs).Count
    $i = 0
    foreach ($inf in @($infs)) {
        if ($st.Cancel) { [void]$st.Lines.Add('Stopped - the remaining drivers were skipped.'); break }
        $i++
        $st.Pct = (($i - 1) * 100.0) / $n
        $st.Msg = "Removing driver ($i/$n): $inf"
        [void]$st.Lines.Add("Removing driver ($i/$n): $inf")
        try {
            $out = & pnputil.exe /delete-driver $inf /uninstall /force 2>&1
            $code = $LASTEXITCODE
            foreach ($l in @($out)) { $t = ([string]$l).Trim(); if ($t) { [void]$st.Lines.Add('  ' + $t) } }
            if ($code -eq 0 -or $code -eq 3010) { $st.Ok = $st.Ok + 1 } else { $st.Fail = $st.Fail + 1 }
        } catch {
            $st.Fail = $st.Fail + 1
            [void]$st.Lines.Add('  failed: ' + $_.Exception.Message)
        }
        $st.Pct = ($i * 100.0) / $n
    }
}.ToString()

function Get-DrvItems([object]$lv) {
    $src = $lv.ItemsSource
    if ($null -eq $src) { return @() }
    return @($src)
}
function Update-DrvCount {
    $all = @(Get-DrvItems $lvDrvInstall)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtDrvCount.Text = "$(L 'Selected'): $n $(L 'of') $($all.Count)"
}
function Update-DrvUCount {
    $all = @(Get-DrvItems $lvDrvUn)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtDrvUCount.Text = "$(L 'Selected'): $n $(L 'of') $($all.Count)"
}

function Get-DrvKind {
    param([string]$Class, [string]$Title)
    switch ($Class.ToLower()) {
        'display'           { return 'Screen / Graphics card' }
        'media'             { return 'Sound / Audio' }
        'audioendpoint'     { return 'Sound / Audio' }
        'net'               { return 'Network (Wi-Fi / Ethernet)' }
        'bluetooth'         { return 'Bluetooth' }
        'camera'            { return 'Camera' }
        'image'             { return 'Camera / Scanner' }
        'hidclass'          { return 'Keyboard / Mouse / Touch' }
        'keyboard'          { return 'Keyboard' }
        'mouse'             { return 'Mouse / Touchpad' }
        'usb'               { return 'USB ports' }
        'system'            { return 'System / Chipset' }
        'scsiadapter'       { return 'Storage (SATA / NVMe)' }
        'hdc'               { return 'Storage controller' }
        'diskdrive'         { return 'Disk drive' }
        'printer'           { return 'Printer' }
        'printqueue'        { return 'Printer' }
        'monitor'           { return 'Monitor' }
        'battery'           { return 'Battery / Power' }
        'firmware'          { return 'Firmware / BIOS' }
        'processor'         { return 'CPU' }
        'biometric'         { return 'Fingerprint / Face login' }
        'sensor'            { return 'Sensors' }
        'ports'             { return 'COM / LPT ports' }
        'modem'             { return 'Modem' }
        'wpd'               { return 'Phone / Portable device' }
        'softwarecomponent' { return 'Software component' }
        'extension'         { return 'Driver extension' }
    }
    $t = $Title.ToLower()
    if ($t -match 'audio|sound|speaker|microphone|realtek.*hd') { return 'Sound / Audio' }
    if ($t -match 'graphics|display|geforce|radeon|nvidia|uhd|iris') { return 'Screen / Graphics card' }
    if ($t -match 'wi-?fi|wireless|wlan|ethernet|network|gigabit') { return 'Network (Wi-Fi / Ethernet)' }
    if ($t -match 'bluetooth') { return 'Bluetooth' }
    if ($t -match 'camera|webcam') { return 'Camera' }
    if ($t -match 'touchpad|mouse|keyboard') { return 'Keyboard / Mouse / Touch' }
    if ($t -match 'chipset|management engine|serial io|gpio|thermal|platform') { return 'System / Chipset' }
    if ($t -match 'nvme|sata|raid|storage') { return 'Storage (SATA / NVMe)' }
    if ($t -match 'usb') { return 'USB ports' }
    if ($Class) { return $Class }
    return 'Other'
}

function Scan-DrvInstall {
    Start-Work {
        Log 'Looking for missing and newer drivers on Windows Update...' 'INFO' 'Drivers'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Looking for missing and newer drivers...'
        $rows = Invoke-BgJob $script:DrvScanScript @() $st
        $list = New-Object 'System.Collections.Generic.List[RockDrv]'
        $bad = @()
        foreach ($r in @($rows)) {
            if (-not $r) { continue }
            if ([string]$r.Kind -eq 'Device') { $bad += ([string]$r.Title + ' [' + (Get-DrvKind ([string]$r.Class) ([string]$r.Title)) + ']'); continue }
            $o = New-Object RockDrv
            $o.Title = [string]$r.Title; $o.DrvClass = [string]$r.Class; $o.Provider = [string]$r.Provider
            $o.DrvDate = [string]$r.Date; $o.Size = [string]$r.Size; $o.Id = [string]$r.Id
            $o.Kind = Get-DrvKind $o.DrvClass $o.Title
            $list.Add($o)
        }
        $lvDrvInstall.ItemsSource = $list
        $script:DrvInstLoaded = $true
        Update-DrvCount
        $msg = "$($list.Count) driver(s) available from Windows Update."
        if ($bad.Count -gt 0) { $msg += "  Devices with a missing or faulty driver ($($bad.Count)): " + (($bad | Select-Object -First 6) -join ', ') }
        elseif ($list.Count -eq 0) { $msg = 'No missing drivers found - everything looks up to date.' }
        $txtDrvInfo.Text = $msg
        Log $msg 'INFO' 'Drivers'
    } 'Looking for drivers...'
}

function Scan-DrvUninstall {
    Start-Work {
        Log 'Reading the installed drivers (this can take up to a minute)...' 'INFO' 'Drivers'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Reading installed drivers...'
        $rows = Invoke-BgJob $script:DrvListScript @() $st
        $list = New-Object 'System.Collections.Generic.List[RockDrv]'
        foreach ($r in @($rows | Sort-Object { $_.Provider }, { $_.Class })) {
            if (-not $r) { continue }
            $o = New-Object RockDrv
            $o.Title = [string]$r.Title; $o.Provider = [string]$r.Provider; $o.DrvClass = [string]$r.Class
            $o.Version = [string]$r.Version; $o.DrvDate = [string]$r.Date; $o.Id = [string]$r.Inf; $o.FilePath = [string]$r.Path
            $o.Kind = Get-DrvKind $o.DrvClass $o.Title
            $list.Add($o)
        }
        $lvDrvUn.ItemsSource = $list
        $script:DrvUnLoaded = $true
        Update-DrvUCount
        $txtDrvUInfo.Text = "$($list.Count) " + (L 'third-party driver packages. Boot-critical drivers are hidden. Removing a driver can stop a device from working - create a restore point first.')
        Log "$($list.Count) driver packages found." 'INFO' 'Drivers'
    } 'Reading installed drivers...'
}

function Show-DrvMode {
    param([string]$Mode)
    $script:DrvMode = $Mode
    $pnlDrvWelcome.Visibility = 'Collapsed'; $pnlDrvInstall.Visibility = 'Collapsed'; $pnlDrvUninstall.Visibility = 'Collapsed'
    if ($Mode -eq 'Install') {
        $pnlDrvInstall.Visibility = 'Visible'; Start-PageAnim $pnlDrvInstall 12
        if (-not $script:DrvInstLoaded) { Scan-DrvInstall }
    } elseif ($Mode -eq 'Uninstall') {
        $pnlDrvUninstall.Visibility = 'Visible'; Start-PageAnim $pnlDrvUninstall 12
        if (-not $script:DrvUnLoaded) { Scan-DrvUninstall }
    } else {
        $pnlDrvWelcome.Visibility = 'Visible'
    }
    if ($Mode -eq 'Install') { Set-Res $btnDrvModeInstall 'Background' '#4A78B0'; Set-Res $btnDrvModeInstall 'Foreground' '#FFFFFF' }
    else { Set-Res $btnDrvModeInstall 'Background' '#1E3547'; Set-Res $btnDrvModeInstall 'Foreground' '#F0F4FF' }
    if ($Mode -eq 'Uninstall') { Set-Res $btnDrvModeUninstall 'Background' '#4A78B0'; Set-Res $btnDrvModeUninstall 'Foreground' '#FFFFFF' }
    else { Set-Res $btnDrvModeUninstall 'Background' '#1E3547'; Set-Res $btnDrvModeUninstall 'Foreground' '#F0F4FF' }
}

$btnDrvModeInstall.Add_Click({ Show-DrvMode 'Install' })
$btnDrvModeUninstall.Add_Click({ Show-DrvMode 'Uninstall' })
$btnDrvPickInstall.Add_Click({ Show-DrvMode 'Install' })
$btnDrvPickUninstall.Add_Click({ Show-DrvMode 'Uninstall' })
$btnDrvScan.Add_Click({ Scan-DrvInstall })
$btnDrvSelAll.Add_Click({ foreach ($d in @(Get-DrvItems $lvDrvInstall)) { $d.Selected = $true }; Update-DrvCount })
$btnDrvSelNone.Add_Click({ foreach ($d in @(Get-DrvItems $lvDrvInstall)) { $d.Selected = $false }; Update-DrvCount })
$btnDrvInstall.Add_Click({
    $sel = @(@(Get-DrvItems $lvDrvInstall) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the drivers you want to install first.' 'WARN' 'Drivers'; return }
    $ids = @($sel | ForEach-Object { $_.Id })
    Invoke-WUInstall -Ids $ids -Restart $false -Rescan { Scan-DrvInstall }
})
$btnDrvUScan.Add_Click({ Scan-DrvUninstall })
$btnDrvUSelNone.Add_Click({ foreach ($d in @(Get-DrvItems $lvDrvUn)) { $d.Selected = $false }; Update-DrvUCount })
$btnDrvUninstall.Add_Click({
    $sel = @(@(Get-DrvItems $lvDrvUn) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the drivers you want to remove first.' 'WARN' 'Drivers'; return }
    $names = ($sel | Select-Object -First 8 | ForEach-Object { '- ' + $_.Provider + ' / ' + $_.Title }) -join "`n"
    if ($sel.Count -gt 8) { $names += "`n" + ((L '... and {0} more') -f ($sel.Count - 8)) }
    $r = [System.Windows.MessageBox]::Show(((L 'Remove {0} driver package(s)?') -f $sel.Count) + "`n`n$names`n`n" + (L 'A removed driver can stop its device from working until you install it again.'), 'Rock Toolbox', 'YesNo', 'Warning')
    if ([string]$r -ne 'Yes') { return }
    $infs = @($sel | ForEach-Object { $_.Id })
    Start-Work {
        if (-not (New-SafetyRestorePoint 'removing drivers')) { throw 'Cancelled by user' }
        $st = New-WUState
        $st.Pct = 0.0
        $st.Msg = 'Removing drivers...'
        [void](Invoke-BgJob $script:DrvUninstallScript @(, $infs) $st)
        Log ("Finished: $($st.Ok) removed, $($st.Fail) failed.") 'INFO' 'Drivers'
    } 'Removing drivers...'
    Scan-DrvUninstall
})
$lvDrvInstall.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{ Update-DrvCount })
$lvDrvUn.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{
    param($sender, $e)
    $btn = $e.OriginalSource
    if ($btn -is [System.Windows.Controls.Button]) {
        $e.Handled = $true
        $fp = [string]$btn.Tag
        try {
            if ($fp -and (Test-Path -LiteralPath $fp)) {
                Start-Process explorer.exe -ArgumentList "/select,`"$fp`""
            } elseif ($fp -and (Test-Path -LiteralPath (Split-Path -Parent $fp))) {
                Start-Process explorer.exe -ArgumentList "`"$(Split-Path -Parent $fp)`""
            } else {
                Log 'Could not find the folder of this driver.' 'WARN' 'Drivers'
            }
        } catch { Log "Could not open the folder: $($_.Exception.Message)" 'WARN' 'Drivers' }
        return
    }
    Update-DrvUCount
})
$lvDrvInstall.Add_MouseDoubleClick({ $it = $lvDrvInstall.SelectedItem; if ($it) { $it.Selected = -not $it.Selected; Update-DrvCount } })
$lvDrvUn.Add_MouseDoubleClick({ $it = $lvDrvUn.SelectedItem; if ($it) { $it.Selected = -not $it.Selected; Update-DrvUCount } })
Show-DrvMode ''

