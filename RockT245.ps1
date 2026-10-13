if (-not (Get-Winget)) { Log 'winget was not found. Install "App Installer" from the Microsoft Store to use the Install tab.' 'WARN' 'UI' }

$script:IconTimer.Start()
