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

# ------------------------------------------------------------------
# XAML
