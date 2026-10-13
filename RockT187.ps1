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
    try { $txtCrumb.Text = $script:CurPage } catch { }
}

function Sync-SettingsUi {
    $script:SettingsBusy = $true
    try {
        $isLight = ($script:Settings['Theme'] -eq 'Light')
        $chkLight.IsChecked = $isLight; $chkGearLight.IsChecked = $isLight
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
        $st = [string]$script:Settings['StartTab']
        if ($cmbStartTab.Items.Contains($st)) { $cmbStartTab.SelectedItem = $st } else { $cmbStartTab.SelectedIndex = 0 }
        $cmbStartTab.IsEnabled = (-not [bool]$script:Settings['RememberTab'])
        $txtAccentHex.Text = Get-CurrentAccentHex
        $txtBgHex.Text = [string]$script:Settings['BgColor']
        $v = [string]$script:Settings['ScrollAnim']; if ($cmbScrollAnim.Items.Contains($v)) { $cmbScrollAnim.SelectedItem = $v } else { $cmbScrollAnim.SelectedItem = 'Smooth' }
        $v = [string]$script:Settings['ScrollSpeed']; if ($cmbScrollSpeed.Items.Contains($v)) { $cmbScrollSpeed.SelectedItem = $v } else { $cmbScrollSpeed.SelectedItem = 'Normal' }
        $v = [string]$script:Settings['PageAnim']; if ($cmbPageAnim.Items.Contains($v)) { $cmbPageAnim.SelectedItem = $v } else { $cmbPageAnim.SelectedItem = 'Slide up' }
        $chkCardAnim.IsChecked = [bool]$script:Settings['CardAnim']
        $op = [int]$script:Settings['BgOpacity']
        $sldBgOpacity.Value = $op
        $txtBgOpacity.Text = "$op%"
        $hasImg = Test-BgImage
        $sldBgOpacity.IsEnabled = $hasImg
        $sldBgBlur.IsEnabled = $hasImg
        $bl = [int]$script:Settings['BgBlur']
        $sldBgBlur.Value = $bl
        $txtBgBlur.Text = "$bl"
        if ($hasImg) { $txtBgImgName.Text = Split-Path -Leaf ([string]$script:Settings['BgImage']) } else { $txtBgImgName.Text = 'No image selected - choose one to unlock the opacity and blur sliders' }
    } finally { $script:SettingsBusy = $false }
}

function Apply-Theme {
    $script:Pal = Get-Palette
    foreach ($k in @($script:Pal.Keys)) { $window.Resources['K_' + $k] = New-RawBrush $script:Pal[$k] }
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

function Show-About {
    $ver = 'V1.0.0'
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
        Title="About Rock Toolbox" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" FontFamily="Segoe UI" FontSize="13">
  <Window.Resources>
    <Style x:Key="AboutBtn" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#2F6FB0" CornerRadius="3" Padding="30,7">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#FFFFFF"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Opacity" Value="0.85"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <Grid ClipToBounds="True">
    <Rectangle x:Name="aboutBg" Visibility="Collapsed" IsHitTestVisible="False" Margin="-40">
      <Rectangle.Effect><BlurEffect Radius="30" KernelType="Gaussian" RenderingBias="Performance"/></Rectangle.Effect>
    </Rectangle>
    <Rectangle x:Name="aboutDim" Visibility="Collapsed" IsHitTestVisible="False" Fill="#1F2329" Opacity="0.4"/>
    <Border BorderBrush="#2B3038" BorderThickness="1" Padding="30,26" MinWidth="460">
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
  </Grid>
</Window>
'@
    $aboutXaml = Convert-XamlTheme -Text $aboutXaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($aboutXaml)
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
            $br = New-Object System.Windows.Media.ImageBrush($imgBg.Source)
            $br.Stretch = 'UniformToFill'
            $br.Opacity = $imgBg.Opacity
            $rect = $w.FindName('aboutBg')
            $rect.Fill = $br
            $rect.Visibility = 'Visible'
            $w.FindName('aboutDim').Visibility = 'Visible'
        }
    } catch { }
    $w.FindName('btnAboutClose').Add_Click({ $script:AboutWin.Close() })
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    $w.Add_Loaded({ Start-PageAnim $script:AboutWin.Content 14 })
    [void]$w.ShowDialog()
}

# ---- gear menu
