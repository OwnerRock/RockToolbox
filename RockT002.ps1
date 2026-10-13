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
