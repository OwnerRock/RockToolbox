$btnBgRestore.Add_Click({
    try { Get-ChildItem -LiteralPath $AppDir -Filter 'background*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue } catch { }
    $script:Settings['BgImage'] = ''; $script:Settings['BgColor'] = ''
    $script:Settings['Background'] = 'Default'; $script:Settings['BgOpacity'] = 40; $script:Settings['BgBlur'] = 0
    Apply-Background; Apply-Theme; Sync-SettingsUi; Save-Settings
    Log 'Background restored to default.' 'INFO' 'Settings'
})
