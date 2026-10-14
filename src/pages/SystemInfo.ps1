# Module: pages/SystemInfo - System Info page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (11) System Info ----
$script:SysInfoLoaded = $false
$script:SysInfoRows = @()
$script:SysInfoScript = {
    param($st)
    $ErrorActionPreference = 'SilentlyContinue'
    $rows = New-Object System.Collections.ArrayList
    function FB([double]$b) {
        if ($b -ge 1TB) { return ('{0:0.0} TB' -f ($b / 1TB)) }
        if ($b -ge 1GB) { return ('{0:0.0} GB' -f ($b / 1GB)) }
        return ('{0:0} MB' -f ($b / 1MB))
    }
    function Add-Row([string]$sec, [string]$lbl, $val, $pct = $null, [bool]$warn = $false, [string]$live = '') {
        if ($null -eq $val -or ([string]$val).Trim() -eq '') { return }
        [void]$rows.Add([pscustomobject]@{ Section = $sec; Label = $lbl; Value = [string]$val; Pct = $pct; Warn = $warn; Live = $live })
    }
    $st.Msg = 'Reading Windows and computer info...'
    $os = Get-CimInstance Win32_OperatingSystem
    $cs = Get-CimInstance Win32_ComputerSystem
    $bios = Get-CimInstance Win32_BIOS
    $bb = Get-CimInstance Win32_BaseBoard
    $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $ver = [string]$cv.DisplayVersion
    if (-not $ver) { $ver = [string]$cv.ReleaseId }
    Add-Row 'Windows' 'Edition' $os.Caption
    Add-Row 'Windows' 'Version' ("$ver  (build $($os.BuildNumber).$($cv.UBR))")
    Add-Row 'Windows' 'Architecture' $os.OSArchitecture
    if ($os.InstallDate) { Add-Row 'Windows' 'Installed on' (([datetime]$os.InstallDate).ToString('yyyy-MM-dd')) }
    if ($os.LastBootUpTime) {
        $up = (Get-Date) - [datetime]$os.LastBootUpTime
        Add-Row 'Windows' 'Uptime' ('{0} d {1} h {2} min {3} s' -f $up.Days, $up.Hours, $up.Minutes, $up.Seconds) $null $false 'up'
    }
    Add-Row 'Windows' 'Computer name' $env:COMPUTERNAME
    try { $sb = Confirm-SecureBootUEFI; if ($sb) { Add-Row 'Windows' 'Secure Boot' 'On' } else { Add-Row 'Windows' 'Secure Boot' 'Off' } } catch { Add-Row 'Windows' 'Secure Boot' 'Not supported (legacy BIOS)' }
    try { $tpm = Get-Tpm; if ($tpm.TpmPresent) { Add-Row 'Windows' 'TPM' ("Present, ready: $($tpm.TpmReady)") } else { Add-Row 'Windows' 'TPM' 'Not found' } } catch { }

    Add-Row 'Computer' 'Manufacturer' $cs.Manufacturer
    Add-Row 'Computer' 'Model' $cs.Model
    Add-Row 'Computer' 'Motherboard' (("$($bb.Manufacturer) $($bb.Product)").Trim())
    Add-Row 'Computer' 'BIOS' (("$($bios.SMBIOSBIOSVersion)  " + $(if ($bios.ReleaseDate) { '(' + ([datetime]$bios.ReleaseDate).ToString('yyyy-MM-dd') + ')' } else { '' })).Trim())
    Add-Row 'Computer' 'Serial number' $bios.SerialNumber

    $st.Msg = 'Reading processor and memory...'
    foreach ($c in @(Get-CimInstance Win32_Processor)) {
        Add-Row 'Processor' 'Name' (([string]$c.Name) -replace '\s+', ' ').Trim()
        Add-Row 'Processor' 'Cores / threads' ("$($c.NumberOfCores) cores / $($c.NumberOfLogicalProcessors) threads")
        Add-Row 'Processor' 'Max speed' ('{0:0.00} GHz' -f ($c.MaxClockSpeed / 1000.0))
        Add-Row 'Processor' 'Current load' ("$($c.LoadPercentage) %") ([int]$c.LoadPercentage) $false 'cpu'
    }
    $total = [double]$os.TotalVisibleMemorySize * 1KB
    $free = [double]$os.FreePhysicalMemory * 1KB
    if ($total -gt 0) {
        $usedPct = [int](100 * ($total - $free) / $total)
        Add-Row 'Memory' 'RAM in use' ("$(FB ($total - $free)) of $(FB $total)  ($usedPct %)") $usedPct ($usedPct -ge 90) 'ram'
    }
    $types = @{ 20 = 'DDR'; 21 = 'DDR2'; 24 = 'DDR3'; 26 = 'DDR4'; 34 = 'DDR5'; 30 = 'LPDDR4'; 35 = 'LPDDR5' }
    $i = 1
    foreach ($m in @(Get-CimInstance Win32_PhysicalMemory)) {
        $tp = $types[[int]$m.SMBIOSMemoryType]
        $sp = [int]$m.ConfiguredClockSpeed; if (-not $sp) { $sp = [int]$m.Speed }
        Add-Row 'Memory' "Module $i" (("$(FB $m.Capacity)  $tp  $sp MHz  $($m.Manufacturer)  $($m.PartNumber)") -replace '\s+', ' ').Trim()
        $i++
    }

    $st.Msg = 'Reading graphics...'
    $classKey = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
    $vramMap = @{}
    foreach ($k in @(Get-ChildItem $classKey)) {
        $pr = Get-ItemProperty $k.PSPath
        if ($pr.DriverDesc -and $pr.'HardwareInformation.qwMemorySize') { $vramMap[[string]$pr.DriverDesc] = [double]$pr.'HardwareInformation.qwMemorySize' }
    }
    foreach ($g in @(Get-CimInstance Win32_VideoController)) {
        Add-Row 'Graphics' 'Card' $g.Name
        $vr = $vramMap[[string]$g.Name]; if (-not $vr) { $vr = [double]$g.AdapterRAM }
        if ($vr -gt 0) { Add-Row 'Graphics' '  Video memory' (FB $vr) }
        $dd = ''; if ($g.DriverDate) { $dd = '  (' + ([datetime]$g.DriverDate).ToString('yyyy-MM-dd') + ')' }
        Add-Row 'Graphics' '  Driver' ("$($g.DriverVersion)$dd")
        if ($g.CurrentHorizontalResolution) { Add-Row 'Graphics' '  Resolution' ("$($g.CurrentHorizontalResolution) x $($g.CurrentVerticalResolution) @ $($g.CurrentRefreshRate) Hz") }
    }

    if (@(Get-CimInstance Win32_VideoController).Count -gt 0) { Add-Row 'Graphics' 'GPU load now (3D engine)' '0 %' 0 $false 'gpu' }

    $st.Msg = 'Reading storage...'
    foreach ($d in @(Get-PhysicalDisk)) {
        $hs = [string]$d.HealthStatus
        Add-Row 'Storage' ("Disk: " + $d.FriendlyName) ("$(FB $d.Size)  $($d.MediaType)  $($d.BusType)  -  health: $hs") $null ($hs -and $hs -ne 'Healthy')
    }
    foreach ($v in @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3')) {
        if ($v.Size) {
            $usedPct = [int](100 * ($v.Size - $v.FreeSpace) / $v.Size)
            Add-Row 'Storage' ("Drive " + $v.DeviceID + "  " + $v.VolumeName) ("$(FB $v.FreeSpace) free of $(FB $v.Size)  ($usedPct % used)") $usedPct ($usedPct -ge 90) ('vol:' + $v.DeviceID)
        }
    }

    Add-Row 'Storage' 'Disk activity now' 'Read 0 B/s    Write 0 B/s    (0 %)' 0 $false 'disk'

    $st.Msg = 'Reading network...'
    foreach ($a in @(Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' })) {
        $ip = (@(Get-NetIPAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4) | ForEach-Object { $_.IPAddress }) -join ', '
        Add-Row 'Network' $a.Name ("$($a.InterfaceDescription)  -  $($a.LinkSpeed)  -  $ip  -  $($a.MacAddress)")
        Add-Row 'Network' '    Traffic now' 'Down 0 B/s    Up 0 B/s' $null $false ('net:' + $a.Name)
    }

    $bat = @(Get-CimInstance Win32_Battery)
    if ($bat.Count -gt 0) {
        $st.Msg = 'Reading battery...'
        foreach ($b in $bat) {
            $stt = @{ 1 = 'Discharging'; 2 = 'On AC power'; 3 = 'Fully charged'; 4 = 'Low'; 5 = 'Critical'; 6 = 'Charging'; 7 = 'Charging (high)'; 8 = 'Charging (low)'; 9 = 'Charging (critical)'; 11 = 'Partially charged' }[[int]$b.BatteryStatus]
            Add-Row 'Battery' 'Charge' ("$($b.EstimatedChargeRemaining) %  $stt") ([int]$b.EstimatedChargeRemaining) $false 'bat'
        }
        $des = [double](Get-CimInstance -Namespace root\wmi -ClassName BatteryStaticData | Select-Object -First 1).DesignedCapacity
        $ful = [double](Get-CimInstance -Namespace root\wmi -ClassName BatteryFullChargedCapacity | Select-Object -First 1).FullChargedCapacity
        if ($des -gt 0 -and $ful -gt 0) {
            $h = [int](100 * $ful / $des)
            Add-Row 'Battery' 'Health' ("$h %   (full charge $([int]$ful) mWh of $([int]$des) mWh designed)") $h ($h -lt 60)
        }
        $cyc = (Get-CimInstance -Namespace root\wmi -ClassName BatteryCycleCount | Select-Object -First 1).CycleCount
        if ($cyc) { Add-Row 'Battery' 'Charge cycles' $cyc }
    }
    return $rows
}.ToString()

function Show-SysInfo {
    param($Rows)
    $siPanel.Children.Clear()
    $script:SysLive = @{}
    $secs = [ordered]@{}
    foreach ($r in $Rows) {
        $s = [string]$r.Section
        if (-not $secs.Contains($s)) { $secs[$s] = New-Object System.Collections.ArrayList }
        [void]$secs[$s].Add($r)
    }
    foreach ($s in $secs.Keys) {
        $h = New-Object System.Windows.Controls.TextBlock
        $h.Text = $s; $h.Style = $window.FindResource('H')
        [void]$siPanel.Children.Add($h)
        $g = New-Object System.Windows.Controls.Grid
        $g.Margin = '6,0,0,4'
        $c1 = New-Object System.Windows.Controls.ColumnDefinition; $c1.Width = New-Object System.Windows.GridLength(210)
        $c2 = New-Object System.Windows.Controls.ColumnDefinition; $c2.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
        [void]$g.ColumnDefinitions.Add($c1); [void]$g.ColumnDefinitions.Add($c2)
        $ri = 0
        foreach ($r in $secs[$s]) {
            $rd = New-Object System.Windows.Controls.RowDefinition; $rd.Height = [System.Windows.GridLength]::Auto
            [void]$g.RowDefinitions.Add($rd)
            $lbl = New-Object System.Windows.Controls.TextBlock
            $lbl.Text = [string]$r.Label; $lbl.Margin = '0,3,12,3'; $lbl.TextWrapping = 'Wrap'
            Set-Res $lbl 'Foreground' '#9AA0B8'
            [System.Windows.Controls.Grid]::SetRow($lbl, $ri); [System.Windows.Controls.Grid]::SetColumn($lbl, 0)
            [void]$g.Children.Add($lbl)
            $stack = New-Object System.Windows.Controls.StackPanel
            $val = New-Object System.Windows.Controls.TextBlock
            $val.Text = [string]$r.Value; $val.Margin = '0,3,0,3'; $val.TextWrapping = 'Wrap'
            if ([bool]$r.Warn) { Set-Res $val 'Foreground' '#FFB454' } else { Set-Res $val 'Foreground' '#E6E8F2' }
            [void]$stack.Children.Add($val)
            if ($r.Live) { $script:SysLive[[string]$r.Live] = @{ Row = $r; Text = $val; Bar = $null } }
            if ($null -ne $r.Pct) {
                $pb = New-Object System.Windows.Controls.ProgressBar
                $pb.Style = $window.FindResource('StripeBar')
                $pb.Height = 6; $pb.Minimum = 0; $pb.Maximum = 100; $pb.Value = [double]$r.Pct
                $pb.Margin = '0,0,40,4'
                if ([bool]$r.Warn) { Set-Res $pb 'Foreground' '#FF6B6B' } else { Set-Res $pb 'Foreground' '#4FD1FF' }
                Set-Res $pb 'Background' '#0F1116'; Set-Res $pb 'BorderBrush' '#2B3038'
                [void]$stack.Children.Add($pb)
                if ($r.Live) { $script:SysLive[[string]$r.Live].Bar = $pb }
            }
            [System.Windows.Controls.Grid]::SetRow($stack, $ri); [System.Windows.Controls.Grid]::SetColumn($stack, 1)
            [void]$g.Children.Add($stack)
            $ri++
        }
        [void]$siPanel.Children.Add($g)
    }
}

function Get-SysInfoText {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("Rock Toolbox - System report  ($((Get-Date).ToString('yyyy-MM-dd HH:mm')))")
    $last = ''
    foreach ($r in $script:SysInfoRows) {
        if ($r.Section -ne $last) { [void]$sb.AppendLine(''); [void]$sb.AppendLine('== ' + $r.Section + ' =='); $last = $r.Section }
        [void]$sb.AppendLine(('{0,-28} {1}' -f ([string]$r.Label).Trim(), $r.Value))
    }
    return $sb.ToString()
}

function Load-SysInfo {
    Start-Work {
        Log 'Reading system information...' 'INFO' 'SysInfo'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Reading system information...'
        $rows = @(Invoke-BgJob $script:SysInfoScript @() $st)
        $script:SysInfoRows = @($rows | Where-Object { $_ -and $_.Section })
        Show-SysInfo $script:SysInfoRows
        $script:SysInfoLoaded = $true
        $txtSiInfo.Text = L 'Live while this tab is open: processor, memory, uptime, network traffic, disk activity, drive space, battery and GPU load. Press Refresh to read everything again.'
        Log 'System information ready.' 'INFO' 'SysInfo'
    } 'Reading system information...'
}

