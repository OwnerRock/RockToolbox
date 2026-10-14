# Module: features/Safety - Automatic restore point before risky actions
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (1) automatic restore point before risky actions ----
$script:LastRestoreAt = [DateTime]::MinValue
$script:RestoreScript = {
    param($st, $desc)
    $ErrorActionPreference = 'Stop'
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
    $old = $null
    try { $old = (Get-ItemProperty -LiteralPath $key -Name SystemRestorePointCreationFrequency -ErrorAction Stop).SystemRestorePointCreationFrequency } catch { }
    try {
        Set-ItemProperty -LiteralPath $key -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -Force
        try { Enable-ComputerRestore -Drive "$env:SystemDrive\" } catch { }
        Checkpoint-Computer -Description $desc -RestorePointType MODIFY_SETTINGS
        return [pscustomobject]@{ Ok = $true; Msg = '' }
    } catch {
        return [pscustomobject]@{ Ok = $false; Msg = [string]$_.Exception.Message }
    } finally {
        try {
            if ($null -ne $old) { Set-ItemProperty -LiteralPath $key -Name SystemRestorePointCreationFrequency -Value $old -Type DWord -Force }
            else { Remove-ItemProperty -LiteralPath $key -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue }
        } catch { }
    }
}.ToString()

# returns $true when it is fine to continue (restore point made, skipped by setting, or the user chose to go on)
function New-SafetyRestorePoint {
    param([string]$Reason = 'changes')
    if (-not [bool]$script:Settings['AutoRestore']) { return $true }
    if (([DateTime]::Now - $script:LastRestoreAt).TotalMinutes -lt 20) { return $true }
    Log "Creating a restore point before $Reason (this can take a minute)..." 'INFO' 'Safety'
    $st = New-WUState
    $st.AbortNow = $true
    $st.Msg = 'Creating a restore point...'
    $res = $null
    try { $res = @(Invoke-BgJob $script:RestoreScript @("Rock Toolbox - before $Reason") $st) | Select-Object -First 1 }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user') { throw }
        $res = [pscustomobject]@{ Ok = $false; Msg = [string]$_.Exception.Message }
    }
    if ($res -and $res.Ok) {
        $script:LastRestoreAt = Get-Date
        Log 'Restore point created.' 'INFO' 'Safety'
        return $true
    }
    $msg = 'unknown error'
    if ($res -and $res.Msg) { $msg = [string]$res.Msg }
    Log "Could not create a restore point: $msg" 'WARN' 'Safety'
    $r = [System.Windows.MessageBox]::Show((L 'Rock Toolbox could not create a restore point:') + "`n`n$msg`n`n" + (L 'Continue without one?'), 'Rock Toolbox', 'YesNo', 'Warning')
    return ([string]$r -eq 'Yes')
}

$chkAutoRestore.Add_Click({
    $script:Settings['AutoRestore'] = [bool]$chkAutoRestore.IsChecked
    Save-Settings
})

