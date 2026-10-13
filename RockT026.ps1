$btnUninstall.Add_Click({ Start-Work {
    $ids = @($script:Selected.Keys)
    if ($ids.Count -eq 0) { Log 'No apps selected.' 'WARN' 'Winget'; return }
    Invoke-Winget 'uninstall' $ids
    Log 'Uninstall queue finished.' 'INFO' 'Winget'
} })
$btnUpgrade.Add_Click({ Start-Work {
    $wg = Get-Winget
    if (-not $wg) { Log 'winget not found.' 'WARN' 'Winget'; return }
    Log 'Upgrading all packages...' 'INFO' 'Winget'
    $code = Invoke-External $wg 'upgrade --all --silent --accept-source-agreements --accept-package-agreements'
    Log "Upgrade finished (exit code $code)." 'INFO' 'Winget'
} })
$btnStopAction.Add_Click({
    if (-not $script:Busy) {
        Log 'No action is running right now.' 'WARN' 'UI'
        return
    }
    $script:CancelRequested = $true
    Log 'Stop requested — cancelling current action...' 'WARN' 'UI'
})
