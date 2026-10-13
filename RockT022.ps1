function Start-CardFade {
    param($Card, [int]$DelayMs = 0)
    if (-not [bool]$script:Settings['CardAnim']) { return }
    try {
        $e = New-Object System.Windows.Media.Animation.CubicEase
        $e.EasingMode = 'EaseOut'
        $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(280))
        $begin = [TimeSpan]::FromMilliseconds($DelayMs)
        $Card.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $null)
        $Card.Opacity = 0.0
        $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
        $fade.BeginTime = $begin; $fade.EasingFunction = $e
        $Card.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
        $tg = $Card.RenderTransform
        if ($tg -is [System.Windows.Media.TransformGroup]) {
            $slide = New-Object System.Windows.Media.Animation.DoubleAnimation(14.0, 0.0, $dur)
            $slide.BeginTime = $begin; $slide.EasingFunction = $e
            $tg.Children[1].BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $slide)
        }
    } catch { }
}
function Start-CardsIntro {
    $i = 0
    foreach ($it in $script:AppCards) {
        if ($it.Card.Visibility -ne 'Visible') { continue }
        Start-CardFade $it.Card (120 + $i * 16)
        $i++
        if ($i -ge 40) { break }
    }
    $script:CardAnimOn = $true
}

# ---- app icons: downloaded in the background (4 at a time), several sources per app, cached on disk ----
$IconDir = Join-Path $AppDir 'Icons'
