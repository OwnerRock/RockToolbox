# Module: pages/Repair - Repair page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (15) Repair ----
$script:RpBuf = ''
$script:RpLastPct = -10.0
$script:RpAll = New-Object System.Text.StringBuilder

function Add-RepairLog {
    param([string]$Line)
    [void]$script:RpAll.AppendLine($Line)
    $txtRepairLog.AppendText($Line + "`r`n")
    $txtRepairLog.ScrollToEnd()
}

# feeds raw tool output: updates the progress bar from "12.5%" style text and writes the other lines to the log box
function Add-RepairText {
    param([string]$Text, [string]$Title)
    $t = $script:RpBuf + $Text
    $parts = [regex]::Split($t, '[\r\n]+')
    $script:RpBuf = [string]$parts[$parts.Count - 1]
    for ($i = 0; $i -lt $parts.Count - 1; $i++) {
        $seg = ([string]$parts[$i]).TrimEnd()
        if (-not $seg.Trim()) { continue }
        $m = [regex]::Match($seg, '(\d{1,3}(?:[.,]\d+)?)\s*(%|percent)')
        $isProg = $m.Success -and ($seg -match '^\s*\[?[=\s]*\d|Verification|complete|Stage|Progress|Total')
        if ($m.Success) {
            $v = 0.0
            if ([double]::TryParse(($m.Groups[1].Value -replace ',', '.'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$v)) {
                if ($v -ge 0 -and $v -le 100) { Set-Progress $v ($Title + '  ' + ('{0:0}%' -f $v)) }
            }
        }
        if ($isProg) {
            if ($m.Success -and [Math]::Floor([double]($m.Groups[1].Value -replace ',', '.') / 10) -ne [Math]::Floor($script:RpLastPct / 10)) {
                $script:RpLastPct = [double]($m.Groups[1].Value -replace ',', '.')
                Add-RepairLog ($seg -replace '=+', '=')
            }
        } else { Add-RepairLog $seg }
    }
}

function Invoke-RepairStep {
    param([string]$Title, [string]$File, [string]$ToolArgs, [string]$Encoding = 'OEM')
    $script:RpBuf = ''; $script:RpLastPct = -10.0
    Add-RepairLog ''
    Add-RepairLog ('=== ' + $Title + '   ' + (Get-Date).ToString('HH:mm:ss') + ' ===')
    Log "$Title..." 'INFO' 'Repair'
    Set-Progress -1 $Title
    $code = 0
    try { $code = Invoke-LiveTool -File $File -ToolArgs $ToolArgs -Encoding $Encoding -OnText { param($x) Add-RepairText $x $Title } }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user') { Add-RepairLog '--- stopped by user ---' }
        throw
    }
    if ($script:RpBuf.Trim()) { Add-RepairLog $script:RpBuf.Trim(); $script:RpBuf = '' }
    Add-RepairLog ("--- finished, exit code $code ---")
    return $code
}

function Get-RepairVerdict {
    param([string]$Kind, [int]$Code, [string]$Text)
    if ($Kind -eq 'sfc') {
        if ($Text -match 'did not find any integrity violations') { return 'SFC: no problems found.' }
        if ($Text -match 'successfully repaired') { return 'SFC: corrupted files were found and repaired. Restart Windows.' }
        if ($Text -match 'unable to fix') { return 'SFC: corrupted files were found but could not be fixed. Run "Repair image (DISM RestoreHealth)" and then SFC again.' }
        if ($Text -match 'pending|could not perform') { return 'SFC could not finish. Restart Windows and try again.' }
        return 'SFC finished - see the log.'
    }
    if ($Code -eq 0) { return 'DISM finished successfully.' }
    return "DISM finished with exit code $Code - see the log."
}

$btnRpSfc.Add_Click({ Start-Work {
    $before = $script:RpAll.Length
    $c = Invoke-RepairStep 'System File Checker (SFC)' "$env:windir\System32\sfc.exe" '/scannow' 'Unicode'
    $txt = $script:RpAll.ToString().Substring($before)
    $v = Get-RepairVerdict 'sfc' $c $txt
    Add-RepairLog $v; Log $v 'INFO' 'Repair'
} 'System File Checker (SFC)' })
$btnRpScan.Add_Click({ Start-Work {
    $c = Invoke-RepairStep 'DISM - ScanHealth' "$env:windir\System32\dism.exe" '/Online /Cleanup-Image /ScanHealth'
    $v = Get-RepairVerdict 'dism' $c ''
    Add-RepairLog $v; Log $v 'INFO' 'Repair'
} 'DISM - ScanHealth' })
$btnRpRestore.Add_Click({ Start-Work {
    $c = Invoke-RepairStep 'DISM - RestoreHealth' "$env:windir\System32\dism.exe" '/Online /Cleanup-Image /RestoreHealth'
    $v = Get-RepairVerdict 'dism' $c ''
    Add-RepairLog $v; Log $v 'INFO' 'Repair'
} 'DISM - RestoreHealth' })
$btnRpFull.Add_Click({
    $r = [System.Windows.MessageBox]::Show((L 'Run DISM RestoreHealth and then SFC?') + "`n`n" + (L 'This can take 15-40 minutes. You can press Stop at any time.'), 'Rock Toolbox', 'YesNo', 'Question')
    if ([string]$r -ne 'Yes') { return }
    Start-Work {
        $c1 = Invoke-RepairStep 'DISM - RestoreHealth' "$env:windir\System32\dism.exe" '/Online /Cleanup-Image /RestoreHealth'
        $v1 = Get-RepairVerdict 'dism' $c1 ''
        Add-RepairLog $v1; Log $v1 'INFO' 'Repair'
        $before = $script:RpAll.Length
        $c2 = Invoke-RepairStep 'System File Checker (SFC)' "$env:windir\System32\sfc.exe" '/scannow' 'Unicode'
        $v2 = Get-RepairVerdict 'sfc' $c2 ($script:RpAll.ToString().Substring($before))
        Add-RepairLog $v2; Log $v2 'INFO' 'Repair'
    } 'Full repair (DISM + SFC)'
})
$btnRpChk.Add_Click({ Start-Work {
    $c = Invoke-RepairStep 'Check disk (read-only)' "$env:windir\System32\chkdsk.exe" "$env:SystemDrive /scan"
    $v = (L 'Check disk finished (exit code') + ' ' + $c + (L '). 0 = no errors, 1 = errors were fixed, 2/3 = cleanup or errors found.')
    Add-RepairLog $v; Log $v 'INFO' 'Repair'
} 'Check disk (read-only)' })
$btnRpChkFix.Add_Click({
    $r = [System.Windows.MessageBox]::Show((L 'Schedule a full disk check and repair of') + " $env:SystemDrive " + (L 'for the next restart?') + "`n`n" + (L 'It can take a long time on large drives. The PC cannot be used while it runs.'), 'Rock Toolbox', 'YesNo', 'Warning')
    if ([string]$r -ne 'Yes') { return }
    Start-Work {
        $c = Invoke-RepairStep 'Schedule disk repair' "$env:ComSpec" "/c echo Y| chkdsk $env:SystemDrive /f /r"
        $v = L 'Disk repair is scheduled. Restart Windows to run it.'
        Add-RepairLog $v; Log $v 'WARN' 'Repair'
    } 'Schedule disk repair'
})
$btnRpComp.Add_Click({ Start-Work {
    $c = Invoke-RepairStep 'DISM - Clean component store' "$env:windir\System32\dism.exe" '/Online /Cleanup-Image /StartComponentCleanup'
    $v = Get-RepairVerdict 'dism' $c ''
    Add-RepairLog $v; Log $v 'INFO' 'Repair'
} 'DISM - Clean component store' })
$btnRpWu.Add_Click({
    $r = [System.Windows.MessageBox]::Show((L 'Reset the Windows Update components (services, SoftwareDistribution and catroot2 folders)?'), 'Rock Toolbox', 'YesNo', 'Question')
    if ([string]$r -ne 'Yes') { return }
    Start-Work {
        Add-RepairLog ''
        Add-RepairLog ('=== Reset Windows Update components   ' + (Get-Date).ToString('HH:mm:ss') + ' ===')
        Reset-WindowsUpdate
        Add-RepairLog 'Windows Update components were reset.'
    } 'Resetting Windows Update...'
})
$btnRpCopy.Add_Click({
    if ($txtRepairLog.Text.Length -eq 0) { Log 'The log is empty.' 'WARN' 'Repair'; return }
    try { [System.Windows.Clipboard]::SetText($txtRepairLog.Text); Log 'Log copied to the clipboard.' 'INFO' 'Repair' } catch { }
})
$btnRpClear.Add_Click({ $txtRepairLog.Clear(); [void]$script:RpAll.Clear() })

