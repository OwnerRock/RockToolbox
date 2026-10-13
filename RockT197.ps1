$btnAccentApply.Add_Click({
    $x = $txtAccentHex.Text.Trim()
    if ($x -notmatch '^#?[0-9A-Fa-f]{6}$') { Log 'Enter a color like #FF6B6B.' 'WARN' 'Settings'; return }
    if (-not $x.StartsWith('#')) { $x = '#' + $x }
    $script:Settings['Accent'] = 'Custom'; $script:Settings['AccentHex'] = $x.ToUpper()
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log ('Accent color: ' + $x.ToUpper()) 'INFO' 'Settings'
})
