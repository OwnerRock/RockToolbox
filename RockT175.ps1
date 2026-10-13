$btnDrvUninstall.Add_Click({
    $sel = @(@(Get-DrvItems $lvDrvUn) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the drivers you want to remove first.' 'WARN' 'Drivers'; return }
    $names = ($sel | Select-Object -First 8 | ForEach-Object { '- ' + $_.Provider + ' / ' + $_.Title }) -join "`n"
    if ($sel.Count -gt 8) { $names += "`n... and $($sel.Count - 8) more" }
    $r = [System.Windows.MessageBox]::Show("Remove $($sel.Count) driver package(s)?`n`n$names`n`nA removed driver can stop its device from working until you install it again.", 'Rock Toolbox', 'YesNo', 'Warning')
    if ([string]$r -ne 'Yes') { return }
    $infs = @($sel | ForEach-Object { $_.Id })
    Start-Work {
        $st = New-WUState
        $st.Pct = 0.0
        $st.Msg = 'Removing drivers...'
        [void](Invoke-BgJob $script:DrvUninstallScript @(, $infs) $st)
        Log ("Finished: $($st.Ok) removed, $($st.Fail) failed.") 'INFO' 'Drivers'
    } 'Removing drivers...'
    Scan-DrvUninstall
})
