$btnTweakUndo.Add_Click({ Start-Work {
    $sel = @($tweakBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'Nothing selected.' 'WARN' 'Tweaks'; return }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $t = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Undoing: ' + $t.Name + '  (' + ($i + 1) + '/' + $n + ')')
        if (-not $t.Ops -and -not $t.Undo) { Log "No undo available for: $($t.Name)" 'WARN' 'Tweaks'; $i++; continue }
        Log "Undoing: $($t.Name)" 'INFO' 'Tweaks'
        try { Invoke-TweakAction $t 'Undo'; Log '  done' 'INFO' 'Tweaks' } catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; Log "  failed: $($_.Exception.Message)" 'ERROR' 'Tweaks' }
        $i++
    }
    $script:SegStart = -1.0
    Log 'Undo finished.' 'INFO' 'Tweaks'
} 'Undoing tweaks...' })
