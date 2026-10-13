$btnDrvInstall.Add_Click({
    $sel = @(@(Get-DrvItems $lvDrvInstall) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the drivers you want to install first.' 'WARN' 'Drivers'; return }
    $ids = @($sel | ForEach-Object { $_.Id })
    Invoke-WUInstall -Ids $ids -Restart $false -Rescan { Scan-DrvInstall }
})
