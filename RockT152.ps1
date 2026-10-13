function Update-UnView {
    $q = $txtUnQuery.Text.Trim()
    $t = [string]$cmbUnType.SelectedItem
    $view = New-Object 'System.Collections.Generic.List[RockInst]'
    foreach ($a in $script:UnAll) {
        if ($t -eq 'Desktop apps' -and $a.Source -ne 'Desktop') { continue }
        if ($t -eq 'Store apps' -and $a.Source -ne 'Store') { continue }
        if ($q -and $a.Name -notlike "*$q*" -and $a.Publisher -notlike "*$q*") { continue }
        $view.Add($a)
    }
    $lvUnApps.ItemsSource = $view
    Update-UnCount
    $txtUnInfo.Text = "$($view.Count) of $($script:UnAll.Count) apps shown. Tick what you want to remove, then press Uninstall selected."
}

function Scan-UnApps {
    Start-Work {
        Log 'Listing installed apps...' 'INFO' 'Uninstall'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Reading installed apps...'
        $rows = Invoke-BgJob $script:AppsScanScript @() $st
        $script:UnAll.Clear()
        foreach ($r in @($rows | Sort-Object { $_.Name })) {
            if (-not $r) { continue }
            $o = New-Object RockInst
            $o.Name = [string]$r.Name; $o.Version = [string]$r.Version; $o.Publisher = [string]$r.Publisher
            $o.Source = [string]$r.Source; $o.Size = [string]$r.Size; $o.Key = [string]$r.Key
            $o.Cmd = [string]$r.Cmd; $o.Quiet = [string]$r.Quiet
            $script:UnAll.Add($o)
        }
        $script:UnLoaded = $true
        Update-UnView
        Log "$($script:UnAll.Count) apps found." 'INFO' 'Uninstall'
    } 'Reading installed apps...'
}

function Uninstall-UnApps {
    $sel = @($script:UnAll | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the apps you want to remove first.' 'WARN' 'Uninstall'; return }
    $names = ($sel | Select-Object -First 8 | ForEach-Object { '- ' + $_.Name }) -join "`n"
    if ($sel.Count -gt 8) { $names += "`n... and $($sel.Count - 8) more" }
    $r = [System.Windows.MessageBox]::Show("Uninstall $($sel.Count) app(s)?`n`n$names", 'Rock Toolbox', 'YesNo', 'Warning')
    if ([string]$r -ne 'Yes') { return }
    $items = @($sel | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Source = $_.Source; Key = $_.Key; Cmd = $_.Cmd; Quiet = $_.Quiet } })
    Start-Work {
        $st = New-WUState
        $st.Pct = 0.0
        $st.Msg = 'Uninstalling apps...'
        [void](Invoke-BgJob $script:AppsUninstallScript @(, $items) $st)
        Log ("Finished: $($st.Ok) removed, $($st.Fail) failed.") 'INFO' 'Uninstall'
    } 'Uninstalling apps...'
    Scan-UnApps
}

foreach ($t in 'All apps', 'Desktop apps', 'Store apps') { [void]$cmbUnType.Items.Add($t) }
