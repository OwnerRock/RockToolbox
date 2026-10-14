# Module: core/Window - Loads the XAML (ui/*), builds the window, logo
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# XAML - the window is split into ui\MainWindow.xaml + ui\Styles.xaml + ui\pages\*.xaml.
# Every  <!-- @include path -->  line is replaced by the content of that file.
# ------------------------------------------------------------------
function Get-XamlSource {
    param([string]$Relative)
    $path = Join-Path $script:RockRoot $Relative
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing UI file: $Relative (keep the ui folder next to RockToolbox.ps1)" }
    $txt = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $eval = [System.Text.RegularExpressions.MatchEvaluator]{ param($m) Get-XamlSource $m.Groups[1].Value }
    return [regex]::Replace($txt, '[ \t]*<!--\s*@include\s+(\S+)\s*-->', $eval)
}
$xamlText = Get-XamlSource 'ui/MainWindow.xaml'


$xamlText = Convert-XamlTheme -Text $xamlText -Inject
if (Test-Arabic) { $xamlText = Convert-XamlLang $xamlText }
[xml]$xaml = $xamlText
$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)

foreach ($n in $xaml.SelectNodes('//*[@*[local-name()="Name"]]')) {
    $name = $n.GetAttribute('Name', 'http://schemas.microsoft.com/winfx/2006/xaml')
    if ($name) { Set-Variable -Name $name -Value $window.FindName($name) -Scope Script }
}

if (Test-Arabic) { $rootGrid.FlowDirection = 'RightToLeft' }


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

