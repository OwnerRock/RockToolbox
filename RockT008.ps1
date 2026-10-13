function Start-PageAnim {
    param($El, [double]$FromY = 16)
    try {
        if (-not ($El.RenderTransform -is [System.Windows.Media.TranslateTransform]) -or $El.RenderTransform.IsFrozen) {
            $El.RenderTransform = New-Object System.Windows.Media.TranslateTransform
        }
        $ease = New-Object System.Windows.Media.Animation.CubicEase
        $ease.EasingMode = 'EaseOut'
        $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(280))
        $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
        $fade.EasingFunction = $ease
        $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
        $slide = New-Object System.Windows.Media.Animation.DoubleAnimation($FromY, 0.0, $dur)
        $slide.EasingFunction = $ease
        $El.RenderTransform.BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $slide)
    } catch { }
}
function Start-PageTransition {
    param($El)
    $mode = [string]$script:Settings['PageAnim']
    if ($mode -eq 'Off') { return }
    if ($mode -eq 'Fade') { Start-PageAnim $El 0; return }
    if ($mode -eq 'Zoom') {
        try {
            $sc = New-Object System.Windows.Media.ScaleTransform(0.95, 0.95)
            $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
            $El.RenderTransform = $sc
            $ease = New-Object System.Windows.Media.Animation.CubicEase
            $ease.EasingMode = 'EaseOut'
            $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(300))
            $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
            $fade.EasingFunction = $ease
            $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
            $z = New-Object System.Windows.Media.Animation.DoubleAnimation(0.95, 1.0, $dur)
            $z.EasingFunction = $ease
            $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $z)
            $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $z)
        } catch { }
        return
    }
    Start-PageAnim $El 16
}
