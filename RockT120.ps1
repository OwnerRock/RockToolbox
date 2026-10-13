$btnUpgAll.Add_Click({
    $ids = @(@(Get-UpgItems) | ForEach-Object { $_.Id })
    Invoke-UpgradeIds $ids
})
