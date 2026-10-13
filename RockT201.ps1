$cmbScale.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbScale.SelectedItem) { return }
    $script:Settings['Scale'] = [int](([string]$cmbScale.SelectedItem) -replace '%', '')
    Apply-Settings; Save-Settings
})
