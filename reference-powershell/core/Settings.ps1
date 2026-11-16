<<<<<<< HEAD
﻿# ==============================================================================
# MODULE: src\core\Settings.ps1
# ==============================================================================
# Module: core/Settings - Theme engine + settings.json
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.


# ------------------------------------------------------------------
# Theme engine + settings (settings.json in %LOCALAPPDATA%\RockToolbox)
# ------------------------------------------------------------------
$script:SettingsFile = Join-Path $AppDir 'settings.json'
$script:SettingsDefault = @{
    Theme = 'Light'; Accent = 'Ocean'; AccentHex = '#4FD1FF'; Background = 'Default'
    UserName = ''; Avatar = ''; WelcomeSeen = ''; UiStyle = 'Modern'
    Language = 'English'; PkgManager = 'WinGet'
    FontSize = 13; Scale = 100; Topmost = $false; StartMaximized = $false; StatusBar = $true
    HideConsole = $true; RememberTab = $false; StartTab = 'Install'; LastTab = 'Install'
    BgColor = ''; BgImage = ''; BgOpacity = 40; AutoRestore = $true; InstantApply = $false
    ScrollAnim = 'Smooth'; ScrollSpeed = 'Normal'; PageAnim = 'Slide up'; CardAnim = $true; BgBlur = 0
    DlDir = ''; DlMax = 3; DlLimit = 'Unlimited'; DlConn = 16
    UpdLatest = ''; UpdUrl = ''; UpdCheckedAt = ''; AutoUpdateCheck = $true; AutoInstallPkgMgrs = $true
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

# ---------------- Modern look (brand gradient) - the Classic look is the original interface (Settings > Appearance) ----------------
function Test-Modern { return ([string]$script:Settings['UiStyle'] -ne 'Classic') }
# palette keys that become the orange > pink > purple gradient in the modern look (primary buttons, active tab, switches, progress fill)
$script:GradKeys = @('2F6FB0', '4A78B0', '2F8BFF', '4F8CFF', 'FF375F')
function New-ThemeBrush([string]$Key, [string]$Hex) {
    if ((Test-Modern) -and ($script:GradKeys -contains $Key)) {
        $b = New-Object System.Windows.Media.LinearGradientBrush
        $b.StartPoint = New-Object System.Windows.Point(0, 0); $b.EndPoint = New-Object System.Windows.Point(1, 1)
        foreach ($s in @(@('#FF9F0A', 0.0), @('#FF375F', 0.55), @('#BF5AF2', 1.0))) {
            $gs = New-Object System.Windows.Media.GradientStop
            $gs.Color = [System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($s[0]); $gs.Offset = $s[1]
            [void]$b.GradientStops.Add($gs)
        }
        return $b
    }
    return (New-RawBrush $Hex)
}
$script:InstallCartXaml = @'
<Border Grid.Column="1" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="14" Margin="10,0,0,0" Padding="12">
          <DockPanel LastChildFill="True">
            <StackPanel DockPanel.Dock="Bottom" Margin="0,8,0,0">
              <Button x:Name="btnInstall" Style="{StaticResource SideBtn}" Content="Install / Upgrade Apps" Background="#2F6FB0" FontWeight="SemiBold" Margin="3,0,3,3"/>
              <Button x:Name="btnStopAction" Style="{StaticResource SideBtn}" Content="Stop Action" Background="#B0343A" Foreground="#FFEEEE"/>
              <WrapPanel HorizontalAlignment="Center" Margin="0,8,0,0">
                <Button x:Name="btnAppsClear" Style="{StaticResource QuietBtn}" Content="Clear Selection"/>
                <Button x:Name="btnUninstall" Style="{StaticResource QuietBtn}" Content="Uninstall Applications"/>
                <Button x:Name="btnUpgrade" Style="{StaticResource QuietBtn}" Content="Upgrade All Applications"/>
              </WrapPanel>
              <TextBlock x:Name="lnkMore" Text="More options" Foreground="#4FD1FF" FontSize="11.5" Cursor="Hand" HorizontalAlignment="Center" Margin="0,8,0,4"/>
              <StackPanel x:Name="spMore" Visibility="Collapsed">
                <Button x:Name="btnCollapse" Style="{StaticResource SideBtn}" Content="Collapse All Categories"/>
                <Button x:Name="btnExpand" Style="{StaticResource SideBtn}" Content="Expand All Categories"/>
                <Button x:Name="btnShowInstalled" Style="{StaticResource SideBtn}" Content="Show Installed Apps"/>
                <Button x:Name="btnShowFind" Style="{StaticResource SideBtn}" Content="Package ID" ToolTip="Browse every app in Rock Toolbox with its WinGet and Chocolatey IDs"/>
                <Button x:Name="btnShowUpg" Style="{StaticResource SideBtn}" Content="Winget Upgrade" ToolTip="List the apps that have a newer version and update the ones you tick"/>
                <TextBlock x:Name="lblCustomHdr" Style="{StaticResource SideH}" Text="Custom winget ID" Margin="3,8,3,3"/>
                <TextBox x:Name="txtCustomId" Height="26" FontSize="12" Margin="3,1" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"/>
                <Button x:Name="btnInstallCustom" Style="{StaticResource SideBtn}" Content="Install ID"/>
              </StackPanel>
            </StackPanel>
            <StackPanel DockPanel.Dock="Top">
              <TextBlock Text="Your list" FontSize="16" FontWeight="SemiBold" Foreground="#F2F4FF" Margin="4,2,4,2"/>
              <TextBlock x:Name="txtSelCount" Text="Selected Apps: 0" FontSize="12" Foreground="#9AA0B8" Margin="4,0,4,2"/>
              <TextBlock x:Name="txtCartTotal" Text="" FontSize="12" FontFamily="Consolas" Foreground="#4FD1FF" Margin="4,0,4,8"/>
            </StackPanel>
            <ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel x:Name="spCart"/></ScrollViewer>
          </DockPanel>
        </Border>
'@
$script:InstallTopXaml = @'
<StackPanel DockPanel.Dock="Top" Margin="0,0,0,8">
              <DockPanel>
                <Border DockPanel.Dock="Right" Background="#181B20" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="12" Padding="2" Margin="8,0,0,0" VerticalAlignment="Center">
                  <StackPanel Orientation="Horizontal">
                    <RadioButton x:Name="rdPkgWinget" Style="{StaticResource SegRadio}" Content="WinGet" GroupName="pkgMgr" IsChecked="True"/>
                    <RadioButton x:Name="rdPkgChoco" Style="{StaticResource SegRadio}" Content="Chocolatey" GroupName="pkgMgr"/>
                  </StackPanel>
                </Border>
                <TextBox x:Name="txtAppSearch" Height="34" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" ToolTip="Search apps"/>
              </DockPanel>
              <ScrollViewer x:Name="svChips" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled" Margin="0,8,0,0"><WrapPanel x:Name="chipPanel"/></ScrollViewer>
            </StackPanel>
'@
$script:RoundBarXaml = @'
    <Style x:Key="RoundBar" TargetType="ProgressBar">
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ProgressBar">
        <Grid>
          <Border CornerRadius="4" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"/>
          <Border x:Name="PART_Track" Margin="1"><Border x:Name="PART_Indicator" HorizontalAlignment="Left" CornerRadius="3" Background="{TemplateBinding Foreground}"/></Border>
        </Grid>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key="QuietBtn" TargetType="Button">
      <Setter Property="Foreground" Value="#9AA0B8"/><Setter Property="Background" Value="Transparent"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="FontSize" Value="11.5"/><Setter Property="Padding" Value="9,5"/><Setter Property="Focusable" Value="False"/><Setter Property="Margin" Value="1"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center"/></Border>
        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#23272D"/><Setter Property="Foreground" Value="#F2F4FF"/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key="SegRadio" TargetType="RadioButton">
      <Setter Property="Foreground" Value="#9AA0B8"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="12"/><Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
        <Border x:Name="bd" CornerRadius="9" Padding="13,6" Background="Transparent"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
        <ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="bd" Property="Background" Value="#4A78B0"/><Setter Property="Foreground" Value="#FFFFFF"/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
'@
# rounder shapes + gradient accents, applied to the XAML text before the theme conversion
function Convert-XamlModern([string]$Text) {
    # design A: tabs move from a wrapping top strip to a grouped sidebar on the left
    $tabNew = '<Border DockPanel.Dock="Left" Width="186" Background="#181B20" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="14" Padding="8" Margin="0,0,10,0"><ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel x:Name="tabBar"/></ScrollViewer></Border>'
    $Text = [regex]::Replace($Text, '<Border DockPanel\.Dock="Top" Background="#181B20"[^>]*?Padding="8,8,8,2"[^>]*>\s*<WrapPanel x:Name="tabBar" Orientation="Horizontal"/>\s*</Border>', $tabNew)
    $s = $Text.IndexOf('<Style TargetType="Button">')
    if ($s -ge 0) {
        $e = $Text.IndexOf('</Style>', $s)
        if ($e -gt $s) { $Text = $Text.Substring(0, $s) + $Text.Substring($s, $e - $s).Replace('CornerRadius="3"', 'CornerRadius="9"') + $Text.Substring($e) }
    }
    $Text = $Text.Replace('CornerRadius="6" Padding="8,8,8,2"', 'CornerRadius="14" Padding="8,8,8,2"')
    $Text = $Text.Replace('CornerRadius="6" Padding="10">', 'CornerRadius="14" Padding="10">')
    $Text = $Text.Replace('CornerRadius="6" Padding="6">', 'CornerRadius="14" Padding="6">')
    $Text = [regex]::Replace($Text, '(<Border Background="#23272D"[^>]*?)CornerRadius="6"', '${1}CornerRadius="12"')
    $Text = [regex]::Replace($Text, '(<ProgressBar\b[^>]*?)Foreground="#4FD1FF"', '${1}Foreground="#FF375F"')
    $Text = [regex]::Replace($Text, '(x:Name="txtCrumb"[^>]*?)Foreground="#9AA0B8"', '${1}Foreground="#FF375F"')
    $Text = [regex]::Replace($Text, '(FontSize="26" FontWeight="SemiBold" )Foreground="White"', '${1}Foreground="#FF375F"')
    # WPF cannot use a gradient as the text-selection highlight, so that one stays a solid color
    $Text = $Text.Replace('<Setter Property="SelectionBrush" Value="#4A78B0"/>', '<Setter Property="SelectionBrush" Value="#35506A"/>')
    # Install page, design 2: apps on the left, a slim "Your list" panel with the Install button on the right
    $Text = [regex]::Replace($Text, '(<Grid x:Name="pnlInstall">\s*<Grid\.ColumnDefinitions>\s*)<ColumnDefinition Width="240"/>(\s*)<ColumnDefinition Width="\*"/>', '${1}<ColumnDefinition Width="*"/>${2}<ColumnDefinition Width="272"/>')
    $Text = [regex]::Replace($Text, '<Border Grid\.Column="0" Background="#1B1F24"[^>]*?>\s*<ScrollViewer VerticalScrollBarVisibility="Auto">\s*<StackPanel>\s*<TextBlock Style="\{StaticResource SideH\}" Text="Actions"[\s\S]*?Content="Install ID"/>\s*</StackPanel>\s*</ScrollViewer>\s*</Border>', [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $script:InstallCartXaml })
    $Text = [regex]::Replace($Text, '<Grid Grid\.Column="1">(\s*<DockPanel x:Name="pnlInstallApps">)', '<Grid Grid.Column="0">${1}')
    $Text = [regex]::Replace($Text, '<DockPanel DockPanel\.Dock="Top" Margin="0,0,0,8">\s*<TextBox x:Name="txtAppSearch"[\s\S]*?<WrapPanel x:Name="chipPanel"/>\s*</DockPanel>', [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $script:InstallTopXaml })
    $Text = $Text.Replace('<Window.Resources>', '<Window.Resources>' + "`n" + $script:RoundBarXaml)
    return $Text
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
    $pal['FF375F'] = '#FF375F'   # marker key: becomes the brand gradient in the modern look
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
        $mod = Test-Modern
        foreach ($k in $script:Pal.Keys) {
            if ($mod -and ($script:GradKeys -contains $k)) {
                [void]$sb.AppendLine('    <LinearGradientBrush x:Key="K_' + $k + '" StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#FF9F0A" Offset="0"/><GradientStop Color="#FF375F" Offset="0.55"/><GradientStop Color="#BF5AF2" Offset="1"/></LinearGradientBrush>')
            } else {
                [void]$sb.AppendLine('    <SolidColorBrush x:Key="K_' + $k + '" Color="' + $script:Pal[$k] + '"/>')
            }
        }
        $Text = $Text.Replace('<Window.Resources>', "<Window.Resources>`n" + $sb.ToString())
    }
    return $Text
}

Read-Settings
# the Package ID page now lives inside the Install page: keep old saved settings working
foreach ($k in 'StartTab', 'LastTab') { if (@('Winget ID', 'Package ID') -contains [string]$script:Settings[$k]) { $script:Settings[$k] = 'Install' } }
$script:Pal = Get-Palette


=======
﻿# ==============================================================================
# MODULE: src\core\Settings.ps1
# ==============================================================================
# Module: core/Settings - Theme engine + settings.json
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.


# ------------------------------------------------------------------
# Theme engine + settings (settings.json in %LOCALAPPDATA%\RockToolbox)
# ------------------------------------------------------------------
$script:SettingsFile = Join-Path $AppDir 'settings.json'
$script:SettingsDefault = @{
    Theme = 'Light'; Accent = 'Ocean'; AccentHex = '#4FD1FF'; Background = 'Default'
    UserName = ''; Avatar = ''; WelcomeSeen = ''; UiStyle = 'Modern'
    Language = 'English'; PkgManager = 'WinGet'
    FontSize = 13; Scale = 100; Topmost = $false; StartMaximized = $false; StatusBar = $true
    HideConsole = $true; RememberTab = $false; StartTab = 'Install'; LastTab = 'Install'
    BgColor = ''; BgImage = ''; BgOpacity = 40; AutoRestore = $true; InstantApply = $false
    ScrollAnim = 'Smooth'; ScrollSpeed = 'Normal'; PageAnim = 'Slide up'; CardAnim = $true; BgBlur = 0
    DlDir = ''; DlMax = 3; DlLimit = 'Unlimited'; DlConn = 16
    UpdLatest = ''; UpdUrl = ''; UpdCheckedAt = ''; AutoUpdateCheck = $true; AutoInstallPkgMgrs = $true
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

# ---------------- Modern look (brand gradient) - the Classic look is the original interface (Settings > Appearance) ----------------
function Test-Modern { return ([string]$script:Settings['UiStyle'] -ne 'Classic') }
# palette keys that become the orange > pink > purple gradient in the modern look (primary buttons, active tab, switches, progress fill)
$script:GradKeys = @('2F6FB0', '4A78B0', '2F8BFF', '4F8CFF', 'FF375F')
function New-ThemeBrush([string]$Key, [string]$Hex) {
    if ((Test-Modern) -and ($script:GradKeys -contains $Key)) {
        $b = New-Object System.Windows.Media.LinearGradientBrush
        $b.StartPoint = New-Object System.Windows.Point(0, 0); $b.EndPoint = New-Object System.Windows.Point(1, 1)
        foreach ($s in @(@('#FF9F0A', 0.0), @('#FF375F', 0.55), @('#BF5AF2', 1.0))) {
            $gs = New-Object System.Windows.Media.GradientStop
            $gs.Color = [System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($s[0]); $gs.Offset = $s[1]
            [void]$b.GradientStops.Add($gs)
        }
        return $b
    }
    return (New-RawBrush $Hex)
}
$script:InstallCartXaml = @'
<Border Grid.Column="1" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="14" Margin="10,0,0,0" Padding="12">
          <DockPanel LastChildFill="True">
            <StackPanel DockPanel.Dock="Bottom" Margin="0,8,0,0">
              <Button x:Name="btnInstall" Style="{StaticResource SideBtn}" Content="Install / Upgrade Apps" Background="#2F6FB0" FontWeight="SemiBold" Margin="3,0,3,3"/>
              <Button x:Name="btnStopAction" Style="{StaticResource SideBtn}" Content="Stop Action" Background="#B0343A" Foreground="#FFEEEE"/>
              <WrapPanel HorizontalAlignment="Center" Margin="0,8,0,0">
                <Button x:Name="btnAppsClear" Style="{StaticResource QuietBtn}" Content="Clear Selection"/>
                <Button x:Name="btnUninstall" Style="{StaticResource QuietBtn}" Content="Uninstall Applications"/>
                <Button x:Name="btnUpgrade" Style="{StaticResource QuietBtn}" Content="Upgrade All Applications"/>
              </WrapPanel>
              <TextBlock x:Name="lnkMore" Text="More options" Foreground="#4FD1FF" FontSize="11.5" Cursor="Hand" HorizontalAlignment="Center" Margin="0,8,0,4"/>
              <StackPanel x:Name="spMore" Visibility="Collapsed">
                <Button x:Name="btnCollapse" Style="{StaticResource SideBtn}" Content="Collapse All Categories"/>
                <Button x:Name="btnExpand" Style="{StaticResource SideBtn}" Content="Expand All Categories"/>
                <Button x:Name="btnShowInstalled" Style="{StaticResource SideBtn}" Content="Show Installed Apps"/>
                <Button x:Name="btnShowFind" Style="{StaticResource SideBtn}" Content="Package ID" ToolTip="Browse every app in Rock Toolbox with its WinGet and Chocolatey IDs"/>
                <Button x:Name="btnShowUpg" Style="{StaticResource SideBtn}" Content="Winget Upgrade" ToolTip="List the apps that have a newer version and update the ones you tick"/>
                <TextBlock x:Name="lblCustomHdr" Style="{StaticResource SideH}" Text="Custom winget ID" Margin="3,8,3,3"/>
                <TextBox x:Name="txtCustomId" Height="26" FontSize="12" Margin="3,1" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"/>
                <Button x:Name="btnInstallCustom" Style="{StaticResource SideBtn}" Content="Install ID"/>
              </StackPanel>
            </StackPanel>
            <StackPanel DockPanel.Dock="Top">
              <TextBlock Text="Your list" FontSize="16" FontWeight="SemiBold" Foreground="#F2F4FF" Margin="4,2,4,2"/>
              <TextBlock x:Name="txtSelCount" Text="Selected Apps: 0" FontSize="12" Foreground="#9AA0B8" Margin="4,0,4,2"/>
              <TextBlock x:Name="txtCartTotal" Text="" FontSize="12" FontFamily="Consolas" Foreground="#4FD1FF" Margin="4,0,4,8"/>
            </StackPanel>
            <ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel x:Name="spCart"/></ScrollViewer>
          </DockPanel>
        </Border>
'@
$script:InstallTopXaml = @'
<StackPanel DockPanel.Dock="Top" Margin="0,0,0,8">
              <DockPanel>
                <Border DockPanel.Dock="Right" Background="#181B20" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="12" Padding="2" Margin="8,0,0,0" VerticalAlignment="Center">
                  <StackPanel Orientation="Horizontal">
                    <RadioButton x:Name="rdPkgWinget" Style="{StaticResource SegRadio}" Content="WinGet" GroupName="pkgMgr" IsChecked="True"/>
                    <RadioButton x:Name="rdPkgChoco" Style="{StaticResource SegRadio}" Content="Chocolatey" GroupName="pkgMgr"/>
                  </StackPanel>
                </Border>
                <TextBox x:Name="txtAppSearch" Height="34" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" ToolTip="Search apps"/>
              </DockPanel>
              <ScrollViewer x:Name="svChips" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled" Margin="0,8,0,0"><WrapPanel x:Name="chipPanel"/></ScrollViewer>
            </StackPanel>
'@
$script:RoundBarXaml = @'
    <Style x:Key="RoundBar" TargetType="ProgressBar">
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ProgressBar">
        <Grid>
          <Border CornerRadius="4" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"/>
          <Border x:Name="PART_Track" Margin="1"><Border x:Name="PART_Indicator" HorizontalAlignment="Left" CornerRadius="3" Background="{TemplateBinding Foreground}"/></Border>
        </Grid>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key="QuietBtn" TargetType="Button">
      <Setter Property="Foreground" Value="#9AA0B8"/><Setter Property="Background" Value="Transparent"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="FontSize" Value="11.5"/><Setter Property="Padding" Value="9,5"/><Setter Property="Focusable" Value="False"/><Setter Property="Margin" Value="1"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center"/></Border>
        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#23272D"/><Setter Property="Foreground" Value="#F2F4FF"/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key="SegRadio" TargetType="RadioButton">
      <Setter Property="Foreground" Value="#9AA0B8"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="12"/><Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
        <Border x:Name="bd" CornerRadius="9" Padding="13,6" Background="Transparent"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
        <ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="bd" Property="Background" Value="#4A78B0"/><Setter Property="Foreground" Value="#FFFFFF"/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
'@
# rounder shapes + gradient accents, applied to the XAML text before the theme conversion
function Convert-XamlModern([string]$Text) {
    # design A: tabs move from a wrapping top strip to a grouped sidebar on the left
    $tabNew = '<Border DockPanel.Dock="Left" Width="186" Background="#181B20" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="14" Padding="8" Margin="0,0,10,0"><ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel x:Name="tabBar"/></ScrollViewer></Border>'
    $Text = [regex]::Replace($Text, '<Border DockPanel\.Dock="Top" Background="#181B20"[^>]*?Padding="8,8,8,2"[^>]*>\s*<WrapPanel x:Name="tabBar" Orientation="Horizontal"/>\s*</Border>', $tabNew)
    $s = $Text.IndexOf('<Style TargetType="Button">')
    if ($s -ge 0) {
        $e = $Text.IndexOf('</Style>', $s)
        if ($e -gt $s) { $Text = $Text.Substring(0, $s) + $Text.Substring($s, $e - $s).Replace('CornerRadius="3"', 'CornerRadius="9"') + $Text.Substring($e) }
    }
    $Text = $Text.Replace('CornerRadius="6" Padding="8,8,8,2"', 'CornerRadius="14" Padding="8,8,8,2"')
    $Text = $Text.Replace('CornerRadius="6" Padding="10">', 'CornerRadius="14" Padding="10">')
    $Text = $Text.Replace('CornerRadius="6" Padding="6">', 'CornerRadius="14" Padding="6">')
    $Text = [regex]::Replace($Text, '(<Border Background="#23272D"[^>]*?)CornerRadius="6"', '${1}CornerRadius="12"')
    $Text = [regex]::Replace($Text, '(<ProgressBar\b[^>]*?)Foreground="#4FD1FF"', '${1}Foreground="#FF375F"')
    $Text = [regex]::Replace($Text, '(x:Name="txtCrumb"[^>]*?)Foreground="#9AA0B8"', '${1}Foreground="#FF375F"')
    $Text = [regex]::Replace($Text, '(FontSize="26" FontWeight="SemiBold" )Foreground="White"', '${1}Foreground="#FF375F"')
    # WPF cannot use a gradient as the text-selection highlight, so that one stays a solid color
    $Text = $Text.Replace('<Setter Property="SelectionBrush" Value="#4A78B0"/>', '<Setter Property="SelectionBrush" Value="#35506A"/>')
    # Install page, design 2: apps on the left, a slim "Your list" panel with the Install button on the right
    $Text = [regex]::Replace($Text, '(<Grid x:Name="pnlInstall">\s*<Grid\.ColumnDefinitions>\s*)<ColumnDefinition Width="240"/>(\s*)<ColumnDefinition Width="\*"/>', '${1}<ColumnDefinition Width="*"/>${2}<ColumnDefinition Width="272"/>')
    $Text = [regex]::Replace($Text, '<Border Grid\.Column="0" Background="#1B1F24"[^>]*?>\s*<ScrollViewer VerticalScrollBarVisibility="Auto">\s*<StackPanel>\s*<TextBlock Style="\{StaticResource SideH\}" Text="Actions"[\s\S]*?Content="Install ID"/>\s*</StackPanel>\s*</ScrollViewer>\s*</Border>', [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $script:InstallCartXaml })
    $Text = [regex]::Replace($Text, '<Grid Grid\.Column="1">(\s*<DockPanel x:Name="pnlInstallApps">)', '<Grid Grid.Column="0">${1}')
    $Text = [regex]::Replace($Text, '<DockPanel DockPanel\.Dock="Top" Margin="0,0,0,8">\s*<TextBox x:Name="txtAppSearch"[\s\S]*?<WrapPanel x:Name="chipPanel"/>\s*</DockPanel>', [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $script:InstallTopXaml })
    $Text = $Text.Replace('<Window.Resources>', '<Window.Resources>' + "`n" + $script:RoundBarXaml)
    return $Text
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
    $pal['FF375F'] = '#FF375F'   # marker key: becomes the brand gradient in the modern look
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
        $mod = Test-Modern
        foreach ($k in $script:Pal.Keys) {
            if ($mod -and ($script:GradKeys -contains $k)) {
                [void]$sb.AppendLine('    <LinearGradientBrush x:Key="K_' + $k + '" StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#FF9F0A" Offset="0"/><GradientStop Color="#FF375F" Offset="0.55"/><GradientStop Color="#BF5AF2" Offset="1"/></LinearGradientBrush>')
            } else {
                [void]$sb.AppendLine('    <SolidColorBrush x:Key="K_' + $k + '" Color="' + $script:Pal[$k] + '"/>')
            }
        }
        $Text = $Text.Replace('<Window.Resources>', "<Window.Resources>`n" + $sb.ToString())
    }
    return $Text
}

Read-Settings
# the Package ID page now lives inside the Install page: keep old saved settings working
foreach ($k in 'StartTab', 'LastTab') { if (@('Winget ID', 'Package ID') -contains [string]$script:Settings[$k]) { $script:Settings[$k] = 'Install' } }
$script:Pal = Get-Palette


>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
