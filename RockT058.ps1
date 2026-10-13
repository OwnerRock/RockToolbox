foreach ($p in $LegacyPanels) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $p.Name; $b.Tag = $p
    $b.Add_Click({
        param($s, $e)
        $pp = $s.Tag
        try {
            if ($pp.Arg) { Start-Process $pp.File -ArgumentList $pp.Arg } else { Start-Process $pp.File }
        } catch { Log "Could not open $($pp.Name): $($_.Exception.Message)" 'ERROR' 'Config' }
    })
    [void]$panelsPanel.Children.Add($b)
}
