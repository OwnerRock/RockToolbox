$btnFeatures.Add_Click({ Start-Work {
    $sel = @($featureBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'No features selected.' 'WARN' 'Features'; return }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $f = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Enabling: ' + $f.Name + '  (' + ($i + 1) + '/' + $n + ')')
        Log "Enabling: $($f.Name)" 'INFO' 'Features'
        if ($f.Cmd) {
            $code = Invoke-External $f.Cmd.File $f.Cmd.Arg
            Log "  exit code $code" 'INFO' 'Features'
        } else {
            foreach ($nm in $f.Features) {
                if ($script:CancelRequested) { throw 'Cancelled by user' }
                $code = Invoke-External 'dism.exe' "/Online /Enable-Feature /FeatureName:$nm /All /NoRestart /Quiet"
                Log "  $nm -> exit code $code (3010 = restart required)" 'INFO' 'Features'
            }
        }
        $i++
    }
    $script:SegStart = -1.0
    Log 'Done. Restart Windows to finish enabling features.' 'INFO' 'Features'
} 'Enabling features...' })

foreach ($f in $Fixes) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $f.Name; $b.Tag = $f
    $b.Add_Click({
        param($s, $e)
        $act = $s.Tag.Action
        Start-Work { & $act }
    })
    [void]$fixesPanel.Children.Add($b)
}
