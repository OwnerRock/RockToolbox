foreach ($c in 'Nature', 'Space', 'Anime', 'Cars', 'Minimal', 'Dark', 'Cyberpunk', 'City', 'Abstract', 'Gaming', 'Mountains', 'Ocean', 'Sunset', 'Forest') {
    $cb = New-Object System.Windows.Controls.Button
    $cb.Content = $c; $cb.Tag = $c; $cb.Padding = '12,4'; $cb.Margin = '3,2'
    $cb.Add_Click({ param($s, $e) $txtWpQuery.Text = [string]$s.Tag; Start-WpSearch 'search' ([string]$s.Tag) '' $false })
    [void]$wpChips.Children.Add($cb)
}

$btnWpSet.Add_Click({
    if (-not $script:wpSel) { Log 'Select a wallpaper first.'; return }
    Apply-WpItem $script:wpSel.Tag
})
