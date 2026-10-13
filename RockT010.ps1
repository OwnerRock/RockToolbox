function Pump-Wait {
    param([int]$Ms)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $Ms) { DoEvents; Start-Sleep -Milliseconds 16 }
}

# progress bar at the bottom of the window ( -1 = indeterminate )
function Set-Progress {
    param([double]$Pct = -1, [string]$Text)
    if ($Text) { $txtStatus.Text = $Text }
    if ($Pct -lt 0) {
        $pbMain.IsIndeterminate = $true
        $txtPct.Text = ''
    } else {
        if ($Pct -gt 100) { $Pct = 100 }
        $pbMain.IsIndeterminate = $false
        Set-PbValue $Pct
        $txtPct.Text = ('{0:0}%' -f $Pct)
    }
    DoEvents
}

# every message goes to the Rock console; the last one is also shown next to the progress bar
function Log {
    param([string]$msg, [string]$level = 'INFO', [string]$tag = 'UI')
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $color = 'Gray'
    if ($level -eq 'ERROR') { $color = 'Red' } elseif ($level -eq 'WARN') { $color = 'Yellow' }
    Write-Host ('[' + $ts + '] [' + $level + '] [' + $tag + '] ' + $msg) -ForegroundColor $color
    if ($txtStatus -and -not $msg.StartsWith(' ')) {
        $txtStatus.Text = $msg.Trim()
        try { $txtStatus.BeginAnimation([System.Windows.UIElement]::OpacityProperty, (Get-FlashAnim)) } catch { }
        if ($level -eq 'ERROR') { Set-Res $txtStatus 'Foreground' '#FF6B6B' }
        elseif ($level -eq 'WARN') { Set-Res $txtStatus 'Foreground' '#FFB454' }
        else { Set-Res $txtStatus 'Foreground' '#C9CDE0' }
    }
    DoEvents
}

$script:Busy = $false
