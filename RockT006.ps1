[xml]$xaml = $xamlText
$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)

foreach ($n in $xaml.SelectNodes('//*[@*[local-name()="Name"]]')) {
    $name = $n.GetAttribute('Name', 'http://schemas.microsoft.com/winfx/2006/xaml')
    if ($name) { Set-Variable -Name $name -Value $window.FindName($name) -Scope Script }
}


# ---- logo: faceted "rock crystal" gem (vector) + window / taskbar icon rendered from the same artwork
$logoXaml = @'
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="40" Height="40">
  <Polygon Points="20,2 4,12 20,20"   Fill="#8FEBFF" Stroke="#181B20" StrokeThickness="0.7" StrokeLineJoin="Round"/>
  <Polygon Points="20,2 36,12 20,20"  Fill="#4FD1FF" Stroke="#181B20" StrokeThickness="0.7" StrokeLineJoin="Round"/>
  <Polygon Points="4,12 4,28 20,20"   Fill="#2F7BFF" Stroke="#181B20" StrokeThickness="0.7" StrokeLineJoin="Round"/>
  <Polygon Points="36,12 36,28 20,20" Fill="#7B5CFF" Stroke="#181B20" StrokeThickness="0.7" StrokeLineJoin="Round"/>
  <Polygon Points="4,28 20,38 20,20"  Fill="#4B35C9" Stroke="#181B20" StrokeThickness="0.7" StrokeLineJoin="Round"/>
  <Polygon Points="36,28 20,38 20,20" Fill="#9A6BFF" Stroke="#181B20" StrokeThickness="0.7" StrokeLineJoin="Round"/>
</Canvas>
'@
try {
    $logoHost.Child = [Windows.Markup.XamlReader]::Parse($logoXaml)
    $iconCanvas = [Windows.Markup.XamlReader]::Parse($logoXaml)
    $iconCanvas.Measure((New-Object System.Windows.Size(40, 40)))
    $iconCanvas.Arrange((New-Object System.Windows.Rect(0, 0, 40, 40)))
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(128, 128, 307.2, 307.2, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($iconCanvas)
    $window.Icon = $rtb
} catch { }

# ------------------------------------------------------------------
# Helpers
