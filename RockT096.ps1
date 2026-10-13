$wpScroll.Add_ScrollChanged({
    param($s, $e)
    if ($e.VerticalChange -le 0) { return }
    if (-not $script:wpHasMore -or $script:Busy -or $script:wpCards.Count -eq 0) { return }
    if ($s.VerticalOffset -ge ($s.ScrollableHeight - 260)) {
        [void]$window.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::Background, [Action]{
            if (-not $script:Busy -and $script:wpHasMore) { Start-WpSearch $script:wpMode $script:wpQuery $script:wpTitle $true }
        })
    }
})
