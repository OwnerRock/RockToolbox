foreach ($t in $Toggles) {
    if ($t.Group -and $t.Group -ne $lastGroup) {
        $gh = New-Object System.Windows.Controls.TextBlock
        $gh.Style = $window.FindResource('H'); $gh.Text = $t.Group
        [void]$customizePanel.Children.Add($gh)
        $lastGroup = $t.Group
    }
    if ($t.Choice) {
        $row = New-Object System.Windows.Controls.StackPanel
        $row.Orientation = 'Horizontal'; $row.Margin = '4,6,4,6'
        $lbl = New-Object System.Windows.Controls.TextBlock
        $lbl.Text = $t.Name; $lbl.VerticalAlignment = 'Center'; $lbl.Margin = '0,0,16,0'
        Set-Res $lbl 'Foreground' '#E6E8F2'
        $cmb = New-Object System.Windows.Controls.ComboBox
        $cmb.Width = 220; $cmb.Height = 28; $cmb.Foreground = Get-Brush '#000000'
        $cmb.VerticalContentAlignment = 'Center'; $cmb.ToolTip = $t.Desc
        foreach ($k in $t.Choice.Options.Keys) { [void]$cmb.Items.Add($k) }
        $cur = $null
        try { $cur = (Get-ItemProperty -LiteralPath $t.Choice.P -Name $t.Choice.N -ErrorAction Stop).($t.Choice.N) } catch { $cur = $null }
        if ($null -eq $cur) { $cur = $t.Choice.Def }
        $pick = $null
        foreach ($k in $t.Choice.Options.Keys) { if ("$($t.Choice.Options[$k])" -eq "$cur") { $pick = $k } }
        if ($pick) { $cmb.SelectedItem = $pick } else { $cmb.SelectedIndex = 0 }
        $cmb.Tag = $t
        $cmb.Add_SelectionChanged({
            param($s, $e)
            if (-not $s.SelectedItem) { return }
            $tg = $s.Tag
            try {
                Set-Reg $tg.Choice.P $tg.Choice.N ([int]$tg.Choice.Options[[string]$s.SelectedItem])
                Log ($tg.Name + ': ' + [string]$s.SelectedItem) 'INFO' 'Customize'
            } catch { Log "Failed: $($_.Exception.Message)" 'ERROR' 'Customize' }
        })
        [void]$row.Children.Add($lbl); [void]$row.Children.Add($cmb)
        [void]$customizePanel.Children.Add($row)
        continue
    }
    $cb = New-Object System.Windows.Controls.CheckBox
    $cb.Style = $window.FindResource('Switch')
    $cb.Content = $t.Name
    $cb.ToolTip = $t.Desc
    $cb.Tag = $t
    $cb.IsChecked = (Get-ToggleState $t)
    $cb.Add_Click({
        param($s, $e)
        $tg = $s.Tag
        try {
            Set-ToggleState $tg ([bool]$s.IsChecked)
            $st = 'OFF'; if ($s.IsChecked) { $st = 'ON' }
            Log ($tg.Name + ': ' + $st) 'INFO' 'Customize'
        } catch {
            Log "Failed: $($_.Exception.Message)" 'ERROR' 'Customize'
            $s.IsChecked = (Get-ToggleState $tg)
        }
    })
    [void]$customizePanel.Children.Add($cb)
}
