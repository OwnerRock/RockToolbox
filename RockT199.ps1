$cmbBgStyle.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbBgStyle.SelectedItem) { return }
    $script:Settings['Background'] = [string]$cmbBgStyle.SelectedItem
    $script:Settings['BgColor'] = ''
    Apply-Theme; Sync-SettingsUi; Save-Settings
})
