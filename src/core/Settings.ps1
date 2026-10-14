# Module: core/Settings - Theme engine + settings.json
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.


# ------------------------------------------------------------------
# Theme engine + settings (settings.json in %LOCALAPPDATA%\RockToolbox)
# ------------------------------------------------------------------
$script:SettingsFile = Join-Path $AppDir 'settings.json'
$script:SettingsDefault = @{
    Theme = 'Dark'; Accent = 'Lime'; AccentHex = '#A3E635'; Background = 'Default'
    Language = 'English'; PkgManager = 'WinGet'
    FontSize = 13; Scale = 100; Topmost = $false; StartMaximized = $false; StatusBar = $true
    HideConsole = $true; RememberTab = $false; StartTab = 'Install'; LastTab = 'Install'
    BgColor = ''; BgImage = ''; BgOpacity = 40; AutoRestore = $true
    ScrollAnim = 'Smooth'; ScrollSpeed = 'Normal'; PageAnim = 'Slide up'; CardAnim = $true; BgBlur = 0
}
$script:Settings = @{}
foreach ($k in $script:SettingsDefault.Keys) { $script:Settings[$k] = $script:SettingsDefault[$k] }

function Read-Settings {
    if (-not (Test-Path -LiteralPath $script:SettingsFile)) { return }
    try {
        $j = Get-Content -LiteralPath $script:SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($k in @($script:SettingsDefault.Keys)) {
            $p = $j.PSObject.Properties[$k]
            if ($p -and $null -ne $p.Value) { $script:Settings[$k] = $p.Value }
        }
    } catch { }
}
function Save-Settings {
    try { $script:Settings | ConvertTo-Json | Set-Content -LiteralPath $script:SettingsFile -Encoding UTF8 } catch { }
}

$script:AccentPresets = [ordered]@{
    'Ocean'='#4FD1FF'; 'Blue'='#4F8CFF'; 'Violet'='#8B7BFF'; 'Purple'='#B36BFF'; 'Pink'='#FF6BC1'; 'Rose'='#FF5C7A'
    'Red'='#FF5555'; 'Orange'='#FF9F43'; 'Amber'='#FFC84A'; 'Lime'='#A3E635'; 'Green'='#3DDC84'; 'Teal'='#2DD4BF'
}
# name = @(hue or -1, saturation factor, lightness factor)
$script:BgVariants = [ordered]@{
    'Default'   = @(-1, 1.0, 1.0)
    'Midnight'  = @(228, 2.4, 0.95)
    'Graphite'  = @(0, 0.0, 1.05)
    'Forest'    = @(150, 1.6, 0.95)
    'Wine'      = @(335, 1.8, 0.98)
    'Deep dark' = @(-1, 0.8, 0.45)
}
$script:SurfKeys   = '1F2329','1B1F24','181B20','23272D','14171B','2B3038','35506A','1E3547','3A3F4B','262B33','0F1116','343A44','12131A'
$script:TextKeys   = 'E6E8F2','C9CDE0','D5D9E6','9AA0B8','6B7186','F2F4FF','8D93AD','F0F4FF'
$script:StatusKeys = '3DDC84','FFB454','FF6B6B','FF4D4D','FF5555','5CE08A'
$script:LightMap = @{
    '1F2329'='#F3F5F9'; '1B1F24'='#FFFFFF'; '181B20'='#E8ECF2'; '23272D'='#FFFFFF'; '14171B'='#FFFFFF'
    '2B3038'='#D5DBE5'; '35506A'='#B7C3D3'; '1E3547'='#E1E7F0'; '3A3F4B'='#B9C1CD'; '262B33'='#DDE3EC'
    '0F1116'='#DCE2EB'; '343A44'='#D5DBE5'; '12131A'='#E6EAF0'
    'E6E8F2'='#1F2733'; 'C9CDE0'='#3B4556'; 'D5D9E6'='#2C3546'; '9AA0B8'='#5A6578'; '6B7186'='#6A7488'
    'F2F4FF'='#111A27'; '8D93AD'='#66718A'; 'F0F4FF'='#1B2733'
    '3DDC84'='#1E8E4E'; 'FFB454'='#B36B00'; 'FF6B6B'='#D32F2F'; 'FF4D4D'='#D32F2F'; 'FF5555'='#D32F2F'; '5CE08A'='#2E9E5B'
}

function ConvertTo-Hsl {
    param([string]$Hex)
    $h = $Hex.TrimStart('#')
    $r = [Convert]::ToInt32($h.Substring(0, 2), 16) / 255.0
    $g = [Convert]::ToInt32($h.Substring(2, 2), 16) / 255.0
    $b = [Convert]::ToInt32($h.Substring(4, 2), 16) / 255.0
    $max = [Math]::Max($r, [Math]::Max($g, $b)); $min = [Math]::Min($r, [Math]::Min($g, $b))
    $l = ($max + $min) / 2.0; $d = $max - $min
    $hh = 0.0; $ss = 0.0
    if ($d -gt 0) {
        $ss = $d / (1 - [Math]::Abs(2 * $l - 1))
        if ($max -eq $r)     { $hh = 60 * ((($g - $b) / $d) % 6) }
        elseif ($max -eq $g) { $hh = 60 * ((($b - $r) / $d) + 2) }
        else                 { $hh = 60 * ((($r - $g) / $d) + 4) }
        if ($hh -lt 0) { $hh += 360 }
    }
    return @($hh, $ss, $l)
}
function ConvertFrom-Hsl {
    param([double]$H, [double]$S, [double]$L)
    $H = (($H % 360) + 360) % 360
    $S = [Math]::Max(0.0, [Math]::Min(1.0, $S)); $L = [Math]::Max(0.0, [Math]::Min(1.0, $L))
    $c = (1 - [Math]::Abs(2 * $L - 1)) * $S
    $x = $c * (1 - [Math]::Abs((($H / 60) % 2) - 1))
    $m = $L - $c / 2
    $r = 0.0; $g = 0.0; $b = 0.0
    switch ([int][Math]::Floor($H / 60)) {
        0 { $r = $c; $g = $x }
        1 { $r = $x; $g = $c }
        2 { $g = $c; $b = $x }
        3 { $g = $x; $b = $c }
        4 { $r = $x; $b = $c }
        default { $r = $c; $b = $x }
    }
    return ('#{0:X2}{1:X2}{2:X2}' -f [int][Math]::Round(($r + $m) * 255), [int][Math]::Round(($g + $m) * 255), [int][Math]::Round(($b + $m) * 255))
}

function Test-BgImage {
    $p = [string]$script:Settings['BgImage']
    return [bool]($p -and (Test-Path -LiteralPath $p))
}

function Get-CurrentAccentHex {
    $name = [string]$script:Settings['Accent']
    if ($name -eq 'Ocean') { return '#4FD1FF' }
    if ($script:AccentPresets.Contains($name)) { return [string]$script:AccentPresets[$name] }
    $x = [string]$script:Settings['AccentHex']
    if ($x -match '^#[0-9A-Fa-f]{6}$') { return $x.ToUpper() }
    return '#4FD1FF'
}

function Get-Palette {
    $light = ($script:Settings['Theme'] -eq 'Light')
    $aHex  = Get-CurrentAccentHex
    $ocean = ($aHex -eq '#4FD1FF')
    $bgc = [string]$script:Settings['BgColor']
    $hasBgc = ($bgc -match '^#[0-9A-Fa-f]{6}$')
    $h = (ConvertTo-Hsl $aHex)[0]
    if ($ocean) { $h = 211 }
    $pal = @{}
    if ($light) {
        foreach ($k in ($script:SurfKeys + $script:TextKeys + $script:StatusKeys)) { $pal[$k] = $script:LightMap[$k] }
        $pal['WHITE'] = '#111827'
        if ($hasBgc) { $pal['1F2329'] = $bgc.ToUpper() }
        if (-not $ocean) {
            $pal['1E3547'] = ConvertFrom-Hsl $h 0.30 0.92
            $pal['35506A'] = ConvertFrom-Hsl $h 0.25 0.78
        }
        $pal['4FD1FF'] = ConvertFrom-Hsl $h 0.85 0.38
        $pal['2F6FB0'] = ConvertFrom-Hsl $h 0.57 0.44
        $pal['4A78B0'] = ConvertFrom-Hsl $h 0.45 0.46
        $pal['2F8BFF'] = ConvertFrom-Hsl $h 0.90 0.46
        $pal['4F8CFF'] = $pal['2F8BFF']
        $pal['1E3A4C'] = ConvertFrom-Hsl $h 0.70 0.92
    } else {
        $v = $script:BgVariants[[string]$script:Settings['Background']]
        if (-not $v) { $v = $script:BgVariants['Default'] }
        if ($hasBgc) {
            $c = ConvertTo-Hsl $bgc
            $v = @($c[0], [Math]::Max(0.0, [Math]::Min(4.0, $c[1] / 0.17)), [Math]::Max(0.3, [Math]::Min(3.0, $c[2] / 0.15)))
        }
        foreach ($k in $script:SurfKeys) {
            $pal[$k] = '#' + $k
            if ($v[0] -ge 0 -or $v[1] -ne 1.0 -or $v[2] -ne 1.0) {
                $x = ConvertTo-Hsl ('#' + $k)
                $hh = $x[0]; $ss = [Math]::Min(1.0, $x[1] * $v[1])
                if ($v[0] -ge 0) { $hh = $v[0]; $ss = [Math]::Min(0.55, $x[1] * $v[1]) }
                $pal[$k] = ConvertFrom-Hsl $hh $ss ([Math]::Min(0.95, $x[2] * $v[2]))
            }
        }
        foreach ($k in ($script:TextKeys + $script:StatusKeys)) { $pal[$k] = '#' + $k }
        $pal['WHITE'] = '#FFFFFF'
        if ($ocean) {
            $pal['4FD1FF'] = '#4FD1FF'; $pal['2F6FB0'] = '#2F6FB0'; $pal['4A78B0'] = '#4A78B0'
            $pal['2F8BFF'] = '#2F8BFF'; $pal['4F8CFF'] = '#4F8CFF'; $pal['1E3A4C'] = '#1E3A4C'
        } else {
            $pal['4FD1FF'] = $aHex
            $pal['2F6FB0'] = ConvertFrom-Hsl $h 0.57 0.44
            $pal['4A78B0'] = ConvertFrom-Hsl $h 0.40 0.49
            $pal['2F8BFF'] = ConvertFrom-Hsl $h 1.00 0.59
            $pal['4F8CFF'] = $pal['2F8BFF']
            $pal['1E3A4C'] = ConvertFrom-Hsl $h 0.38 0.22
            $pal['1E3547'] = ConvertFrom-Hsl $h 0.35 0.21
            $pal['35506A'] = ConvertFrom-Hsl $h 0.33 0.32
        }
    }
    if (Test-BgImage) {
        foreach ($k in '1B1F24', '23272D', '181B20') { $pal[$k] = '#B0' + $pal[$k].TrimStart('#') }
    }
    return $pal
}

# Rewrites the hex colors of a XAML string through the current palette.
#   default  : colors become {DynamicResource K_xxxxxx} (live theme switching) ; -Inject adds the brush resources
#   -Literal : colors are replaced by the current palette values (used for the About window)
function Convert-XamlTheme {
    param([string]$Text, [switch]$Literal, [switch]$Inject)
    $script:ConvLiteral = [bool]$Literal
    if (-not $Literal) {
        $Text = [regex]::Replace($Text, '<Button\b[^>]*>', [System.Text.RegularExpressions.MatchEvaluator]{
            param($m)
            $t = $m.Value
            if ($t -match 'Background="#(2F6FB0|B0343A)"' -and $t -notmatch 'Foreground=') { $t = '<Button Foreground="#FFFFFF"' + $t.Substring(7) }
            $t
        })
    }
    $Text = [regex]::Replace($Text, '="#([0-9A-Fa-f]{6})"', [System.Text.RegularExpressions.MatchEvaluator]{
        param($m)
        $k = $m.Groups[1].Value.ToUpper()
        if ($script:Pal.ContainsKey($k)) {
            if ($script:ConvLiteral) { '="' + $script:Pal[$k] + '"' } else { '="{DynamicResource K_' + $k + '}"' }
        } else { $m.Value }
    })
    $Text = [regex]::Replace($Text, '(Foreground|CaretBrush)="White"', [System.Text.RegularExpressions.MatchEvaluator]{
        param($m)
        if ($script:ConvLiteral) { $m.Groups[1].Value + '="' + $script:Pal['WHITE'] + '"' } else { $m.Groups[1].Value + '="{DynamicResource K_WHITE}"' }
    })
    if ($Inject) {
        $sb = New-Object System.Text.StringBuilder
        foreach ($k in $script:Pal.Keys) { [void]$sb.AppendLine('    <SolidColorBrush x:Key="K_' + $k + '" Color="' + $script:Pal[$k] + '"/>') }
        $Text = $Text.Replace('<Window.Resources>', "<Window.Resources>`n" + $sb.ToString())
    }
    return $Text
}

Read-Settings
$script:Pal = Get-Palette

