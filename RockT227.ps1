$btnBgColorClear.Add_Click({
    $script:Settings['BgColor'] = ''
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log 'Custom background color cleared.' 'INFO' 'Settings'
})
