foreach ($grp in 'Essential', 'Advanced') {
    $h = New-Object System.Windows.Controls.TextBlock
    $h.Style = $window.FindResource('H')
    if ($grp -eq 'Essential') { $h.Text = 'Essential Tweaks' }
    else { $h.Text = 'Advanced Tweaks - CAUTION'; Set-Res $h 'Foreground' '#FFB454' }
    [void]$tweaksPanel.Children.Add($h)
    foreach ($t in @($Tweaks | Where-Object { $_.Group -eq $grp })) {
        $cb = New-Object System.Windows.Controls.CheckBox
        $cb.Content = $t.Name
        $cb.ToolTip = $t.Desc
        $cb.Tag = $t
        [void]$tweaksPanel.Children.Add($cb)
        [void]$tweakBoxes.Add($cb)
    }
}

$btnTweakApply.Add_Click({ Start-Work {
    $sel = @($tweakBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'Nothing selected.' 'WARN' 'Tweaks'; return }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $t = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Applying: ' + $t.Name + '  (' + ($i + 1) + '/' + $n + ')')
        Log "Applying: $($t.Name)  ($($i + 1)/$n)" 'INFO' 'Tweaks'
        try { Invoke-TweakAction $t 'Apply'; Log '  done' 'INFO' 'Tweaks' } catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; Log "  failed: $($_.Exception.Message)" 'ERROR' 'Tweaks' }
        $i++
    }
    $script:SegStart = -1.0
    Log "Tweaks finished ($n). A restart may be needed for some of them." 'INFO' 'Tweaks'
} 'Applying tweaks...' })
