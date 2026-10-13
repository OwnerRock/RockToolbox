$cmbStartTab.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbStartTab.SelectedItem) { return }
    $script:Settings['StartTab'] = [string]$cmbStartTab.SelectedItem
    Save-Settings
})

# ---- taskbar pin (checkbox reflects the real state)
