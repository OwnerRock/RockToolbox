$wpScroll.Add_SizeChanged({
    if ($script:wpCards.Count -gt 0 -and (Get-WpColCount) -ne $script:wpCols) { Layout-Wp }
})
