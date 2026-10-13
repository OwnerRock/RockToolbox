$cmbScrollSpeed.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbScrollSpeed.SelectedItem) { return }
    $script:Settings['ScrollSpeed'] = [string]$cmbScrollSpeed.SelectedItem
    Apply-Scroll; Save-Settings
})
