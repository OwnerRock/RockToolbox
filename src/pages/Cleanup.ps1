# Module: pages/Cleanup - Cleanup page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (12) Cleanup ----
$script:CleanLoaded = $false
$script:CleanDefs = @(
    @{ Id = 'usertemp';  Name = 'User temporary files';           Rec = $true;  Desc = 'Your %TEMP% folder - leftovers of installers and apps.'; Paths = @("$env:TEMP\*") },
    @{ Id = 'wintemp';   Name = 'Windows temporary files';        Rec = $true;  Desc = 'C:\Windows\Temp'; Paths = @("$env:windir\Temp\*") },
    @{ Id = 'wucache';   Name = 'Windows Update download cache';  Rec = $true;  Desc = 'Already-downloaded update files (Windows re-downloads what it needs). Update services are paused while cleaning.'; Paths = @("$env:windir\SoftwareDistribution\Download\*") },
    @{ Id = 'delivopt';  Name = 'Delivery Optimization cache';    Rec = $true;  Desc = 'Update files shared with other PCs.'; Paths = @("$env:windir\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache\*") },
    @{ Id = 'wer';       Name = 'Windows error reports';          Rec = $true;  Desc = 'Crash reports waiting to be sent.'; Paths = @("$env:ProgramData\Microsoft\Windows\WER\ReportArchive\*", "$env:ProgramData\Microsoft\Windows\WER\ReportQueue\*", "$env:ProgramData\Microsoft\Windows\WER\Temp\*") },
    @{ Id = 'dumps';     Name = 'Crash dumps';                    Rec = $true;  Desc = 'Memory dumps from blue screens (MEMORY.DMP, Minidump).'; Paths = @("$env:windir\Minidump\*", "$env:windir\MEMORY.DMP", "$env:windir\LiveKernelReports\*.dmp") },
    @{ Id = 'cbslogs';   Name = 'Windows servicing logs';         Rec = $true;  Desc = 'CBS and DISM log files.'; Paths = @("$env:windir\Logs\CBS\*.log", "$env:windir\Logs\CBS\*.cab", "$env:windir\Logs\DISM\*.log") },
    @{ Id = 'thumbs';    Name = 'Thumbnail cache';                Rec = $true;  Desc = 'Explorer rebuilds the picture previews when needed.'; Paths = @("$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db") },
    @{ Id = 'recycle';   Name = 'Recycle Bin';                    Rec = $false; Desc = 'Permanently deletes everything in the Recycle Bin of all drives.'; Paths = @() },
    @{ Id = 'chrome';    Name = 'Google Chrome cache';            Rec = $true;  Desc = 'Cached web pages (not passwords, history or bookmarks).'; Paths = @("$env:LOCALAPPDATA\Google\Chrome\User Data\*\Cache\*", "$env:LOCALAPPDATA\Google\Chrome\User Data\*\Code Cache\*", "$env:LOCALAPPDATA\Google\Chrome\User Data\*\GPUCache\*") },
    @{ Id = 'edge';      Name = 'Microsoft Edge cache';           Rec = $true;  Desc = 'Cached web pages (not passwords, history or bookmarks).'; Paths = @("$env:LOCALAPPDATA\Microsoft\Edge\User Data\*\Cache\*", "$env:LOCALAPPDATA\Microsoft\Edge\User Data\*\Code Cache\*", "$env:LOCALAPPDATA\Microsoft\Edge\User Data\*\GPUCache\*") },
    @{ Id = 'firefox';   Name = 'Firefox cache';                  Rec = $true;  Desc = 'Cached web pages (not passwords, history or bookmarks).'; Paths = @("$env:LOCALAPPDATA\Mozilla\Firefox\Profiles\*\cache2\*") },
    @{ Id = 'prefetch';  Name = 'Prefetch files';                 Rec = $false; Desc = 'Windows rebuilds these, apps open a little slower the first time after cleaning.'; Paths = @("$env:windir\Prefetch\*.pf") },
    @{ Id = 'winold';    Name = 'Previous Windows installation (Windows.old)'; Rec = $false; Desc = 'Frees many GB but you can no longer roll back to the previous Windows version. Slow to delete.'; Paths = @("$env:SystemDrive\Windows.old") }
)

$script:CleanScanScript = {
    param($st, $defs)
    $ErrorActionPreference = 'SilentlyContinue'
    $n = @($defs).Count; $i = 0
    foreach ($d in @($defs)) {
        $st.Pct = 100.0 * $i / $n
        $st.Msg = 'Measuring: ' + $d.Name
        $bytes = 0.0
        if ($d.Id -eq 'recycle') {
            $bytes = [double](Get-ChildItem -LiteralPath "$env:SystemDrive\`$Recycle.Bin" -Force -Recurse -File | Measure-Object Length -Sum).Sum
        } else {
            foreach ($p in @($d.Paths)) {
                $bytes += [double](Get-ChildItem -Path $p -Force -Recurse -File | Measure-Object Length -Sum).Sum
            }
        }
        [pscustomobject]@{ Id = [string]$d.Id; Bytes = [long]$bytes }
        $i++
    }
}.ToString()

$script:CleanRunScript = {
    param($st, $defs)
    $ErrorActionPreference = 'SilentlyContinue'
    function Measure-Defs($d) {
        $b = 0.0
        if ($d.Id -eq 'recycle') { return [double](Get-ChildItem -LiteralPath "$env:SystemDrive\`$Recycle.Bin" -Force -Recurse -File | Measure-Object Length -Sum).Sum }
        foreach ($p in @($d.Paths)) { $b += [double](Get-ChildItem -Path $p -Force -Recurse -File | Measure-Object Length -Sum).Sum }
        return $b
    }
    $n = @($defs).Count; $i = 0
    $stopped = @()
    foreach ($d in @($defs)) {
        if ($st.Cancel) { break }
        $st.Pct = 100.0 * $i / $n
        $st.Msg = 'Cleaning: ' + $d.Name
        [void]$st.Lines.Add('Cleaning: ' + $d.Name)
        $before = Measure-Defs $d
        if ($d.Id -eq 'wucache') {
            foreach ($s in 'wuauserv', 'bits') { if ((Get-Service $s).Status -eq 'Running') { Stop-Service $s -Force; $stopped += $s } }
        }
        if ($d.Id -eq 'recycle') {
            Clear-RecycleBin -Force -ErrorAction SilentlyContinue
        } elseif ($d.Id -eq 'winold') {
            $p = [string]$d.Paths[0]
            if (Test-Path -LiteralPath $p) {
                [void](& takeown.exe /F $p /R /D Y 2>&1)
                [void](& icacls.exe $p /grant '*S-1-5-32-544:F' /T /C /Q 2>&1)
                [void](& cmd.exe /c rd /s /q "`"$p`"" 2>&1)
            }
        } else {
            foreach ($p in @($d.Paths)) {
                foreach ($it in @(Get-ChildItem -Path $p -Force)) {
                    if ($st.Cancel) { break }
                    Remove-Item -LiteralPath $it.FullName -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        }
        if ($d.Id -eq 'wucache') { foreach ($s in $stopped) { Start-Service $s } ; $stopped = @() }
        $after = Measure-Defs $d
        $freed = $before - $after; if ($freed -lt 0) { $freed = 0 }
        [pscustomobject]@{ Id = [string]$d.Id; Freed = [long]$freed }
        $i++
    }
    foreach ($s in $stopped) { Start-Service $s }
}.ToString()

function Get-CleanItems {
    $src = $lvClean.ItemsSource
    if ($null -eq $src) { return @() }
    return @($src)
}
function Update-CleanCount {
    $sel = @(@(Get-CleanItems) | Where-Object { $_.Selected })
    $sum = 0.0
    foreach ($x in $sel) { $sum += [double]$x.Bytes }
    $txtCleanCount.Text = "$(L 'Selected'): $($sel.Count)  ($(L 'about') $(Format-Bytes $sum))"
}

function Scan-Cleanup {
    Start-Work {
        Log 'Measuring what can be cleaned...' 'INFO' 'Cleanup'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Pct = 0.0
        $st.Msg = 'Measuring...'
        $defs = @($script:CleanDefs | ForEach-Object { [pscustomobject]@{ Id = $_.Id; Name = $_.Name; Paths = @($_.Paths) } })
        $rows = @(Invoke-BgJob $script:CleanScanScript @(, $defs) $st)
        $map = @{}
        foreach ($r in $rows) { if ($r) { $map[[string]$r.Id] = [long]$r.Bytes } }
        $list = New-Object 'System.Collections.Generic.List[RockClean]'
        $total = 0.0
        foreach ($d in $script:CleanDefs) {
            $o = New-Object RockClean
            $o.Id = [string]$d.Id; $o.Name = [string]$d.Name; $o.Desc = [string]$d.Desc; $o.Recommended = [bool]$d.Rec
            $o.Bytes = [long]$map[[string]$d.Id]
            $o.Size = Format-Bytes $o.Bytes
            $o.Selected = ([bool]$d.Rec -and $o.Bytes -gt 0)
            $total += [double]$o.Bytes
            $list.Add($o)
        }
        $lvClean.ItemsSource = $list
        $script:CleanLoaded = $true
        Update-CleanCount
        Log "About $(Format-Bytes $total) can be cleaned in total." 'INFO' 'Cleanup'
    } 'Measuring...'
}

$btnCleanScan.Add_Click({ Scan-Cleanup })
$btnCleanRec.Add_Click({ foreach ($x in @(Get-CleanItems)) { $x.Selected = ([bool]$x.Recommended -and $x.Bytes -gt 0) }; Update-CleanCount })
$btnCleanAll.Add_Click({ foreach ($x in @(Get-CleanItems)) { $x.Selected = $true }; Update-CleanCount })
$btnCleanNone.Add_Click({ foreach ($x in @(Get-CleanItems)) { $x.Selected = $false }; Update-CleanCount })
$lvClean.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{ Update-CleanCount })
$lvClean.Add_MouseDoubleClick({ $it = $lvClean.SelectedItem; if ($it) { $it.Selected = -not $it.Selected; Update-CleanCount } })
$btnCleanRun.Add_Click({
    $sel = @(@(Get-CleanItems) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick what you want to clean first.' 'WARN' 'Cleanup'; return }
    $names = ($sel | Select-Object -First 10 | ForEach-Object { '- ' + $_.Name + '  (' + $_.Size + ')' }) -join "`n"
    if ($sel.Count -gt 10) { $names += "`n" + ((L '... and {0} more') -f ($sel.Count - 10)) }
    $extra = ''
    if (@($sel | Where-Object { $_.Id -eq 'winold' }).Count -gt 0) { $extra = "`n`n" + (L 'Windows.old will be deleted: you will not be able to go back to the previous Windows version.') }
    if (@($sel | Where-Object { $_.Id -eq 'recycle' }).Count -gt 0) { $extra += "`n`n" + (L 'The Recycle Bin will be emptied permanently.') }
    $r = [System.Windows.MessageBox]::Show(((L 'Clean {0} item(s)?') -f $sel.Count) + "`n`n$names$extra", 'Rock Toolbox', 'YesNo', 'Warning')
    if ([string]$r -ne 'Yes') { return }
    $defIds = @($sel | ForEach-Object { $_.Id })
    Start-Work {
        $st = New-WUState
        $st.Pct = 0.0
        $st.Msg = 'Cleaning...'
        $defs = @($script:CleanDefs | Where-Object { $defIds -contains $_.Id } | ForEach-Object { [pscustomobject]@{ Id = $_.Id; Name = $_.Name; Paths = @($_.Paths) } })
        $rows = @(Invoke-BgJob $script:CleanRunScript @(, $defs) $st)
        $freed = 0.0
        foreach ($r2 in $rows) { if ($r2) { $freed += [double]$r2.Freed } }
        Log "Cleanup finished - freed about $(Format-Bytes $freed)." 'INFO' 'Cleanup'
    } 'Cleaning...'
    Scan-Cleanup
})

