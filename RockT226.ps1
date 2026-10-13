$btnBgColorApply.Add_Click({
    $x = $txtBgHex.Text.Trim()
    if ($x -notmatch '^#?[0-9A-Fa-f]{6}$') { Log 'Enter a color like #1A2233.' 'WARN' 'Settings'; return }
    if (-not $x.StartsWith('#')) { $x = '#' + $x }
    $script:Settings['BgColor'] = $x.ToUpper()
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log ('Background color: ' + $x.ToUpper()) 'INFO' 'Settings'
})
