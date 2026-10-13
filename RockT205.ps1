$chkHideConsole.Add_Click({
    $script:Settings['HideConsole'] = [bool]$chkHideConsole.IsChecked
    Set-ConsoleVisible (-not [bool]$chkHideConsole.IsChecked)
    Save-Settings
})
