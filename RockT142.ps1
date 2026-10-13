function Set-WUAuto([bool]$Enable) {
    try {
        if (-not (Test-Path -LiteralPath $script:AUKey)) { New-Item -Path $script:AUKey -Force | Out-Null }
        if ($Enable) {
            New-ItemProperty -LiteralPath $script:AUKey -Name NoAutoUpdate -Value 0 -PropertyType DWord -Force | Out-Null
            Remove-ItemProperty -LiteralPath $script:AUKey -Name AUOptions -ErrorAction SilentlyContinue
            Log 'Automatic Windows updates enabled.' 'INFO' 'WinUpdate'
        } else {
            New-ItemProperty -LiteralPath $script:AUKey -Name NoAutoUpdate -Value 1 -PropertyType DWord -Force | Out-Null
            New-ItemProperty -LiteralPath $script:AUKey -Name AUOptions -Value 2 -PropertyType DWord -Force | Out-Null
            Log 'Automatic Windows updates disabled. You can still install updates from this page.' 'INFO' 'WinUpdate'
        }
    } catch { Log ('Could not change the setting: ' + $_.Exception.Message) 'ERROR' 'WinUpdate' }
    Update-WUAutoText
}
