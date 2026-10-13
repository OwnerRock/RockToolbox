$btnUpgRun.Add_Click({
    $ids = @(@(Get-UpgItems) | Where-Object { $_.Selected } | ForEach-Object { $_.Id })
    Invoke-UpgradeIds $ids
})
