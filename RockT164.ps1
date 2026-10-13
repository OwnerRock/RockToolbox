function Update-DrvCount {
    $all = @(Get-DrvItems $lvDrvInstall)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtDrvCount.Text = "Selected: $n of $($all.Count)"
}
