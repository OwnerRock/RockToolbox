foreach ($cat in $BloatCats) {
    if ($leftCats -contains $cat) { $col = $debloatColL } else { $col = $debloatColR }
    $h = New-Object System.Windows.Controls.TextBlock
    $h.Text = $cat
    $hs = $window.TryFindResource('H')
    if ($hs) { $h.Style = $hs }
    $h.Margin = '4,14,0,4'
    [void]$col.Children.Add($h)
    foreach ($b in @($Bloat | Where-Object { $_.Cat -eq $cat })) {
        $cb = New-Object System.Windows.Controls.CheckBox
        $cb.Content = New-Label $b.Label $b.Name
        $cb.Tag = $b
        [void]$col.Children.Add($cb)
        [void]$bloatBoxes.Add($cb)
    }
}
