$script:CardAnimOn = $false
function Initialize-CardAnim {
    param($Card)
    $tg = New-Object System.Windows.Media.TransformGroup
    [void]$tg.Children.Add((New-Object System.Windows.Media.ScaleTransform(1.0, 1.0)))
    [void]$tg.Children.Add((New-Object System.Windows.Media.TranslateTransform(0.0, 0.0)))
    $Card.RenderTransform = $tg
    $Card.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
}
function Animate-Card {
    param($Card, [double]$Scale, [double]$Y, [int]$Ms = 130, [string]$Ease = 'cubic')
    if (-not [bool]$script:Settings['CardAnim']) { return }
    $tg = $Card.RenderTransform
    if ($tg -isnot [System.Windows.Media.TransformGroup]) { return }
    if ($Ease -eq 'back') { $e = New-Object System.Windows.Media.Animation.BackEase; $e.Amplitude = 0.7 }
    else { $e = New-Object System.Windows.Media.Animation.CubicEase }
    $e.EasingMode = 'EaseOut'
    $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds($Ms))
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation
    $a.To = $Scale; $a.Duration = $dur; $a.EasingFunction = $e
    $tg.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $a)
    $tg.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $a)
    $b = New-Object System.Windows.Media.Animation.DoubleAnimation
    $b.To = $Y; $b.Duration = $dur; $b.EasingFunction = $e
    $tg.Children[1].BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $b)
}
