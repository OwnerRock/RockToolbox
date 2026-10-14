# Module: pages/SystemInfoLive - System Info live (1 second) refresh
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- live (1 second) refresh of CPU load, memory use and uptime while the System Info tab is open ----
$script:SysLive = @{}
$script:CpuCounter = $null
$script:MemInfo = $null
$script:BootTime = $null
function Set-BarSmooth {
    param($Bar, [double]$V)
    if (-not $Bar) { return }
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation
    $a.To = $V
    $a.Duration = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(600))
    $Bar.BeginAnimation([System.Windows.Controls.Primitives.RangeBase]::ValueProperty, $a)
}
function Set-LiveLook {
    param($Entry, [bool]$Warn)
    if ($Warn) { Set-Res $Entry.Text 'Foreground' '#FFB454' } else { Set-Res $Entry.Text 'Foreground' '#E6E8F2' }
    if ($Entry.Bar) { if ($Warn) { Set-Res $Entry.Bar 'Foreground' '#FF6B6B' } else { Set-Res $Entry.Bar 'Foreground' '#4FD1FF' } }
}
$script:LiveTick = 0
$script:NetPrev = @{}
$script:DiskCtr = $null
$script:GpuEvery = 3
function Update-SysLive {
    if ($script:CurPage -ne 'System Info' -or $script:SysLive.Count -eq 0) { return }
    $script:LiveTick++
    $tick = $script:LiveTick
    try {
        # ---- processor (every second)
        if ($script:SysLive.ContainsKey('cpu')) {
            $e = $script:SysLive['cpu']
            if ($script:CpuCounter) { $raw = [double]$script:CpuCounter.NextValue() }
            else { $raw = [double](Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'").PercentProcessorTime }
            $v = [int][Math]::Min(100, [Math]::Round($raw))
            $e.Text.Text = "$v %"
            $e.Row.Value = "$v %"
            Set-BarSmooth $e.Bar $v
            Set-LiveLook $e ($v -ge 90)
        }
        # ---- memory (every second)
        if ($script:SysLive.ContainsKey('ram') -and $script:MemInfo) {
            $e = $script:SysLive['ram']
            $tot = [double]$script:MemInfo.TotalPhysicalMemory
            $free = [double]$script:MemInfo.AvailablePhysicalMemory
            if ($tot -gt 0) {
                $pct = [int][Math]::Round(100 * ($tot - $free) / $tot)
                $txt = "$(Format-Bytes ($tot - $free)) of $(Format-Bytes $tot)  ($pct %)"
                $e.Text.Text = $txt
                $e.Row.Value = $txt
                Set-BarSmooth $e.Bar $pct
                Set-LiveLook $e ($pct -ge 90)
            }
        }
        # ---- uptime (every second)
        if ($script:SysLive.ContainsKey('up') -and $script:BootTime) {
            $up = (Get-Date) - $script:BootTime
            $txt = '{0} d {1} h {2} min {3} s' -f $up.Days, $up.Hours, $up.Minutes, $up.Seconds
            $script:SysLive['up'].Text.Text = $txt
            $script:SysLive['up'].Row.Value = $txt
        }
        # ---- network traffic per adapter (every second)
        $netKeys = @($script:SysLive.Keys | Where-Object { ([string]$_).StartsWith('net:') })
        if ($netKeys.Count -gt 0) {
            $now = [DateTime]::UtcNow
            $nics = @{}
            foreach ($n in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) { $nics[[string]$n.Name] = $n }
            foreach ($k in $netKeys) {
                $name = ([string]$k).Substring(4)
                $n = $nics[$name]
                if (-not $n) { continue }
                $st4 = $n.GetIPv4Statistics()
                $rx = [double]$st4.BytesReceived; $tx = [double]$st4.BytesSent
                $prev = $script:NetPrev[$name]
                if ($prev) {
                    $dt = ($now - $prev.T).TotalSeconds
                    if ($dt -gt 0.2) {
                        $dn = [Math]::Max(0, ($rx - $prev.Rx) / $dt); $upr = [Math]::Max(0, ($tx - $prev.Tx) / $dt)
                        $txt = "Down $(Format-Bytes $dn)/s    Up $(Format-Bytes $upr)/s"
                        $script:SysLive[$k].Text.Text = $txt
                        $script:SysLive[$k].Row.Value = $txt
                    }
                }
                $script:NetPrev[$name] = @{ T = $now; Rx = $rx; Tx = $tx }
            }
        }
        # ---- disk activity (every second)
        if ($script:SysLive.ContainsKey('disk')) {
            $e = $script:SysLive['disk']
            $busy = $null; $rd = 0.0; $wr = 0.0
            if ($script:DiskCtr) {
                $busy = [double]$script:DiskCtr.T.NextValue(); $rd = [double]$script:DiskCtr.R.NextValue(); $wr = [double]$script:DiskCtr.W.NextValue()
            } elseif ($tick % 2 -eq 0) {
                $d = Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk -Filter "Name='_Total'"
                if ($d) { $busy = [double]$d.PercentDiskTime; $rd = [double]$d.DiskReadBytesPersec; $wr = [double]$d.DiskWriteBytesPersec }
            }
            if ($null -ne $busy) {
                $bp = [int][Math]::Min(100, [Math]::Round($busy))
                $txt = "Read $(Format-Bytes $rd)/s    Write $(Format-Bytes $wr)/s    ($bp %)"
                $e.Text.Text = $txt; $e.Row.Value = $txt
                Set-BarSmooth $e.Bar $bp
                Set-LiveLook $e ($bp -ge 95)
            }
        }
        # ---- battery (every 2 s)
        if ($script:SysLive.ContainsKey('bat') -and ($tick % 2 -eq 0)) { try {
            $e = $script:SysLive['bat']
            $ps = [System.Windows.Forms.SystemInformation]::PowerStatus
            $lvl = [int][Math]::Round(100 * [double]$ps.BatteryLifePercent)
            if ($lvl -le 100) {
                $state = 'Discharging'
                if ([string]$ps.PowerLineStatus -eq 'Online') { if (([string]$ps.BatteryChargeStatus) -match 'Charging') { $state = 'Charging' } else { $state = 'On AC power' } }
                $txt = "$lvl %  $state"
                $e.Text.Text = $txt; $e.Row.Value = $txt
                Set-BarSmooth $e.Bar $lvl
                Set-LiveLook $e (($lvl -le 15) -and ($state -eq 'Discharging'))
            }
        } catch { [void]$script:SysLive.Remove('bat') } }
        # ---- drive free space (every 5 s)
        if ($tick % 5 -eq 0) {
            foreach ($k in @($script:SysLive.Keys | Where-Object { ([string]$_).StartsWith('vol:') })) {
                $e = $script:SysLive[$k]
                $di = New-Object System.IO.DriveInfo(([string]$k).Substring(4))
                if ($di.IsReady -and $di.TotalSize -gt 0) {
                    $pct = [int][Math]::Round(100 * ($di.TotalSize - $di.TotalFreeSpace) / $di.TotalSize)
                    $txt = "$(Format-Bytes $di.TotalFreeSpace) free of $(Format-Bytes $di.TotalSize)  ($pct % used)"
                    $e.Text.Text = $txt; $e.Row.Value = $txt
                    Set-BarSmooth $e.Bar $pct
                    Set-LiveLook $e ($pct -ge 90)
                }
            }
        }
        # ---- GPU 3D load (WMI query is heavier, so it runs every few seconds and backs off if it is slow)
        if ($script:SysLive.ContainsKey('gpu') -and ($tick % $script:GpuEvery -eq 0)) {
            $e = $script:SysLive['gpu']
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $items = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction Stop | Where-Object { $_.Name -like '*engtype_3D*' })
            $sw.Stop()
            if ($sw.ElapsedMilliseconds -gt 300) { $script:GpuEvery = 10 } else { $script:GpuEvery = 3 }
            $sum = 0.0
            foreach ($it in $items) { $sum += [double]$it.UtilizationPercentage }
            $gp = [int][Math]::Min(100, [Math]::Round($sum))
            $e.Text.Text = "$gp %"; $e.Row.Value = "$gp %"
            Set-BarSmooth $e.Bar $gp
            Set-LiveLook $e ($gp -ge 95)
        }
    } catch {
        # a counter that is not available just stays at its last value
        if ($_.Exception.Message -match 'Invalid class|Not supported|not found') { [void]$script:SysLive.Remove('gpu') }
    }
}
$script:LiveTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:LiveTimer.Interval = [TimeSpan]::FromSeconds(1)
$script:LiveTimer.Add_Tick({ Update-SysLive })
function Start-SysLive {
    try {
        if (-not $script:CpuCounter) {
            $script:CpuCounter = New-Object System.Diagnostics.PerformanceCounter('Processor', '% Processor Time', '_Total')
            [void]$script:CpuCounter.NextValue()
        }
    } catch { $script:CpuCounter = $null }
    try {
        if (-not $script:MemInfo) {
            Add-Type -AssemblyName Microsoft.VisualBasic
            $script:MemInfo = New-Object Microsoft.VisualBasic.Devices.ComputerInfo
        }
    } catch { $script:MemInfo = $null }
    try { if (-not $script:BootTime) { $script:BootTime = [datetime](Get-CimInstance Win32_OperatingSystem).LastBootUpTime } } catch { }
    try { Add-Type -AssemblyName System.Windows.Forms } catch { }
    try {
        if (-not $script:DiskCtr) {
            $dc = @{
                T = New-Object System.Diagnostics.PerformanceCounter('PhysicalDisk', '% Disk Time', '_Total')
                R = New-Object System.Diagnostics.PerformanceCounter('PhysicalDisk', 'Disk Read Bytes/sec', '_Total')
                W = New-Object System.Diagnostics.PerformanceCounter('PhysicalDisk', 'Disk Write Bytes/sec', '_Total')
            }
            foreach ($c in $dc.Values) { [void]$c.NextValue() }
            $script:DiskCtr = $dc
        }
    } catch { $script:DiskCtr = $null }
    $script:LiveTick = 0; $script:NetPrev = @{}
    if (-not $script:LiveTimer.IsEnabled) { $script:LiveTimer.Start() }
}
function Stop-SysLive { if ($script:LiveTimer.IsEnabled) { $script:LiveTimer.Stop() } }

$btnSiRefresh.Add_Click({ Load-SysInfo })
$btnSiCopy.Add_Click({
    if ($script:SysInfoRows.Count -eq 0) { Log 'Nothing to copy yet - wait for the report to load.' 'WARN' 'SysInfo'; return }
    try { [System.Windows.Clipboard]::SetText((Get-SysInfoText)); Log 'System report copied to the clipboard.' 'INFO' 'SysInfo' } catch { Log 'Could not copy to the clipboard.' 'WARN' 'SysInfo' }
})
$btnSiSave.Add_Click({
    if ($script:SysInfoRows.Count -eq 0) { Log 'Nothing to save yet - wait for the report to load.' 'WARN' 'SysInfo'; return }
    $dlg = New-Object Microsoft.Win32.SaveFileDialog
    $dlg.Filter = 'Text file (*.txt)|*.txt'
    $dlg.FileName = 'System-report.txt'
    $dlg.InitialDirectory = [Environment]::GetFolderPath('Desktop')
    if ($dlg.ShowDialog()) {
        try { Set-Content -LiteralPath $dlg.FileName -Value (Get-SysInfoText) -Encoding UTF8; Log "Report saved: $($dlg.FileName)" 'INFO' 'SysInfo' } catch { Log "Could not save: $($_.Exception.Message)" 'ERROR' 'SysInfo' }
    }
})

