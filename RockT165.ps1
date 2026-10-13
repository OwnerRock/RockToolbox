function Update-DrvUCount {
    $all = @(Get-DrvItems $lvDrvUn)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtDrvUCount.Text = "Selected: $n of $($all.Count)"
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
            $o.Version = [string]$r.Version; $o.DrvDate = [string]$r.Date; $o.Id = [string]$r.Inf
            $o.Kind = Get-DrvKind $o.DrvClass $o.Title
            $list.Add($o)
        }
        $lvDrvUn.ItemsSource = $list
        $script:DrvUnLoaded = $true
        Update-DrvUCount
        $txtDrvUInfo.Text = "$($list.Count) third-party driver packages. Boot-critical drivers are hidden. Removing a driver can stop a device from working - create a restore point first."
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
