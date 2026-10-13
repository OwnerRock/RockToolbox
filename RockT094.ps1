$btnWpSimilar.Add_Click({
    if (-not $script:wpSel) { Log 'Click a picture first, then press Find similar.' 'WARN' 'Wallpaper'; return }
    $it = $script:wpSel.Tag
    if ($it.Src -eq 'Wallhaven' -and $it.Id) { Start-WpSearch 'like' ([string]$it.Id) ([string]$it.Title) $false }
    else {
        $t = [string]$it.Title
        if (-not $t) { $t = $txtWpQuery.Text.Trim() }
        Start-WpSearch 'search' $t $t $false
    }
})
