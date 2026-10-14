# Module: pages/Debloat - Debloat page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Build UI: Debloat
# ------------------------------------------------------------------
$bloatBoxes = New-Object System.Collections.ArrayList
$leftCats = @('Microsoft Apps', 'Microsoft Ecosystem', 'Utilities & Productivity')
foreach ($cat in $BloatCats) {
    if ($leftCats -contains $cat) { $col = $debloatColL } else { $col = $debloatColR }
    $h = New-Object System.Windows.Controls.TextBlock
    $h.Text = $cat
    $hs = $window.TryFindResource('H')
    if ($hs) { $h.Style = $hs }
    $h.Margin = '4,14,0,4'
    [void]$col.Children.Add($h)
    foreach ($b in @($Bloat | Where-Object { $_.Cat -eq $cat })) {
        $cb = New-Object System.Windows.Controls.CheckBox
        $cb.Content = New-Label $b.Label $b.Name
        $cb.Tag = $b
        [void]$col.Children.Add($cb)
        [void]$bloatBoxes.Add($cb)
    }
}
$btnDebRec.Add_Click({ foreach ($cb in $bloatBoxes) { $cb.IsChecked = [bool]$cb.Tag.Rec } })
$btnDebInst.Add_Click({ Start-Work {
    Log 'Checking which of these apps are installed...'
    $names = @(Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $n = 0
    foreach ($cb in $bloatBoxes) {
        $pat = [string]$cb.Tag.Name
        $hit = (@($names | Where-Object { $_ -like $pat }).Count -gt 0)
        $cb.IsChecked = $hit
        if ($hit) { $n++ }
    }
    Log "Selected $n installed app(s)."
} 'Checking installed apps...' })
$btnDebAll.Add_Click({ foreach ($cb in $bloatBoxes) { $cb.IsChecked = $true } })
$btnDebClear.Add_Click({ foreach ($cb in $bloatBoxes) { $cb.IsChecked = $false } })
$btnDebloat.Add_Click({ Start-Work {
    $sel = @($bloatBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'Nothing selected.'; return }
    if (-not (New-SafetyRestorePoint 'removing apps')) { throw 'Cancelled by user' }
    Log 'Reading provisioned packages...'
    $prov = @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue)
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $b = $cb.Tag
        Set-Progress (100.0 * $i / $n) ('Removing: ' + $b.Label + '  (' + ($i + 1) + '/' + $n + ')')
        $i++
        Log "Removing: $($b.Label)"
        try {
            $pk = @(Get-AppxPackage -AllUsers -Name $b.Name -ErrorAction SilentlyContinue)
            foreach ($p in $pk) { Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction SilentlyContinue }
            foreach ($pp in @($prov | Where-Object { $_.DisplayName -like $b.Name })) {
                Remove-AppxProvisionedPackage -Online -PackageName $pp.PackageName -ErrorAction SilentlyContinue | Out-Null
            }
            if ($pk.Count -eq 0) { Log '  (not installed)' } else { Log '  removed' }
        } catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; Log "  failed: $($_.Exception.Message)" }
    }
    Log 'Debloat finished.'
} 'Removing apps...' })

