$btnWpBoth.Add_Click({
    if (-not $script:wpSel) { Log 'Select a wallpaper first.' 'WARN' 'Wallpaper'; return }
    Apply-WpItem $script:wpSel.Tag 'Both'
})
