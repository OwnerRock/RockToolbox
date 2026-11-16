# ==============================================================================
# MODULE: src\pages\Settings.ps1
# ==============================================================================
# Module: pages/Settings - Settings page + gear menu + About
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Settings page + gear menu + About
# ------------------------------------------------------------------
$script:SettingsBusy = $true
$script:PopClosedAt  = [DateTime]::MinValue
$script:FontSizes    = @(12, 13, 14, 16)
$script:TabNames     = @('Install', 'Tweaks', 'Debloat', 'Config', 'Updates', 'Windows Update', 'Uninstall Apps', 'Drivers', 'System Info', 'Cleanup', 'Startup', 'Repair', 'Downloads', 'Wallpapers', 'God Mode', 'Settings')

foreach ($n in $script:BgVariants.Keys) { [void]$cmbBgStyle.Items.Add($n) }
foreach ($t in 'Small (12)', 'Normal (13)', 'Large (14)', 'Extra large (16)') { [void]$cmbFontSize.Items.Add($t) }
foreach ($t in '90%', '100%', '110%', '125%', '150%') { [void]$cmbScale.Items.Add($t) }
foreach ($t in $script:TabNames) { [void]$cmbStartTab.Items.Add($t) }

function Set-ConsoleVisible([bool]$Show) {
    try {
        $h = [WinToolboxNative]::GetConsoleWindow()
        if ($h -ne [IntPtr]::Zero) { $cmd = 0; if ($Show) { $cmd = 5 }; [void][WinToolboxNative]::ShowWindow($h, $cmd) }
    } catch { }
}

function Apply-Settings {
    $window.FontSize = [double]$script:Settings['FontSize']
    $sc = [double]$script:Settings['Scale'] / 100.0
    $rootGrid.LayoutTransform = New-Object System.Windows.Media.ScaleTransform($sc, $sc)
    $window.Topmost = [bool]$script:Settings['Topmost']
    if ([bool]$script:Settings['StatusBar']) { $statusBar.Visibility = 'Visible' } else { $statusBar.Visibility = 'Collapsed' }
}

function Build-Swatches {
    $swatchPanel.Children.Clear()
    $cur = [string]$script:Settings['Accent']
    foreach ($name in $script:AccentPresets.Keys) {
        $b = New-Object System.Windows.Controls.Button
        $b.Width = 34; $b.Height = 34; $b.Margin = '4'; $b.Padding = '0'
        $b.Tag = $name; $b.ToolTip = $name
        $b.Background = New-RawBrush $script:AccentPresets[$name]
        if ($name -eq $cur) { $b.BorderThickness = '3'; Set-Res $b 'BorderBrush' '#E6E8F2' }
        else { $b.BorderThickness = '1'; Set-Res $b 'BorderBrush' '#35506A' }
        $b.Add_Click({ param($s, $e) Set-Accent ([string]$s.Tag) })
        [void]$swatchPanel.Children.Add($b)
    }
}

function Update-TabColors {
    foreach ($k in @($tabButtons.Keys)) {
        if ($k -eq $script:CurPage) {
            Set-Res $tabButtons[$k] 'Background' '#4A78B0'; Set-Res $tabButtons[$k] 'Foreground' '#FFFFFF'
            $tabButtons[$k].FontWeight = 'Bold'
        } else {
            Set-Res $tabButtons[$k] 'Background' '#23272D'; Set-Res $tabButtons[$k] 'Foreground' '#C9CDE0'
            $tabButtons[$k].FontWeight = 'SemiBold'
        }
    }
    try { $txtCrumb.Text = L $script:CurPage } catch { }
}

function Sync-SettingsUi {
    $script:SettingsBusy = $true
    try {
        $isLight = ($script:Settings['Theme'] -eq 'Light')
        $chkLight.IsChecked = $isLight; $chkGearLight.IsChecked = $isLight
        # the label names the mode you switch to: in light mode it says "Dark mode" and vice versa
        if ($isLight) { $themeLbl = L 'Dark mode' } else { $themeLbl = L 'Light mode' }
        $chkLight.Content = $themeLbl; $chkGearLight.Content = $themeLbl
        $cmbBgStyle.IsEnabled = (-not $isLight)
        $bg = [string]$script:Settings['Background']
        if ($cmbBgStyle.Items.Contains($bg)) { $cmbBgStyle.SelectedItem = $bg } else { $cmbBgStyle.SelectedIndex = 0 }
        $fi = [Array]::IndexOf($script:FontSizes, [int]$script:Settings['FontSize']); if ($fi -lt 0) { $fi = 1 }
        $cmbFontSize.SelectedIndex = $fi
        $sc = "$([int]$script:Settings['Scale'])%"
        if ($cmbScale.Items.Contains($sc)) { $cmbScale.SelectedItem = $sc } else { $cmbScale.SelectedItem = '100%' }
        $chkTopmost.IsChecked    = [bool]$script:Settings['Topmost']
        $chkMaxStart.IsChecked   = [bool]$script:Settings['StartMaximized']
        $chkStatusBar.IsChecked  = [bool]$script:Settings['StatusBar']
        $chkHideConsole.IsChecked = [bool]$script:Settings['HideConsole']
        $chkRemember.IsChecked   = [bool]$script:Settings['RememberTab']
        $chkAutoRestore.IsChecked = [bool]$script:Settings['AutoRestore']
        $chkAutoUpd.IsChecked = [bool]$script:Settings['AutoUpdateCheck']
        $chkAutoPkg.IsChecked = [bool]$script:Settings['AutoInstallPkgMgrs']
        $chkInstant.IsChecked     = [bool]$script:Settings['InstantApply']
        $st = [string]$script:Settings['StartTab']
        if ($cmbStartTab.Items.Contains($st)) { $cmbStartTab.SelectedItem = $st } else { $cmbStartTab.SelectedIndex = 0 }
        $cmbStartTab.IsEnabled = (-not [bool]$script:Settings['RememberTab'])
        $txtAccentHex.Text = Get-CurrentAccentHex
        $txtBgHex.Text = [string]$script:Settings['BgColor']
        $v = [string]$script:Settings['ScrollAnim']; if ($cmbScrollAnim.Items.Contains($v)) { $cmbScrollAnim.SelectedItem = $v } else { $cmbScrollAnim.SelectedItem = 'Smooth' }
        $v = [string]$script:Settings['ScrollSpeed']; if ($cmbScrollSpeed.Items.Contains($v)) { $cmbScrollSpeed.SelectedItem = $v } else { $cmbScrollSpeed.SelectedItem = 'Normal' }
        $v = [string]$script:Settings['PageAnim']; if ($cmbPageAnim.Items.Contains($v)) { $cmbPageAnim.SelectedItem = $v } else { $cmbPageAnim.SelectedItem = 'Slide up' }
        $chkCardAnim.IsChecked = [bool]$script:Settings['CardAnim']
        $chkClassicUi.IsChecked = ([string]$script:Settings['UiStyle'] -eq 'Classic')
        $op = [int]$script:Settings['BgOpacity']
        $sldBgOpacity.Value = $op
        $txtBgOpacity.Text = "$op%"
        $hasImg = Test-BgImage
        $sldBgOpacity.IsEnabled = $hasImg
        $sldBgBlur.IsEnabled = $hasImg
        $bl = [int]$script:Settings['BgBlur']
        $sldBgBlur.Value = $bl
        $txtBgBlur.Text = "$bl"
        if ($hasImg) { $txtBgImgName.Text = Split-Path -Leaf ([string]$script:Settings['BgImage']) } else { $txtBgImgName.Text = L 'No image selected - choose one to unlock the opacity and blur sliders' }
    } finally { $script:SettingsBusy = $false }
}

function Apply-Theme {
    $script:Pal = Get-Palette
    $mod = Test-Modern
    foreach ($k in @($script:Pal.Keys)) {
        # the brand gradient is the same in light and dark mode: it is never swapped at runtime (swapping it is what broke the theme switch)
        if ($mod -and ($script:GradKeys -contains $k)) { continue }
        try { $window.Resources['K_' + $k] = New-RawBrush $script:Pal[$k] }
        catch {
            try {
                $dir = Join-Path $env:LOCALAPPDATA 'RockToolbox'
                if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
                Add-Content -LiteralPath (Join-Path $dir 'error.log') -Value ((Get-Date).ToString('s') + '  Theme brush K_' + $k + ' failed: ' + $_.Exception.ToString() + "`r`n") -Encoding UTF8
            } catch { }
        }
    }
    Update-TabColors
    Build-Swatches
}

function Set-ThemeMode([bool]$Light) {
    if ($Light) { $script:Settings['Theme'] = 'Light' } else { $script:Settings['Theme'] = 'Dark' }
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log ('Theme: ' + $script:Settings['Theme']) 'INFO' 'Settings'
}

function Set-Accent([string]$Name) {
    $script:Settings['Accent'] = $Name
    if ($script:AccentPresets.Contains($Name)) { $script:Settings['AccentHex'] = [string]$script:AccentPresets[$Name] }
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log ('Accent color: ' + $Name) 'INFO' 'Settings'
}

$script:RockVersion = 'V3.0.0'
$script:RockRepoUrl = 'https://github.com/OwnerRock/RockToolbox'

function Show-About {
    $ver = $script:RockVersion
    $osText = 'Windows'
    try {
        $o = Get-CimInstance Win32_OperatingSystem
        $osText = [string]$o.Caption
        $dv = ''
        try { $dv = [string](Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop).DisplayVersion } catch { }
        if ($dv) { $osText += ' ' + $dv }
        $osText += ' (build ' + $o.BuildNumber + ', ' + $o.OSArchitecture + ')'
    } catch { }
    $aboutXaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="About Rock Toolbox" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" WindowStyle="None" BorderBrush="#2B3038" BorderThickness="1" FontFamily="Segoe UI" FontSize="13">
  <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight="36" ResizeBorderThickness="0" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/></shell:WindowChrome.WindowChrome>
  <Window.Resources>
    <Style x:Key="TitleClose" TargetType="Button">
      <Setter Property="Width" Value="44"/><Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="#C9CDE0"/>
      <Setter Property="Focusable" Value="False"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#E5484D"/><Setter Property="Foreground" Value="#FFFFFF"/></Trigger>
        <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#C23B40"/></Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="AboutBtn" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#2F6FB0" CornerRadius="3" Padding="30,7">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#FFFFFF"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="0.82" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <Grid ClipToBounds="True">
    <Rectangle x:Name="aboutBg" Visibility="Collapsed" IsHitTestVisible="False" Margin="-40">
    </Rectangle>
    <Rectangle x:Name="aboutDim" Visibility="Collapsed" IsHitTestVisible="False" Fill="#1F2329" Opacity="0.4"/>
    <Border BorderBrush="#2B3038" BorderThickness="1" Padding="30,62,30,26" MinWidth="460">
      <StackPanel>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
          <Viewbox x:Name="aboutLogo" Width="64" Height="64" Margin="0,0,16,0"/>
          <StackPanel VerticalAlignment="Center">
            <TextBlock Text="ROCK TOOLBOX" FontSize="24" FontWeight="Black" Foreground="White"/>
            <TextBlock Text="Rock Tech - Windows Toolbox" Foreground="#9AA0B8"/>
          </StackPanel>
        </StackPanel>
        <TextBlock Text="Install apps, apply tweaks, debloat Windows, configure features, control updates, set wallpapers and open every Control Panel setting from one place." Foreground="#C9CDE0" TextWrapping="Wrap" MaxWidth="440" Margin="0,18,0,0" TextAlignment="Center"/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4" Padding="18,12" Margin="0,16,0,0">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" Grid.Column="0" Text="Developer" Foreground="#9AA0B8" Margin="0,4,24,4"/>
            <StackPanel Grid.Row="0" Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
              <TextBlock Text="Mohammed" Foreground="#E6E8F2" FontWeight="SemiBold"/>
              <Border Width="24" Height="16" Margin="8,1,0,0" BorderBrush="#2B3038" BorderThickness="0.5" ClipToBounds="True" ToolTip="Egypt">
                <Grid>
                  <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                  </Grid.RowDefinitions>
                  <Rectangle Grid.Row="0" Fill="#CE1126"/>
                  <Rectangle Grid.Row="1" Fill="#FFFFFF"/>
                  <Rectangle Grid.Row="2" Fill="#000000"/>
                  <Ellipse Grid.Row="1" Width="4.5" Height="4.5" Fill="#C09300" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Grid>
              </Border>
            </StackPanel>

            <TextBlock Grid.Row="1" Grid.Column="0" Text="Version" Foreground="#9AA0B8" Margin="0,4,24,4"/>
            <TextBlock x:Name="aboutVersion" Grid.Row="1" Grid.Column="1" Foreground="#4FD1FF" FontWeight="SemiBold" VerticalAlignment="Center"/>

            <TextBlock Grid.Row="2" Grid.Column="0" Text="Windows" Foreground="#9AA0B8" Margin="0,4,24,4"/>
            <TextBlock x:Name="aboutWindows" Grid.Row="2" Grid.Column="1" Foreground="#E6E8F2" TextWrapping="Wrap" MaxWidth="320" VerticalAlignment="Center"/>

            <TextBlock Grid.Row="3" Grid.Column="0" Text="Credits" Foreground="#9AA0B8" Margin="0,4,24,4"/>
            <TextBlock x:Name="aboutCredits" Grid.Row="3" Grid.Column="1" Text="@OwnerRock" Foreground="#4FD1FF" FontWeight="SemiBold"
                       VerticalAlignment="Center" HorizontalAlignment="Left" Cursor="Hand" ToolTip="Open https://t.me/OwnerRock" RenderTransformOrigin="0,0.5">
              <TextBlock.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></TextBlock.RenderTransform>
              <TextBlock.Triggers>
                <EventTrigger RoutedEvent="MouseEnter">
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1.12" Duration="0:0:0.15"/>
                      <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1.12" Duration="0:0:0.15"/>
                    </Storyboard>
                  </BeginStoryboard>
                </EventTrigger>
                <EventTrigger RoutedEvent="MouseLeave">
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.15"/>
                      <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.15"/>
                    </Storyboard>
                  </BeginStoryboard>
                </EventTrigger>
              </TextBlock.Triggers>
            </TextBlock>
          </Grid>
        </Border>
        <Button x:Name="btnAboutClose" Content="Close" Style="{StaticResource AboutBtn}" HorizontalAlignment="Center" Margin="0,18,0,0" Cursor="Hand"/>
      </StackPanel>
    </Border>
    <Border VerticalAlignment="Top" Height="36" Background="#181B20" BorderBrush="#2B3038" BorderThickness="0,0,0,1">
      <DockPanel>
        <Button x:Name="btnTitleClose" DockPanel.Dock="Right" Style="{StaticResource TitleClose}" shell:WindowChrome.IsHitTestVisibleInChrome="True">
          <Path Data="M0,0 L10,10 M10,0 L0,10" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.3" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Width="10" Height="10"/>
        </Button>
        <TextBlock Text="About Rock Toolbox" Foreground="#C9CDE0" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center" Margin="14,0,0,0"/>
      </DockPanel>
    </Border>
  </Grid>
</Window>
'@
    $aboutXaml = Convert-XamlTheme -Text $aboutXaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($aboutXaml)
    Merge-RockResources $w
    $script:AboutWin = $w
    try { $w.FindName('aboutLogo').Child = [Windows.Markup.XamlReader]::Parse($logoXaml) } catch { }
    $w.FindName('aboutVersion').Text = $ver
    $w.FindName('aboutWindows').Text = $osText
    $cred = $w.FindName('aboutCredits')
    $cred.Add_MouseEnter({ param($s, $e) $s.TextDecorations = [System.Windows.TextDecorations]::Underline })
    $cred.Add_MouseLeave({ param($s, $e) $s.TextDecorations = $null })
    $cred.Add_MouseLeftButtonUp({
        try { Start-Process 'https://t.me/OwnerRock' } catch { Log ('Could not open the link: ' + $_.Exception.Message) 'ERROR' 'About' }
    })
    # same custom background image / opacity as the main window
    try {
        if ($imgBg.Visibility -eq 'Visible' -and $imgBg.Source) {
            $br = New-Object System.Windows.Media.ImageBrush((Get-BgBlurSource 30))
            $br.Stretch = 'UniformToFill'
            $br.Opacity = $imgBg.Opacity
            $rect = $w.FindName('aboutBg')
            $rect.Fill = $br
            $rect.Visibility = 'Visible'
            $w.FindName('aboutDim').Visibility = 'Visible'
        }
    } catch { }
    $w.FindName('btnAboutClose').Add_Click({ $script:AboutWin.Close() })
    $w.FindName('btnTitleClose').Add_Click({ $script:AboutWin.Close() })
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    $w.Add_Loaded({ Start-DialogAnim $script:AboutWin.Content })
    [void]$w.ShowDialog()
}

# ---- Check for updates window (opened from the gear menu)
function ConvertTo-RockVer([string]$v) {
    $m = [regex]::Match($v, '\d+(\.\d+){0,3}')
    if (-not $m.Success) { return $null }
    $t = $m.Value
    if ($t -notmatch '\.') { $t += '.0' }
    $o = $null
    if ([version]::TryParse($t, [ref]$o)) { return $o }
    return $null
}

# the background job that asks GitHub for the latest version (runs in its own runspace)
$script:UpdateJob = {
    $ProgressPreference = 'SilentlyContinue'
    try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }
    $hdr = @{ 'User-Agent' = 'RockToolbox' }
    $out = @{ Ok = $false; Latest = ''; Url = '' }
    try {
        $r = Invoke-RestMethod -UseBasicParsing -TimeoutSec 12 -Headers $hdr -Uri 'https://api.github.com/repos/OwnerRock/RockToolbox/releases/latest'
        if ($r.tag_name) { $out.Ok = $true; $out.Latest = [string]$r.tag_name; $out.Url = [string]$r.html_url }
    } catch { }
    if (-not $out.Ok) {
        # no published release: read the version written in the script on the main branch
        try {
            $raw = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 -Headers $hdr -Uri 'https://raw.githubusercontent.com/OwnerRock/RockToolbox/main/RockToolbox.ps1'
            $m = [regex]::Match([string]$raw.Content, "RockVersion\s*=\s*'([^']+)'")
            if (-not $m.Success) { $m = [regex]::Match([string]$raw.Content, "\`$ver\s*=\s*'(V[^']+)'") }
            if ($m.Success) { $out.Ok = $true; $out.Latest = $m.Groups[1].Value; $out.Url = 'https://github.com/OwnerRock/RockToolbox' }
        } catch { }
    }
    New-Object PSObject -Property $out
}

function Get-UpdateState {
    $latest = [string]$script:Settings['UpdLatest']
    if (-not $latest) { return 'None' }
    $cur = ConvertTo-RockVer $script:RockVersion
    $new = ConvertTo-RockVer $latest
    if ($cur -and $new -and $new -gt $cur) { return 'Available' }
    return 'Latest'
}

# red dot on the gear button + on "Check for updates" inside the gear menu
function Update-UpdateBadges {
    $avail = ((Get-UpdateState) -eq 'Available')
    foreach ($d in @($dotGear, $dotGearUpdate)) {
        if (-not $d) { continue }
        if ($avail) { $d.Visibility = 'Visible'; Start-DotPulse $d }
        else {
            try { $d.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $null) } catch { }
            $d.Visibility = 'Collapsed'
        }
    }
    try {
        if ($avail) { $btnGear.ToolTip = (L 'Settings') + ' - ' + (L 'A new version is available.') } else { $btnGear.ToolTip = L 'Settings' }
    } catch { }
}

function Save-UpdateResult {
    param($Res)
    $script:Settings['UpdLatest'] = [string]$Res.Latest
    $script:Settings['UpdUrl'] = [string]$Res.Url
    $script:Settings['UpdCheckedAt'] = (Get-Date).ToString('o')
    Save-Settings
    Update-UpdateBadges
}

function Format-UpdateChecked {
    $t = [string]$script:Settings['UpdCheckedAt']
    if (-not $t) { return '' }
    try {
        $d = [DateTime]::Parse($t, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToLocalTime()
        return ((L 'Last checked') + ': ' + $d.ToString('g'))
    } catch { return '' }
}

function Test-UpdateCheckDue {
    if (-not [bool]$script:Settings['AutoUpdateCheck']) { return $false }
    $t = [string]$script:Settings['UpdCheckedAt']
    if (-not $t) { return $true }
    try {
        $d = [DateTime]::Parse($t, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        return (((Get-Date).ToUniversalTime() - $d.ToUniversalTime()).TotalHours -ge 6)
    } catch { return $true }
}

# shows the saved result in the "Check for updates" window (no need to press the button again after reopening)
function Show-UpdateStatus {
    $ui = $script:UpdUi
    if (-not $ui) { return }
    $state = Get-UpdateState
    $ui.BtnDl.Visibility = 'Collapsed'
    $script:UpdUrl = $script:RockRepoUrl
    if ($state -eq 'None') {
        $ui.Latest.Text = '-'
        $ui.Status.Text = L 'Press "Check for updates" to look for a new version.'
        $ui.Status.Foreground = Get-Brush '#9AA0B8'
        $ui.Checked.Text = ''
        return
    }
    $ui.Latest.Text = [string]$script:Settings['UpdLatest']
    if ([string]$script:Settings['UpdUrl']) { $script:UpdUrl = [string]$script:Settings['UpdUrl'] }
    if ($state -eq 'Available') {
        $ui.Status.Text = L 'A new version is available.'
        $ui.Status.Foreground = Get-Brush '#FFB454'
        $ui.BtnDl.Visibility = 'Visible'
    } else {
        $ui.Status.Text = L 'You have the latest version.'
        $ui.Status.Foreground = Get-Brush '#3DDC84'
    }
    $ui.Checked.Text = Format-UpdateChecked
}

# automatic check when the app starts (background, silent - only the dots show the result)
function Start-SilentUpdateCheck {
    if ($script:SilentPs) { return }
    try {
        $script:SilentPs = [powershell]::Create()
        [void]$script:SilentPs.AddScript($script:UpdateJob.ToString())
        $script:SilentHandle = $script:SilentPs.BeginInvoke()
    } catch { $script:SilentPs = $null; return }
    $script:SilentTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:SilentTimer.Interval = [TimeSpan]::FromMilliseconds(500)
    $script:SilentTimer.Add_Tick({
        if (-not $script:SilentHandle -or -not $script:SilentHandle.IsCompleted) { return }
        $script:SilentTimer.Stop()
        $res = $null
        try { $res = @($script:SilentPs.EndInvoke($script:SilentHandle))[0] } catch { }
        try { $script:SilentPs.Dispose() } catch { }
        $script:SilentPs = $null
        if ($res -and $res.Ok) {
            Save-UpdateResult $res
            if ((Get-UpdateState) -eq 'Available') { Log ('A new version of Rock Toolbox is available: ' + [string]$res.Latest) 'INFO' 'Update' }
        }
    })
    $script:SilentTimer.Start()
}

function Start-UpdateCheck {
    $ui = $script:UpdUi
    $ui.Latest.Text = '...'
    $ui.Status.Text = L 'Checking for updates...'
    $ui.Status.Foreground = Get-Brush '#9AA0B8'
    $ui.Checked.Text = ''
    $ui.BtnCheck.IsEnabled = $false
    $ui.BtnDl.Visibility = 'Collapsed'
    try {
        $script:UpdPs = [powershell]::Create()
        [void]$script:UpdPs.AddScript($script:UpdateJob.ToString())
        $script:UpdHandle = $script:UpdPs.BeginInvoke()
    } catch {
        $ui.Status.Text = L 'Could not check for updates. Check your internet connection.'
        $ui.Status.Foreground = Get-Brush '#FF6B6B'
        $ui.BtnCheck.IsEnabled = $true
        return
    }
    if ($script:UpdTimer) { $script:UpdTimer.Stop() }
    $script:UpdTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:UpdTimer.Interval = [TimeSpan]::FromMilliseconds(250)
    $script:UpdTimer.Add_Tick({
        if (-not $script:UpdHandle -or -not $script:UpdHandle.IsCompleted) { return }
        $script:UpdTimer.Stop()
        $ui = $script:UpdUi
        $res = $null
        try { $res = @($script:UpdPs.EndInvoke($script:UpdHandle))[0] } catch { }
        try { $script:UpdPs.Dispose() } catch { }
        $ui.BtnCheck.IsEnabled = $true
        if (-not $res -or -not $res.Ok) {
            $ui.Latest.Text = '-'
            $ui.Status.Text = L 'Could not check for updates. Check your internet connection.'
            $ui.Status.Foreground = Get-Brush '#FF6B6B'
            return
        }
        Save-UpdateResult $res     # remembered: the result is still there after closing and reopening
        Show-UpdateStatus
    })
    $script:UpdTimer.Start()
}

# ---- updates: the app is installed by Setup, so an update = download and run the new installer from the release page
$script:RockUpdateUrl = 'https://github.com/OwnerRock/RockToolbox/releases/latest'
function Start-RockSelfUpdate {
    try { Start-Process $script:RockUpdateUrl } catch { Log ('Could not open the download page: ' + $_.Exception.Message) 'ERROR' 'Update' }
}

function Show-UpdateCheck {
    $updXaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="Check for updates" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" WindowStyle="None" BorderBrush="#2B3038" BorderThickness="1" FontFamily="Segoe UI" FontSize="13">
  <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight="36" ResizeBorderThickness="0" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/></shell:WindowChrome.WindowChrome>
  <Window.Resources>
    <Style x:Key="TitleClose" TargetType="Button">
      <Setter Property="Width" Value="44"/><Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="#C9CDE0"/>
      <Setter Property="Focusable" Value="False"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#E5484D"/><Setter Property="Foreground" Value="#FFFFFF"/></Trigger>
        <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#C23B40"/></Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="AboutBtn" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#2F6FB0" CornerRadius="3" Padding="26,7">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#FFFFFF"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="0.82" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False"><Setter TargetName="bd" Property="Opacity" Value="0.45"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <Grid ClipToBounds="True">
    <Rectangle x:Name="updBg" Visibility="Collapsed" IsHitTestVisible="False" Margin="-40">
    </Rectangle>
    <Rectangle x:Name="updDim" Visibility="Collapsed" IsHitTestVisible="False" Fill="#1F2329" Opacity="0.4"/>
    <Border BorderBrush="#2B3038" BorderThickness="1" Padding="30,62,30,26" MinWidth="460">
      <StackPanel>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
          <Viewbox x:Name="updLogo" Width="64" Height="64" Margin="0,0,16,0"/>
          <StackPanel VerticalAlignment="Center">
            <TextBlock Text="ROCK TOOLBOX" FontSize="24" FontWeight="Black" Foreground="White"/>
            <TextBlock Text="Check for updates" Foreground="#9AA0B8"/>
          </StackPanel>
        </StackPanel>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4" Padding="18,12" Margin="0,18,0,0">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <TextBlock Grid.Row="0" Grid.Column="0" Text="Current version" Foreground="#9AA0B8" Margin="0,4,24,4"/>
            <TextBlock x:Name="updCurrent" Grid.Row="0" Grid.Column="1" Foreground="#4FD1FF" FontWeight="SemiBold" VerticalAlignment="Center"/>
            <TextBlock Grid.Row="1" Grid.Column="0" Text="Latest version" Foreground="#9AA0B8" Margin="0,4,24,4"/>
            <TextBlock x:Name="updLatest" Grid.Row="1" Grid.Column="1" Foreground="#E6E8F2" FontWeight="SemiBold" VerticalAlignment="Center"/>
          </Grid>
        </Border>
        <TextBlock x:Name="updStatus" Foreground="#9AA0B8" TextWrapping="Wrap" MaxWidth="400" TextAlignment="Center" HorizontalAlignment="Center" Margin="0,16,0,0"/>
        <TextBlock x:Name="updChecked" Foreground="#6B7186" FontSize="11.5" TextAlignment="Center" HorizontalAlignment="Center" Margin="0,4,0,0"/>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,18,0,0">
          <Button x:Name="btnUpdCheck" Content="Check for updates" Style="{StaticResource AboutBtn}" Cursor="Hand" Margin="0,0,8,0"/>
          <Button x:Name="btnUpdDownload" Content="Update now" Style="{StaticResource AboutBtn}" Cursor="Hand" Margin="0,0,8,0" Visibility="Collapsed"/>
          <Button x:Name="btnUpdClose" Content="Close" Style="{StaticResource AboutBtn}" Cursor="Hand"/>
        </StackPanel>
      </StackPanel>
    </Border>
    <Border VerticalAlignment="Top" Height="36" Background="#181B20" BorderBrush="#2B3038" BorderThickness="0,0,0,1">
      <DockPanel>
        <Button x:Name="btnTitleClose" DockPanel.Dock="Right" Style="{StaticResource TitleClose}" shell:WindowChrome.IsHitTestVisibleInChrome="True">
          <Path Data="M0,0 L10,10 M10,0 L0,10" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.3" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Width="10" Height="10"/>
        </Button>
        <TextBlock Text="Check for updates" Foreground="#C9CDE0" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center" Margin="14,0,0,0"/>
      </DockPanel>
    </Border>
  </Grid>
</Window>
'@
    if (Test-Translated) { $updXaml = Convert-XamlLang $updXaml }
    $updXaml = Convert-XamlTheme -Text $updXaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($updXaml)
    Merge-RockResources $w
    $script:UpdWin = $w
    try { $w.FindName('updLogo').Child = [Windows.Markup.XamlReader]::Parse($logoXaml) } catch { }
    $script:UpdUi = @{
        Latest   = $w.FindName('updLatest')
        Status   = $w.FindName('updStatus')
        Checked  = $w.FindName('updChecked')
        BtnCheck = $w.FindName('btnUpdCheck')
        BtnDl    = $w.FindName('btnUpdDownload')
    }
    $w.FindName('updCurrent').Text = $script:RockVersion
    Show-UpdateStatus
    try {
        if ($imgBg.Visibility -eq 'Visible' -and $imgBg.Source) {
            $br = New-Object System.Windows.Media.ImageBrush((Get-BgBlurSource 30))
            $br.Stretch = 'UniformToFill'; $br.Opacity = $imgBg.Opacity
            $rect = $w.FindName('updBg'); $rect.Fill = $br; $rect.Visibility = 'Visible'
            $w.FindName('updDim').Visibility = 'Visible'
        }
    } catch { }
    $script:UpdUi.BtnCheck.Add_Click({ Start-UpdateCheck })
    $script:UpdUi.BtnDl.Add_Click({ Start-RockSelfUpdate })
    $w.FindName('btnUpdClose').Add_Click({ $script:UpdWin.Close() })
    $w.FindName('btnTitleClose').Add_Click({ $script:UpdWin.Close() })
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    $w.Add_Loaded({ Start-DialogAnim $script:UpdWin.Content })
    [void]$w.ShowDialog()
}

# ---- gear popup menu
# a menu item under the mouse when the menu closes never gets its "mouse left" (it is collapsed), so it would reopen stuck in the hover look:
# rebuild the items so every opening starts clean
function Reset-GearItems {
    foreach ($b in @($btnGearSettings, $btnProfExport, $btnProfImport, $btnGearUpdate, $btnGearAbout)) {
        try { $st = $b.Style; $b.Style = $null; $b.Style = $st } catch { }
    }
}
function Show-GearMenu { Reset-GearItems; $menuLang.Visibility = 'Collapsed'; $menuGear.Visibility = 'Visible'; Start-PageAnim $menuGear -10; Update-ProfileCard; Start-ProfileAnim }
function Hide-GearMenu {
    $menuGear.Visibility = 'Collapsed'
    $menuLang.Visibility = 'Collapsed'
    try { [System.Windows.Input.Mouse]::Synchronize() } catch { }
}
$btnGear.Add_Click({
    if ($menuGear.Visibility -eq 'Visible') { Hide-GearMenu } else { Show-GearMenu }
})
$window.AddHandler([System.Windows.UIElement]::PreviewMouseLeftButtonDownEvent, [System.Windows.Input.MouseButtonEventHandler]{
    if ($menuGear.Visibility -eq 'Visible' -and -not $menuGear.IsMouseOver -and -not $menuLang.IsMouseOver -and -not $btnGear.IsMouseOver) { Hide-GearMenu }
    elseif ($menuLang.Visibility -eq 'Visible' -and -not $menuLang.IsMouseOver -and -not $btnGearLang.IsMouseOver) { $menuLang.Visibility = 'Collapsed' }
})
$window.Add_Deactivated({ Hide-GearMenu })
$window.Add_PreviewKeyDown({ param($s, $e) if ($e.Key -eq 'Escape') { Hide-GearMenu } })
$chkGearLight.Add_Click({ Set-ThemeMode ([bool]$chkGearLight.IsChecked) })

# ---- custom title bar buttons (minimize / maximize-restore / close)
function Update-CaptionState {
    if ($window.WindowState -eq 'Maximized') {
        # a borderless maximized window overhangs the screen by the resize border - pull the content back in
        $rootGrid.Margin = [System.Windows.SystemParameters]::WindowResizeBorderThickness
        $pathMax.Visibility = 'Collapsed'; $pathRestore.Visibility = 'Visible'
        $btnMax.ToolTip = L 'Restore'
    } else {
        $rootGrid.Margin = New-Object System.Windows.Thickness(0)
        $pathMax.Visibility = 'Visible'; $pathRestore.Visibility = 'Collapsed'
        $btnMax.ToolTip = L 'Maximize'
    }
}
$btnMin.Add_Click({ $window.WindowState = 'Minimized' })
$btnMax.Add_Click({ if ($window.WindowState -eq 'Maximized') { $window.WindowState = 'Normal' } else { $window.WindowState = 'Maximized' } })
$btnClose.Add_Click({ $window.Close() })
$window.Add_StateChanged({ Update-CaptionState })
Update-CaptionState
$btnGearSettings.Add_Click({ Hide-GearMenu; Show-Page 'Settings' })
$btnGearAbout.Add_Click({ Hide-GearMenu; Show-About })
$btnGearUpdate.Add_Click({ Hide-GearMenu; Show-UpdateCheck })

# ---- flags drawn with shapes (60x40 canvas, scaled): used by the welcome language page and the gear-menu language list
function New-FlagBrush([string]$Hex) {
    $b = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($Hex))
    $b.Freeze(); return $b
}
function Add-FlagRect($Cv, [double]$X, [double]$Y, [double]$W, [double]$H, [string]$Hex) {
    $r = New-Object System.Windows.Shapes.Rectangle; $r.Width = $W; $r.Height = $H; $r.Fill = New-FlagBrush $Hex
    [System.Windows.Controls.Canvas]::SetLeft($r, $X); [System.Windows.Controls.Canvas]::SetTop($r, $Y); [void]$Cv.Children.Add($r)
}
function Add-FlagCircle($Cv, [double]$Cx, [double]$Cy, [double]$R, [string]$Hex, [bool]$Ring = $false) {
    $e = New-Object System.Windows.Shapes.Ellipse; $e.Width = 2 * $R; $e.Height = 2 * $R
    if ($Ring) { $e.Stroke = New-FlagBrush $Hex; $e.StrokeThickness = 0.9 } else { $e.Fill = New-FlagBrush $Hex }
    [System.Windows.Controls.Canvas]::SetLeft($e, $Cx - $R); [System.Windows.Controls.Canvas]::SetTop($e, $Cy - $R); [void]$Cv.Children.Add($e)
}
function Add-FlagPoly($Cv, [string]$Pts, [string]$Hex) {
    $p = New-Object System.Windows.Shapes.Polygon; $p.Points = [System.Windows.Media.PointCollection]::Parse($Pts); $p.Fill = New-FlagBrush $Hex; [void]$Cv.Children.Add($p)
}
function Add-FlagLine($Cv, [double]$X1, [double]$Y1, [double]$X2, [double]$Y2, [double]$Th, [string]$Hex) {
    $l = New-Object System.Windows.Shapes.Line; $l.X1 = $X1; $l.Y1 = $Y1; $l.X2 = $X2; $l.Y2 = $Y2; $l.Stroke = New-FlagBrush $Hex; $l.StrokeThickness = $Th; [void]$Cv.Children.Add($l)
}
function Add-FlagStar($Cv, [double]$Cx, [double]$Cy, [double]$R, [string]$Hex, [double]$Rot = -90) {
    $inv = [System.Globalization.CultureInfo]::InvariantCulture; $pts = @()
    for ($i = 0; $i -lt 10; $i++) {
        $rad = $R; if ($i % 2 -eq 1) { $rad = $R * 0.382 }
        $a = ($Rot + $i * 36) * [Math]::PI / 180
        $pts += (($Cx + $rad * [Math]::Cos($a)).ToString('0.##', $inv) + ',' + ($Cy + $rad * [Math]::Sin($a)).ToString('0.##', $inv))
    }
    Add-FlagPoly $Cv ($pts -join ' ') $Hex
}
function New-FlagVisual([string]$Code, [double]$W, [double]$H, [double]$Ring = 3) {
    $cv = New-Object System.Windows.Controls.Canvas; $cv.Width = 60; $cv.Height = 40
    switch ($Code) {
        'en' {
            Add-FlagRect $cv 0 0 60 40 '#012169'
            Add-FlagLine $cv 0 0 60 40 7 '#FFFFFF'; Add-FlagLine $cv 60 0 0 40 7 '#FFFFFF'
            Add-FlagLine $cv 0 0 60 40 2.6 '#C8102E'; Add-FlagLine $cv 60 0 0 40 2.6 '#C8102E'
            Add-FlagRect $cv 0 14 60 12 '#FFFFFF'; Add-FlagRect $cv 24 0 12 40 '#FFFFFF'
            Add-FlagRect $cv 0 16.4 60 7.2 '#C8102E'; Add-FlagRect $cv 26.4 0 7.2 40 '#C8102E'
        }
        'ar' { Add-FlagRect $cv 0 0 60 13.4 '#CE1126'; Add-FlagRect $cv 0 13.3 60 13.4 '#FFFFFF'; Add-FlagRect $cv 0 26.6 60 13.4 '#000000'; Add-FlagCircle $cv 30 20 3.2 '#C09300' }
        'fr' { Add-FlagRect $cv 0 0 20.2 40 '#0055A4'; Add-FlagRect $cv 20 0 20.2 40 '#FFFFFF'; Add-FlagRect $cv 40 0 20 40 '#EF4135' }
        'es' { Add-FlagRect $cv 0 0 60 10.2 '#AA151B'; Add-FlagRect $cv 0 10 60 20.2 '#F1BF00'; Add-FlagRect $cv 0 30 60 10 '#AA151B' }
        'de' { Add-FlagRect $cv 0 0 60 13.4 '#000000'; Add-FlagRect $cv 0 13.3 60 13.4 '#DD0000'; Add-FlagRect $cv 0 26.6 60 13.4 '#FFCE00' }
        'tr' { Add-FlagRect $cv 0 0 60 40 '#E30A17'; Add-FlagCircle $cv 22 20 10 '#FFFFFF'; Add-FlagCircle $cv 25 20 8 '#E30A17'; Add-FlagStar $cv 33.5 20 5 '#FFFFFF' -90 }
        'ru' { Add-FlagRect $cv 0 0 60 13.4 '#FFFFFF'; Add-FlagRect $cv 0 13.3 60 13.4 '#0039A6'; Add-FlagRect $cv 0 26.6 60 13.4 '#D52B1E' }
        'zh' {
            Add-FlagRect $cv 0 0 60 40 '#DE2910'; Add-FlagStar $cv 10 10 6.2 '#FFDE00'
            Add-FlagStar $cv 20 4 2.1 '#FFDE00'; Add-FlagStar $cv 24 8.5 2.1 '#FFDE00'; Add-FlagStar $cv 24 14.5 2.1 '#FFDE00'; Add-FlagStar $cv 20 19 2.1 '#FFDE00'
        }
        'ja' { Add-FlagRect $cv 0 0 60 40 '#FFFFFF'; Add-FlagCircle $cv 30 20 12 '#BC002D' }
        'pt' { Add-FlagRect $cv 0 0 60 40 '#009C3B'; Add-FlagPoly $cv '30,3 56,20 30,37 4,20' '#FFDF00'; Add-FlagCircle $cv 30 20 9.5 '#002776' }
        'it' { Add-FlagRect $cv 0 0 20.2 40 '#009246'; Add-FlagRect $cv 20 0 20.2 40 '#FFFFFF'; Add-FlagRect $cv 40 0 20 40 '#CE2B37' }
        'hi' { Add-FlagRect $cv 0 0 60 13.4 '#FF9933'; Add-FlagRect $cv 0 13.3 60 13.4 '#FFFFFF'; Add-FlagRect $cv 0 26.6 60 13.4 '#138808'; Add-FlagCircle $cv 30 20 5.4 '#000080' $true; Add-FlagCircle $cv 30 20 1.1 '#000080' }
        default { Add-FlagRect $cv 0 0 60 40 '#9AA0B8' }
    }
    $vb = New-Object System.Windows.Controls.Viewbox; $vb.Stretch = 'Fill'; $vb.Child = $cv
    $iw = [Math]::Max(4, $W - 2 * $Ring); $ih = [Math]::Max(4, $H - 2 * $Ring)
    $rr = [Math]::Max(3, [Math]::Min($iw, $ih) * 0.22)
    $inner = New-Object System.Windows.Controls.Grid; $inner.Width = $iw; $inner.Height = $ih
    $inner.Clip = New-Object System.Windows.Media.RectangleGeometry((New-Object System.Windows.Rect(0, 0, $iw, $ih)), $rr, $rr)
    [void]$inner.Children.Add($vb)
    $b = New-Object System.Windows.Controls.Border
    $b.Width = $W; $b.Height = $H; $b.CornerRadius = New-Object System.Windows.CornerRadius ($rr + $Ring)
    $b.BorderThickness = New-Object System.Windows.Thickness($Ring); $b.BorderBrush = [System.Windows.Media.Brushes]::Transparent
    $b.Child = $inner
    return $b
}

# ---- profile card (photo + name) at the top of the gear menu
function Load-AvatarBitmap([string]$Path) {
    try {
        $bi = New-Object System.Windows.Media.Imaging.BitmapImage
        $bi.BeginInit(); $bi.CacheOption = 'OnLoad'; $bi.DecodePixelWidth = 256
        $bi.UriSource = New-Object System.Uri($Path)
        $bi.EndInit(); $bi.Freeze()
        return $bi
    } catch { return $null }
}
function Get-UserDisplayName {
    $n = ([string]$script:Settings['UserName']).Trim()
    if (-not $n) { $n = [string]$env:USERNAME }
    return $n
}
function Update-ProfileCard {
    $n = Get-UserDisplayName
    $txtUserName.Text = $n
    $bmp = $null
    $ap = [string]$script:Settings['Avatar']
    if ($ap -and (Test-Path -LiteralPath $ap)) { $bmp = Load-AvatarBitmap $ap }
    if ($bmp) {
        $ib = New-Object System.Windows.Media.ImageBrush($bmp); $ib.Stretch = 'UniformToFill'
        $elAvatar.Fill = $ib; $txtAvatarLetter.Visibility = 'Collapsed'
    } else {
        $elAvatar.Fill = New-FlagBrush '#B8C0CC'
        $txtAvatarLetter.Visibility = 'Visible'
    }
}
function Start-ProfileAnim {
    try {
        $d = New-Object System.Windows.Duration([TimeSpan]::FromMilliseconds(480))
        $sc = New-Object System.Windows.Media.ScaleTransform(0.55, 0.55)
        $gridAvatar.RenderTransform = $sc
        $ease = New-Object System.Windows.Media.Animation.BackEase; $ease.EasingMode = 'EaseOut'; $ease.Amplitude = 0.7
        $a = New-Object System.Windows.Media.Animation.DoubleAnimation(0.55, 1.0, $d); $a.EasingFunction = $ease
        $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $a)
        $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $a)
        $rs = New-Object System.Windows.Media.ScaleTransform(0.9, 0.9)
        $elRing.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
        $elRing.RenderTransform = $rs
        $rd = New-Object System.Windows.Duration([TimeSpan]::FromMilliseconds(1100))
        $rep = New-Object System.Windows.Media.Animation.RepeatBehavior(2.0)
        $ra = New-Object System.Windows.Media.Animation.DoubleAnimation(0.9, 1.25, $rd); $ra.RepeatBehavior = $rep; $ra.BeginTime = [TimeSpan]::FromMilliseconds(350)
        $of = New-Object System.Windows.Media.Animation.DoubleAnimation(0.75, 0.0, $rd); $of.RepeatBehavior = $rep; $of.BeginTime = [TimeSpan]::FromMilliseconds(350)
        $rs.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $ra)
        $rs.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $ra)
        $elRing.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $of)
    } catch { }
}
$cardProfile.Add_MouseLeftButtonUp({ Hide-GearMenu; Show-ProfileEditor })

# ---- language flyout (opens to the LEFT of the gear menu with a slide-in animation)
function Update-LangButtons {
    $cur = [string]$script:Settings['Language']
    $nat = $cur
    if ($script:LangList.Contains($cur)) { $nat = [string]$script:LangList[$cur].Native }
    $txtGearLangCur.Text = $nat + '  ' + [string][char]0x203A
}
function Start-FlyAnim($El) {
    $tt = New-Object System.Windows.Media.TranslateTransform(22, 0)
    $El.RenderTransform = $tt
    $ease = New-Object System.Windows.Media.Animation.CubicEase; $ease.EasingMode = 'EaseOut'
    $d = New-Object System.Windows.Duration([TimeSpan]::FromMilliseconds(260))
    $ax = New-Object System.Windows.Media.Animation.DoubleAnimation(22.0, 0.0, $d); $ax.EasingFunction = $ease
    $af = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $d)
    $tt.BeginAnimation([System.Windows.Media.TranslateTransform]::XProperty, $ax)
    $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $af)
}
function Build-LangFly {
    $spLangList.Children.Clear()
    $cur = [string]$script:Settings['Language']
    $style = $window.FindResource('GearItemBtn')
    $i = 0
    foreach ($k in $script:LangList.Keys) {
        $b = New-Object System.Windows.Controls.Button
        $b.Style = $style; $b.Tag = [string]$k; $b.HorizontalContentAlignment = 'Stretch'; $b.Padding = '12,7'
        $dp = New-Object System.Windows.Controls.DockPanel; $dp.LastChildFill = $false
        $fl = New-FlagVisual ([string]$script:LangList[$k].Code) 26 19 0
        $fl.Margin = '0,0,10,0'; $fl.VerticalAlignment = 'Center'
        [System.Windows.Controls.DockPanel]::SetDock($fl, [System.Windows.Controls.Dock]::Left)
        $t = New-Object System.Windows.Controls.TextBlock; $t.Text = [string]$script:LangList[$k].Native
        [System.Windows.Controls.DockPanel]::SetDock($t, [System.Windows.Controls.Dock]::Left)
        $c = New-Object System.Windows.Controls.TextBlock
        [System.Windows.Controls.DockPanel]::SetDock($c, [System.Windows.Controls.Dock]::Right)
        Set-Res $c 'Foreground' '#4FD1FF'
        if ($k -eq $cur) { $c.Text = [string][char]0x2713; Set-Res $b 'Background' '#35506A' }
        [void]$dp.Children.Add($fl); [void]$dp.Children.Add($t); [void]$dp.Children.Add($c)
        $b.Content = $dp
        $b.Add_Click({ param($s, $e) Hide-GearMenu; Set-RockLanguage ([string]$s.Tag) })
        $b.Opacity = 0
        $fa = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, (New-Object System.Windows.Duration([TimeSpan]::FromMilliseconds(220))))
        $fa.BeginTime = [TimeSpan]::FromMilliseconds(70 + $i * 32)
        $b.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fa)
        [void]$spLangList.Children.Add($b)
        $i++
    }
}
$btnGearLang.Add_Click({
    if ($menuLang.Visibility -eq 'Visible') { $menuLang.Visibility = 'Collapsed'; return }
    Build-LangFly
    $menuLang.Visibility = 'Visible'
    try {
        $pt = $btnGearLang.TranslatePoint((New-Object System.Windows.Point(0, 0)), $menuGear)
        $menuLang.Margin = New-Object System.Windows.Thickness(0, (8 + $pt.Y - 6), (14 + $menuGear.ActualWidth + 8), 0)
    } catch { }
    Start-FlyAnim $menuLang
})
$btnProfExport.Add_Click({ Hide-GearMenu })
$btnProfImport.Add_Click({ Hide-GearMenu })
Update-LangButtons
Update-ProfileCard

# ---- settings page
$chkLight.Add_Click({ Set-ThemeMode ([bool]$chkLight.IsChecked) })
$btnAccentApply.Add_Click({
    $x = $txtAccentHex.Text.Trim()
    if ($x -notmatch '^#?[0-9A-Fa-f]{6}$') { Log 'Enter a color like #FF6B6B.' 'WARN' 'Settings'; return }
    if (-not $x.StartsWith('#')) { $x = '#' + $x }
    $script:Settings['Accent'] = 'Custom'; $script:Settings['AccentHex'] = $x.ToUpper()
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log ('Accent color: ' + $x.ToUpper()) 'INFO' 'Settings'
})
$btnAccentReset.Add_Click({ Set-Accent 'Ocean' })
$cmbBgStyle.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbBgStyle.SelectedItem) { return }
    $script:Settings['Background'] = [string]$cmbBgStyle.SelectedItem
    $script:Settings['BgColor'] = ''
    Apply-Theme; Sync-SettingsUi; Save-Settings
})
$cmbFontSize.Add_SelectionChanged({
    if ($script:SettingsBusy -or $cmbFontSize.SelectedIndex -lt 0) { return }
    $script:Settings['FontSize'] = $script:FontSizes[$cmbFontSize.SelectedIndex]
    Apply-Settings; Save-Settings
})
$cmbScale.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbScale.SelectedItem) { return }
    $script:Settings['Scale'] = [int](([string]$cmbScale.SelectedItem) -replace '%', '')
    Apply-Settings; Save-Settings
})
$chkTopmost.Add_Click({ $script:Settings['Topmost'] = [bool]$chkTopmost.IsChecked; Apply-Settings; Save-Settings })
$chkMaxStart.Add_Click({ $script:Settings['StartMaximized'] = [bool]$chkMaxStart.IsChecked; Save-Settings })
$chkStatusBar.Add_Click({ $script:Settings['StatusBar'] = [bool]$chkStatusBar.IsChecked; Apply-Settings; Save-Settings })
$chkHideConsole.Add_Click({
    $script:Settings['HideConsole'] = [bool]$chkHideConsole.IsChecked
    Set-ConsoleVisible (-not [bool]$chkHideConsole.IsChecked)
    Save-Settings
})
$chkRemember.Add_Click({
    $script:Settings['RememberTab'] = [bool]$chkRemember.IsChecked
    $cmbStartTab.IsEnabled = (-not [bool]$chkRemember.IsChecked)
    Save-Settings
})
$cmbStartTab.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbStartTab.SelectedItem) { return }
    $script:Settings['StartTab'] = [string]$cmbStartTab.SelectedItem
    Save-Settings
})


