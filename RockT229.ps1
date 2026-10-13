$btnBgImgClear.Add_Click({
    try { Get-ChildItem -LiteralPath $AppDir -Filter 'background*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue } catch { }
    $script:Settings['BgImage'] = ''
    Apply-Background; Apply-Theme; Sync-SettingsUi; Save-Settings
    Log 'Background image removed.' 'INFO' 'Settings'
})
