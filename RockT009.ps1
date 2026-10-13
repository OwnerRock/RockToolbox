function Get-FlashAnim {
    if (-not $script:FlashAnim) {
        $a = New-Object System.Windows.Media.Animation.DoubleAnimation
        $a.From = 0.25; $a.To = 1.0
        $a.Duration = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(240))
        $a.Freeze()
        $script:FlashAnim = $a
    }
    return $script:FlashAnim
}

# glides the progress bar to a new value (jumps back instantly when the value goes down)
function Set-PbValue {
    param([double]$V, [switch]$Instant)
    $prop = [System.Windows.Controls.Primitives.RangeBase]::ValueProperty
    $cur = [double]$pbMain.Value
    if ($Instant -or $V -lt $cur) {
        $pbMain.BeginAnimation($prop, $null)
        $pbMain.Value = $V
        return
    }
    if ([Math]::Abs($V - $cur) -lt 0.05) { return }
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation
    $a.To = $V
    $a.Duration = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(420))
    $ease = New-Object System.Windows.Media.Animation.CubicEase
    $ease.EasingMode = 'EaseOut'
    $a.EasingFunction = $ease
    $pbMain.BeginAnimation($prop, $a)
}

# waits while keeping the window rendering at ~60 fps (animations stay smooth)
