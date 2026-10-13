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
