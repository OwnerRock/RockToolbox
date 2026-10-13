# ------------------------------------------------------------------
function New-RawBrush([string]$hex) { return (New-Object System.Windows.Media.BrushConverter).ConvertFromString($hex) }
# theme-aware: canonical dark colors are mapped through the active palette
function Get-Brush([string]$hex) {
    $k = $hex.TrimStart('#').ToUpper()
    if ($script:Pal -and $k.Length -eq 6 -and $script:Pal.ContainsKey($k)) { $hex = $script:Pal[$k] }
    return (New-RawBrush $hex)
}
# binds a brush property to a theme resource so it follows live theme changes
function Set-Res {
    param($El, [string]$Prop, [string]$Hex)
    $k = $Hex.TrimStart('#').ToUpper()
    if ($script:Pal -and $script:Pal.ContainsKey($k)) {
        $t = $El.GetType(); $dp = $null
        while ($t -and -not $dp) {
            $f = $t.GetField($Prop + 'Property', [System.Reflection.BindingFlags]'Public,Static,DeclaredOnly')
            if ($f) { $dp = $f.GetValue($null) } else { $t = $t.BaseType }
        }
        if ($dp) { $El.SetResourceReference($dp, 'K_' + $k); return }
    }
    $El.$Prop = Get-Brush $Hex
}

function DoEvents {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    [void][System.Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke(
        [System.Windows.Threading.DispatcherPriority]::Background,
        [System.Windows.Threading.DispatcherOperationCallback]{ param($f) $f.Continue = $false; return $null },
        $frame)
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}

# ---- animations
