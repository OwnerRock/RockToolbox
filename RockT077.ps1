$btnDebloat.Add_Click({ Start-Work {
    $sel = @($bloatBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'Nothing selected.'; return }
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

# ------------------------------------------------------------------
# Wallpapers
