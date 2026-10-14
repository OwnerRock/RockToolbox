# Module: features/PauseUpdates - Pause Windows updates
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (17) Pause updates ----
$script:UXKey = 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
$script:PauseOptions = [ordered]@{ '1 week' = 7; '2 weeks' = 14; '3 weeks' = 21; '4 weeks' = 28; '5 weeks (maximum)' = 35 }
foreach ($k in $script:PauseOptions.Keys) { [void]$cmbPauseDays.Items.Add($k) }
$cmbPauseDays.SelectedIndex = 1
$script:PauseNames = 'PauseUpdatesStartTime', 'PauseUpdatesExpiryTime', 'PauseFeatureUpdatesStartTime', 'PauseFeatureUpdatesEndTime', 'PauseQualityUpdatesStartTime', 'PauseQualityUpdatesEndTime'

function Get-PauseUntil {
    try {
        $v = (Get-ItemProperty -LiteralPath $script:UXKey -Name PauseUpdatesExpiryTime -ErrorAction Stop).PauseUpdatesExpiryTime
        $d = [DateTime]::Parse([string]$v, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal)
        if ($d -gt [DateTime]::UtcNow) { return $d.ToLocalTime() }
    } catch { }
    return $null
}
function Update-PauseState {
    $u = Get-PauseUntil
    if ($u) { $txtPauseState.Text = (L 'Paused until') + ' ' + $u.ToString('yyyy-MM-dd HH:mm') } else { $txtPauseState.Text = L 'Updates are not paused' }
}
$btnPause.Add_Click({ Start-Work {
    $days = [int]$script:PauseOptions[[string]$cmbPauseDays.SelectedItem]
    if ($days -le 0) { $days = 14 }
    $start = [DateTime]::UtcNow; $end = $start.AddDays($days)
    $fmt = 'yyyy-MM-dd\THH:mm:ss'
    $s = $start.ToString($fmt, [Globalization.CultureInfo]::InvariantCulture) + 'Z'
    $e = $end.ToString($fmt, [Globalization.CultureInfo]::InvariantCulture) + 'Z'
    Set-Reg $script:UXKey 'PauseUpdatesStartTime' $s 'String'
    Set-Reg $script:UXKey 'PauseUpdatesExpiryTime' $e 'String'
    Set-Reg $script:UXKey 'PauseFeatureUpdatesStartTime' $s 'String'
    Set-Reg $script:UXKey 'PauseFeatureUpdatesEndTime' $e 'String'
    Set-Reg $script:UXKey 'PauseQualityUpdatesStartTime' $s 'String'
    Set-Reg $script:UXKey 'PauseQualityUpdatesEndTime' $e 'String'
    Update-PauseState
    Log "Windows updates paused for $days days (until $($end.ToLocalTime().ToString('yyyy-MM-dd')))." 'INFO' 'Update'
} 'Pausing updates...' })
$btnResume.Add_Click({ Start-Work {
    foreach ($n in $script:PauseNames) { Remove-Reg $script:UXKey $n }
    Update-PauseState
    Log 'Windows updates resumed.' 'INFO' 'Update'
} 'Resuming updates...' })
Update-PauseState


