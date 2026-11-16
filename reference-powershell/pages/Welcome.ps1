# ==============================================================================
# MODULE: Welcome wizard (first run of a new version) + profile editor
# ==============================================================================
$script:WelcomeVersion = '3.0.0'
$script:Wz = @{}
$script:WizWin = $null
$script:WizBitmap = $null
$script:WizLang = 'English'
$script:WizBusy = $false
$script:WizMode = 'Full'
$script:WizResult = $false
$script:WizReplay = $false
$script:WizHiTimer = $null

$script:WizXaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="Rock Toolbox" Width="520" Height="620" ResizeMode="NoResize" WindowStyle="None" WindowStartupLocation="CenterScreen"
        Background="#1F2329" BorderBrush="#2B3038" BorderThickness="1" FontFamily="Segoe UI" FontSize="13" UseLayoutRounding="True" SnapsToDevicePixels="True">
  <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight="36" ResizeBorderThickness="0" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/></shell:WindowChrome.WindowChrome>
  <Window.Resources>
    <Style x:Key="TitleClose" TargetType="Button">
      <Setter Property="Width" Value="44"/><Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="#C9CDE0"/>
      <Setter Property="Focusable" Value="False"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
      </ControlTemplate></Setter.Value></Setter>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#E5484D"/><Setter Property="Foreground" Value="#FFFFFF"/></Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="WizBtn" TargetType="Button">
      <Setter Property="Foreground" Value="#FFFFFF"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="16"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Focusable" Value="False"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
        <Border x:Name="bd" CornerRadius="26" Padding="58,14" RenderTransformOrigin="0.5,0.5">
          <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#FF9F0A" Offset="0"/><GradientStop Color="#FF375F" Offset="0.55"/><GradientStop Color="#BF5AF2" Offset="1"/></LinearGradientBrush></Border.Background>
          <Border.RenderTransform><ScaleTransform x:Name="sc" ScaleX="1" ScaleY="1"/></Border.RenderTransform>
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Trigger.EnterActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1.05" Duration="0:0:0.16"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1.05" Duration="0:0:0.16"/>
            </Storyboard></BeginStoryboard></Trigger.EnterActions>
            <Trigger.ExitActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1" Duration="0:0:0.2"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1" Duration="0:0:0.2"/>
            </Storyboard></BeginStoryboard></Trigger.ExitActions>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Trigger.EnterActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="0.93" Duration="0:0:0.08"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="0.93" Duration="0:0:0.08"/>
            </Storyboard></BeginStoryboard></Trigger.EnterActions>
            <Trigger.ExitActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1.05" Duration="0:0:0.18"><DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.8"/></DoubleAnimation.EasingFunction></DoubleAnimation>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1.05" Duration="0:0:0.18"><DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.8"/></DoubleAnimation.EasingFunction></DoubleAnimation>
            </Storyboard></BeginStoryboard></Trigger.ExitActions>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key="WizIcon" TargetType="Button">
      <Setter Property="Cursor" Value="Hand"/><Setter Property="Focusable" Value="False"/><Setter Property="Margin" Value="2,6"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
        <Border x:Name="bd" Background="Transparent" RenderTransformOrigin="0.5,0.5">
          <Border.RenderTransform><ScaleTransform x:Name="sc" ScaleX="1" ScaleY="1"/></Border.RenderTransform>
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Trigger.EnterActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1.1" Duration="0:0:0.16"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1.1" Duration="0:0:0.16"/>
            </Storyboard></BeginStoryboard></Trigger.EnterActions>
            <Trigger.ExitActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1" Duration="0:0:0.2"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1" Duration="0:0:0.2"/>
            </Storyboard></BeginStoryboard></Trigger.ExitActions>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Trigger.EnterActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="0.9" Duration="0:0:0.08"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="0.9" Duration="0:0:0.08"/>
            </Storyboard></BeginStoryboard></Trigger.EnterActions>
            <Trigger.ExitActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1.1" Duration="0:0:0.2"><DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.9"/></DoubleAnimation.EasingFunction></DoubleAnimation>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1.1" Duration="0:0:0.2"><DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.9"/></DoubleAnimation.EasingFunction></DoubleAnimation>
            </Storyboard></BeginStoryboard></Trigger.ExitActions>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key="WizText" TargetType="TextBox">
      <Setter Property="Background" Value="#14171B"/><Setter Property="Foreground" Value="#F2F4FF"/><Setter Property="CaretBrush" Value="#4FD1FF"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
        <Border x:Name="bd" Background="{TemplateBinding Background}" BorderBrush="#2B3038" BorderThickness="1.5" CornerRadius="14" Padding="10,0">
          <ScrollViewer x:Name="PART_ContentHost" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#2F6FB0"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
  </Window.Resources>
  <Grid>
    <Grid.RowDefinitions><RowDefinition Height="36"/><RowDefinition Height="*"/><RowDefinition Height="46"/></Grid.RowDefinitions>
    <DockPanel Grid.Row="0">
      <Button x:Name="wBtnClose" DockPanel.Dock="Right" Style="{StaticResource TitleClose}" Content="&#x2715;" shell:WindowChrome.IsHitTestVisibleInChrome="True"/>
      <TextBlock Text="Rock Toolbox" Margin="14,0" VerticalAlignment="Center" Foreground="#9AA0B8" FontSize="12"/>
    </DockPanel>
    <Grid Grid.Row="1" Margin="34,0">

      <StackPanel x:Name="pgHi" VerticalAlignment="Center" HorizontalAlignment="Center" Visibility="Collapsed">
        <TextBlock x:Name="wHi" Tag="pop" Text="Hi" FontSize="110" FontWeight="Bold" HorizontalAlignment="Center">
          <TextBlock.Foreground>
              <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                <GradientStop Color="#FF9F0A" Offset="0"/><GradientStop Color="#FF375F" Offset="0.5"/><GradientStop Color="#BF5AF2" Offset="1"/>
              </LinearGradientBrush>
            </TextBlock.Foreground>
        </TextBlock>
        <Border x:Name="wHiLine" Width="72" Height="5" CornerRadius="3" Background="#FF375F" Margin="0,8,0,0"/>
      </StackPanel>

      <StackPanel x:Name="pgWelcome" VerticalAlignment="Center" HorizontalAlignment="Center" Visibility="Collapsed">
        <Grid x:Name="wGreetHost" Width="440" Height="92" HorizontalAlignment="Center" Background="Transparent" Cursor="Hand" ToolTip="Click to skip">
          <TextBlock x:Name="wGreetA" FontSize="46" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center" Opacity="0">
            <TextBlock.Foreground>
              <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                <GradientStop Color="#FF9F0A" Offset="0"/><GradientStop Color="#FF375F" Offset="0.5"/><GradientStop Color="#BF5AF2" Offset="1"/>
              </LinearGradientBrush>
            </TextBlock.Foreground>
          </TextBlock>
          <TextBlock x:Name="wGreetB" FontSize="46" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center" Opacity="0">
            <TextBlock.Foreground>
              <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                <GradientStop Color="#FF9F0A" Offset="0"/><GradientStop Color="#FF375F" Offset="0.5"/><GradientStop Color="#BF5AF2" Offset="1"/>
              </LinearGradientBrush>
            </TextBlock.Foreground>
          </TextBlock>
        </Grid>
        <TextBlock x:Name="wSub" Text="a new version from Rock Toolbox" FontSize="16" Foreground="#9AA0B8" HorizontalAlignment="Center" Margin="0,6,0,0"/>
        <Border x:Name="wVerPill" Background="#1E3547" CornerRadius="16" Padding="22,6" Margin="0,22,0,0" HorizontalAlignment="Center">
          <TextBlock x:Name="wVer" Text="V3.0.0" FontFamily="Consolas" FontSize="16" FontWeight="SemiBold" Foreground="#4FD1FF"/>
        </Border>
        <Grid x:Name="wStartHost" HorizontalAlignment="Center" Margin="0,46,0,0">
          <Border x:Name="wStartGlow" CornerRadius="26" Background="#FF375F" Opacity="0" IsHitTestVisible="False"/>
          <Button x:Name="wBtnStart" Style="{StaticResource WizBtn}" Content="Start"/>
        </Grid>
      </StackPanel>

      <StackPanel x:Name="pgLang" VerticalAlignment="Center" HorizontalAlignment="Center" Width="380" Visibility="Collapsed">
          <TextBlock Text="Choose your language" FontSize="26" FontWeight="Bold" Foreground="#F2F4FF" HorizontalAlignment="Center"/>
          <TextBlock Text="You can change it any time from the gear menu" FontSize="13" Foreground="#9AA0B8" HorizontalAlignment="Center" Margin="0,6,0,0"/>
          <UniformGrid x:Name="wLangGrid" Columns="4" Margin="0,22,0,0"/>
          <StackPanel Margin="0,8,0,0" MinHeight="56">
            <TextBlock x:Name="wLangStatus" Text="" FontSize="12" Foreground="#9AA0B8" HorizontalAlignment="Center" TextAlignment="Center" TextWrapping="Wrap"/>
            <ProgressBar x:Name="wLangPb" Height="6" Width="240" Margin="0,8,0,0" IsIndeterminate="True" Visibility="Collapsed" Foreground="#2F6FB0" Background="#2B3038" BorderThickness="0"/>
          </StackPanel>
          <Button x:Name="wBtnLangNext" Style="{StaticResource WizBtn}" Content="Next" Margin="0,6,0,0" HorizontalAlignment="Center"/>
        </StackPanel>

        <StackPanel x:Name="pgProfile" VerticalAlignment="Center" HorizontalAlignment="Center" Width="320" Visibility="Collapsed">
          <TextBlock Text="Who are you?" FontSize="26" FontWeight="Bold" Foreground="#F2F4FF" HorizontalAlignment="Center"/>
          <TextBlock Text="Add a photo and your name" FontSize="13" Foreground="#9AA0B8" HorizontalAlignment="Center" Margin="0,6,0,0"/>
          <Grid x:Name="wAvatarGrid" Tag="pop" Width="112" Height="112" Margin="0,22,0,0" Cursor="Hand" Background="Transparent">
            <Ellipse x:Name="wAvEl" Fill="#B8C0CC"/>
            <Viewbox x:Name="wAvLetter" Stretch="Uniform" Width="112" Height="112" HorizontalAlignment="Center" VerticalAlignment="Center"><Canvas Width="100" Height="100"><Path Fill="#FFFFFF" Opacity="0.95"><Path.Data><GeometryGroup><EllipseGeometry Center="50,38" RadiusX="17" RadiusY="17"/><PathGeometry Figures="M 13.4 84 C 16 64 34 57 50 57 C 66 57 84 64 86.6 84 A 50 50 0 0 1 13.4 84 Z"/></GeometryGroup></Path.Data></Path></Canvas></Viewbox>
            <Border Width="34" Height="34" CornerRadius="17" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" HorizontalAlignment="Right" VerticalAlignment="Bottom">
              <TextBlock Text="&#xE722;" FontFamily="Segoe MDL2 Assets" FontSize="16" Foreground="#2F6FB0" HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
          </Grid>
          <TextBlock Text="Click the photo to choose one (optional)" FontSize="12" Foreground="#6B7186" HorizontalAlignment="Center" Margin="0,12,0,16"/>
          <TextBox x:Name="wName" Style="{StaticResource WizText}" Width="280" Height="44" FontSize="16" MaxLength="24" TextAlignment="Center" VerticalContentAlignment="Center"/>
          <TextBlock x:Name="wNameErr" Text="" FontSize="12" Foreground="#FF6B6B" HorizontalAlignment="Center" Margin="0,8,0,8" MinHeight="18"/>
          <Button x:Name="wBtnProfNext" Style="{StaticResource WizBtn}" Content="Next" HorizontalAlignment="Center"/>
        </StackPanel>

        <StackPanel x:Name="pgDone" VerticalAlignment="Center" HorizontalAlignment="Center" Width="360" Visibility="Collapsed">
          <Grid Tag="pop" Width="108" Height="108" HorizontalAlignment="Center">
            <Ellipse x:Name="wDoneRing" Stroke="#4FD1FF" StrokeThickness="3" Opacity="0" IsHitTestVisible="False"/>
            <Ellipse x:Name="wDoneAv" Fill="#B8C0CC" Margin="6"/>
            <Viewbox x:Name="wDoneLetter" Stretch="Uniform" Width="96" Height="96" HorizontalAlignment="Center" VerticalAlignment="Center"><Canvas Width="100" Height="100"><Path Fill="#FFFFFF" Opacity="0.95"><Path.Data><GeometryGroup><EllipseGeometry Center="50,38" RadiusX="17" RadiusY="17"/><PathGeometry Figures="M 13.4 84 C 16 64 34 57 50 57 C 66 57 84 64 86.6 84 A 50 50 0 0 1 13.4 84 Z"/></GeometryGroup></Path.Data></Path></Canvas></Viewbox>
          </Grid>
          <TextBlock x:Name="wDoneTitle" Text="Welcome" FontSize="30" FontWeight="Bold" Foreground="#F2F4FF" HorizontalAlignment="Center" Margin="0,18,0,0" TextTrimming="CharacterEllipsis"/>
          <TextBlock Text="Your toolbox is ready." FontSize="15" Foreground="#C9CDE0" HorizontalAlignment="Center" Margin="0,10,0,0"/>
          <TextBlock Text="Install apps, tune Windows and clean up in a few clicks." FontSize="13" Foreground="#9AA0B8" HorizontalAlignment="Center" TextAlignment="Center" TextWrapping="Wrap" Margin="0,4,0,0"/>
          <TextBlock Text="Tip: change the language, theme and your profile from the gear menu." FontSize="12" Foreground="#6B7186" HorizontalAlignment="Center" TextAlignment="Center" TextWrapping="Wrap" Margin="0,14,0,0"/>
          <Button x:Name="wBtnFinish" Style="{StaticResource WizBtn}" Content="Finish" Margin="0,32,0,0" HorizontalAlignment="Center"/>
        </StackPanel>

        <StackPanel x:Name="pgRestart" VerticalAlignment="Center" HorizontalAlignment="Center" Visibility="Collapsed">
          <Ellipse x:Name="wSpin" Width="44" Height="44" Stroke="#4FD1FF" StrokeThickness="4" StrokeDashArray="5 4" HorizontalAlignment="Center"/>
          <TextBlock Text="Restarting Rock Toolbox" FontSize="20" FontWeight="Bold" Foreground="#F2F4FF" HorizontalAlignment="Center" Margin="0,20,0,0"/>
          <TextBlock Text="Applying your settings..." FontSize="13" Foreground="#9AA0B8" HorizontalAlignment="Center" Margin="0,6,0,0"/>
          <ProgressBar x:Name="wRestartPb" Minimum="0" Maximum="100" Value="0" Height="6" Width="250" Margin="0,18,0,0" Foreground="#2F6FB0" Background="#2B3038" BorderThickness="0"/>
        </StackPanel>
      
    </Grid>
    <StackPanel x:Name="spDots" Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center"/>
  </Grid>
</Window>
'@

function New-WzDur([int]$Ms) { return (New-Object System.Windows.Duration([TimeSpan]::FromMilliseconds($Ms))) }
function Wz-Brush([string]$Hex) {
    $k = $Hex.TrimStart('#').ToUpper()
    $h = $Hex
    if ($script:Pal.ContainsKey($k)) { $h = [string]$script:Pal[$k] }
    return (New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($h)))
}
function Wz-FadeUp($El, [int]$DelayMs = 0, [double]$FromY = 18) {
    $tt = New-Object System.Windows.Media.TranslateTransform(0, $FromY)
    $El.RenderTransform = $tt
    $ease = New-Object System.Windows.Media.Animation.CubicEase; $ease.EasingMode = 'EaseOut'
    $bt = [TimeSpan]::FromMilliseconds($DelayMs)
    $ay = New-Object System.Windows.Media.Animation.DoubleAnimation($FromY, 0.0, (New-WzDur 520)); $ay.EasingFunction = $ease; $ay.BeginTime = $bt
    $af = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, (New-WzDur 520)); $af.BeginTime = $bt
    $El.Opacity = 0
    $tt.BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $ay)
    $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $af)
}
function Wz-Pop($El, [int]$DelayMs = 0) {
    $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $sc = New-Object System.Windows.Media.ScaleTransform(0.4, 0.4)
    $El.RenderTransform = $sc
    $ease = New-Object System.Windows.Media.Animation.BackEase; $ease.EasingMode = 'EaseOut'; $ease.Amplitude = 0.7
    $bt = [TimeSpan]::FromMilliseconds($DelayMs)
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation(0.4, 1.0, (New-WzDur 650)); $a.EasingFunction = $ease; $a.BeginTime = $bt
    $f = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, (New-WzDur 320)); $f.BeginTime = $bt
    $El.Opacity = 0
    $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $a)
    $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $a)
    $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $f)
}
function Wz-Ring($El, [int]$Times = 2, [double]$From = 0.92, [double]$To = 1.3, [double]$Op = 0.8, [int]$DelayMs = 0) {
    $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $sc = New-Object System.Windows.Media.ScaleTransform($From, $From)
    $El.RenderTransform = $sc
    if ($Times -le 0) { $rep = [System.Windows.Media.Animation.RepeatBehavior]::Forever }
    else { $rep = New-Object System.Windows.Media.Animation.RepeatBehavior([double]$Times) }
    $bt = [TimeSpan]::FromMilliseconds($DelayMs)
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation($From, $To, (New-WzDur 1300)); $a.RepeatBehavior = $rep; $a.BeginTime = $bt
    $o = New-Object System.Windows.Media.Animation.DoubleAnimation($Op, 0.0, (New-WzDur 1300)); $o.RepeatBehavior = $rep; $o.BeginTime = $bt
    $El.Opacity = 0
    $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $a)
    $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $a)
    $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $o)
}
function Wz-Bounce($El) {
    $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $sc = New-Object System.Windows.Media.ScaleTransform(0.78, 0.78)
    $El.RenderTransform = $sc
    $ease = New-Object System.Windows.Media.Animation.BackEase; $ease.EasingMode = 'EaseOut'; $ease.Amplitude = 1.1
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation(0.78, 1.0, (New-WzDur 480)); $a.EasingFunction = $ease
    $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $a)
    $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $a)
}
function Wz-Spin($El) {
    $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $rt = New-Object System.Windows.Media.RotateTransform(0)
    $El.RenderTransform = $rt
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 360.0, (New-WzDur 1100))
    $a.RepeatBehavior = [System.Windows.Media.Animation.RepeatBehavior]::Forever
    $rt.BeginAnimation([System.Windows.Media.RotateTransform]::AngleProperty, $a)
}

function Update-WizAvatars {
    $nm = ([string]$script:Wz['wName'].Text).Trim()
    $ch = 'A'
    if ($nm) { $ch = $nm.Substring(0, 1).ToUpper() }
    foreach ($pair in @(@('wAvEl', 'wAvLetter'), @('wDoneAv', 'wDoneLetter'))) {
        $el = $script:Wz[$pair[0]]; $tb = $script:Wz[$pair[1]]
        if ($script:WizBitmap) {
            $ib = New-Object System.Windows.Media.ImageBrush($script:WizBitmap); $ib.Stretch = 'UniformToFill'
            $el.Fill = $ib; $tb.Visibility = 'Collapsed'
        } else {
            $el.Fill = Wz-Brush '#B8C0CC'; $tb.Visibility = 'Visible'
        }
    }
}

function Update-WizDots([string]$Page) {
    $idx = @{ Welcome = 0; Lang = 1; Profile = 2; Done = 3 }
    $sp = $script:Wz['spDots']
    if ($script:WizMode -ne 'Full' -or -not $idx.ContainsKey($Page)) { $sp.Visibility = 'Collapsed'; return }
    $sp.Visibility = 'Visible'
    if ($sp.Children.Count -eq 0) {
        for ($i = 0; $i -lt 4; $i++) {
            $d = New-Object System.Windows.Controls.Border
            $d.Height = 8; $d.Width = 8; $d.Margin = '4,0'; $d.CornerRadius = New-Object System.Windows.CornerRadius 4
            [void]$sp.Children.Add($d)
        }
    }
    for ($i = 0; $i -lt 4; $i++) {
        $d = $sp.Children[$i]
        if ($i -eq $idx[$Page]) {
            $d.Background = Wz-Brush '#FF375F'
            $wa = New-Object System.Windows.Media.Animation.DoubleAnimation(8.0, 24.0, (New-WzDur 280))
            $d.BeginAnimation([System.Windows.FrameworkElement]::WidthProperty, $wa)
        } else {
            $d.BeginAnimation([System.Windows.FrameworkElement]::WidthProperty, $null)
            $d.Width = 8; $d.Background = Wz-Brush '#2B3038'
        }
    }
}

# ---- iOS-style greeting: "Welcome" in many languages, each one fading/scaling in over the last (cross-fade), landing on Welcome ----
$script:WzGreets = @('أهلاً بك', 'Bienvenue', 'Bienvenido', 'Willkommen', '欢迎', 'ようこそ', 'Welcome')
$script:WzGreetIdx = 0
$script:WzGreetFlip = $false
$script:WzGreetDone = $false
$script:WzGreetLast = ''
$script:WzGreetTimer = $null
function Wz-GreetPrep($Tb) {
    $Tb.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $g = New-Object System.Windows.Media.TransformGroup
    [void]$g.Children.Add((New-Object System.Windows.Media.ScaleTransform(1, 1)))
    [void]$g.Children.Add((New-Object System.Windows.Media.TranslateTransform(0, 0)))
    $Tb.RenderTransform = $g
    $Tb.Opacity = 0
}
function Wz-GreetShow([string]$Text) {
    $a = $script:Wz['wGreetA']; $b = $script:Wz['wGreetB']
    if ($script:WzGreetFlip) { $inTb = $b; $outTb = $a } else { $inTb = $a; $outTb = $b }
    $first = ($script:WzGreetLast -eq '')
    $script:WzGreetFlip = -not $script:WzGreetFlip
    $script:WzGreetLast = $Text
    $inTb.Text = $Text
    $ease = New-Object System.Windows.Media.Animation.CubicEase; $ease.EasingMode = 'EaseOut'
    $gi = $inTb.RenderTransform
    $sa = New-Object System.Windows.Media.Animation.DoubleAnimation(0.86, 1.0, (New-WzDur 440)); $sa.EasingFunction = $ease
    $ya = New-Object System.Windows.Media.Animation.DoubleAnimation(18.0, 0.0, (New-WzDur 440)); $ya.EasingFunction = $ease
    $oa = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, (New-WzDur 380))
    $gi.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $sa)
    $gi.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $sa)
    $gi.Children[1].BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $ya)
    $inTb.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $oa)
    if (-not $first) {
        $go = $outTb.RenderTransform
        $sb = New-Object System.Windows.Media.Animation.DoubleAnimation(1.0, 1.14, (New-WzDur 340)); $sb.EasingFunction = $ease
        $yb = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, -12.0, (New-WzDur 340)); $yb.EasingFunction = $ease
        $ob = New-Object System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, (New-WzDur 300))
        $go.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $sb)
        $go.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $sb)
        $go.Children[1].BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $yb)
        $outTb.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $ob)
    }
}
function Finish-WizGreet {
    if ($script:WzGreetDone) { return }
    $script:WzGreetDone = $true
    try { if ($script:WzGreetTimer) { $script:WzGreetTimer.Stop() } } catch { }
    if ($script:WzGreetLast -ne 'Welcome') { Wz-GreetShow 'Welcome' }
    Wz-FadeUp $script:Wz['wSub'] 120
    Wz-Pop $script:Wz['wVerPill'] 260
    Wz-FadeUp $script:Wz['wStartHost'] 420
    Wz-Ring $script:Wz['wStartGlow'] 0 1.0 1.3 0.55 1400
}
function Start-WizGreet {
    $script:WzGreetIdx = 0; $script:WzGreetFlip = $false; $script:WzGreetDone = $false; $script:WzGreetLast = ''
    Wz-GreetPrep $script:Wz['wGreetA']; Wz-GreetPrep $script:Wz['wGreetB']
    Wz-GreetShow $script:WzGreets[0]; $script:WzGreetIdx = 1
    $t = New-Object System.Windows.Threading.DispatcherTimer
    $t.Interval = [TimeSpan]::FromMilliseconds(560)
    $t.Add_Tick({
        if ($script:WzGreetIdx -ge $script:WzGreets.Count) { Finish-WizGreet; return }
        Wz-GreetShow $script:WzGreets[$script:WzGreetIdx]
        $script:WzGreetIdx++
        if ($script:WzGreetIdx -ge $script:WzGreets.Count) { Finish-WizGreet }
    })
    $script:WzGreetTimer = $t
    $t.Start()
}

function Show-WizPage([string]$Name) {
    foreach ($p in 'Hi', 'Welcome', 'Lang', 'Profile', 'Done', 'Restart') { $script:Wz['pg' + $p].Visibility = 'Collapsed' }
    $pg = $script:Wz['pg' + $Name]
    $pg.Visibility = 'Visible'
    $i = 0
    if ($Name -eq 'Welcome') {
        $kids = @($pg.Children)
        for ($k = 1; $k -lt $kids.Count; $k++) { $kids[$k].Opacity = 0 }
        Start-WizGreet
    } else {
        foreach ($c in @($pg.Children)) {
            if ([string]$c.Tag -eq 'pop') { Wz-Pop $c (80 + $i * 110) } else { Wz-FadeUp $c (80 + $i * 110) }
            $i++
        }
    }
    if ($Name -eq 'Hi') { Wz-FadeUp $script:Wz['wHiLine'] 520 8 }
    try { $script:Wz['wStartGlow'].BeginAnimation([System.Windows.UIElement]::OpacityProperty, $null); $script:Wz['wStartGlow'].Opacity = 0 } catch { }
    if ($Name -eq 'Lang') {
        $ci = 0
        foreach ($b in @($script:Wz['wLangGrid'].Children)) { Wz-Pop $b (260 + $ci * 55); $ci++ }
    }
    if ($Name -eq 'Done') { Wz-Ring $script:Wz['wDoneRing'] 3 }
    if ($Name -eq 'Restart') { Wz-Spin $script:Wz['wSpin'] }
    Update-WizDots $Name
}

function Set-WizLangChips {
    foreach ($b in @($script:Wz['wLangGrid'].Children)) {
        $g = $b.Content.Children[0]; $ic = $g.Children[0]; $ck = $g.Children[1]; $lb = $b.Content.Children[1]
        if ([string]$b.Tag -eq $script:WizLang) {
            $ic.BorderBrush = Wz-Brush '#F2F4FF'; $ck.Visibility = 'Visible'; $lb.Foreground = Wz-Brush '#4FD1FF'
        } else {
            $ic.BorderBrush = [System.Windows.Media.Brushes]::Transparent; $ck.Visibility = 'Collapsed'; $lb.Foreground = Wz-Brush '#9AA0B8'
        }
    }
}

function Save-UserProfile([string]$Name, $Bitmap) {
    $script:Settings['UserName'] = $Name.Trim()
    if ($Bitmap) {
        try {
            $png = Join-Path $AppDir 'avatar.png'
            if (-not (Test-Path -LiteralPath $AppDir)) { New-Item -ItemType Directory -Force -Path $AppDir | Out-Null }
            $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
            $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($Bitmap))
            $fs = [System.IO.File]::Create($png)
            try { $enc.Save($fs) } finally { $fs.Dispose() }
            $script:Settings['Avatar'] = $png
        } catch { Log ('Could not save the profile photo: ' + $_.Exception.Message) 'WARN' 'UI' }
    }
    Save-Settings
}
$script:Pe = @{}
$script:PeBitmap = $null
$script:PeWin = $null
function Show-ProfileEditor {
    if ($script:PeWin) { try { $script:PeWin.Activate(); return } catch { $script:PeWin = $null } }
    $res = [regex]::Match($script:WizXaml, '<Window\.Resources>.*?</Window\.Resources>', 'Singleline').Value
    $x = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="Rock Toolbox" Width="420" Height="480" ResizeMode="NoResize" WindowStyle="None" WindowStartupLocation="CenterOwner"
        Background="#1F2329" BorderBrush="#2B3038" BorderThickness="1" FontFamily="Segoe UI" FontSize="13" UseLayoutRounding="True">
  <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight="36" ResizeBorderThickness="0" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/></shell:WindowChrome.WindowChrome>
  $res
  <Grid>
    <Grid.RowDefinitions><RowDefinition Height="36"/><RowDefinition Height="*"/></Grid.RowDefinitions>
    <DockPanel Grid.Row="0">
      <Button x:Name="pBtnClose" DockPanel.Dock="Right" Style="{StaticResource TitleClose}" Content="&#x2715;" shell:WindowChrome.IsHitTestVisibleInChrome="True"/>
      <TextBlock Text="Rock Toolbox" Margin="14,0" VerticalAlignment="Center" Foreground="#9AA0B8" FontSize="12"/>
    </DockPanel>
    <StackPanel Grid.Row="1" VerticalAlignment="Center" HorizontalAlignment="Center" Width="320">
      <TextBlock Text="Your profile" FontSize="24" FontWeight="Bold" Foreground="#F2F4FF" HorizontalAlignment="Center"/>
      <Grid x:Name="pAvatarGrid" Width="104" Height="104" Margin="0,20,0,0" Cursor="Hand" Background="Transparent">
        <Ellipse x:Name="pAvEl" Fill="#B8C0CC"/>
        <Viewbox x:Name="pAvLetter" Stretch="Uniform" Width="104" Height="104" HorizontalAlignment="Center" VerticalAlignment="Center"><Canvas Width="100" Height="100"><Path Fill="#FFFFFF" Opacity="0.95"><Path.Data><GeometryGroup><EllipseGeometry Center="50,38" RadiusX="17" RadiusY="17"/><PathGeometry Figures="M 13.4 84 C 16 64 34 57 50 57 C 66 57 84 64 86.6 84 A 50 50 0 0 1 13.4 84 Z"/></GeometryGroup></Path.Data></Path></Canvas></Viewbox>
        <Border Width="32" Height="32" CornerRadius="16" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" HorizontalAlignment="Right" VerticalAlignment="Bottom">
          <TextBlock Text="&#xE722;" FontFamily="Segoe MDL2 Assets" FontSize="15" Foreground="#2F6FB0" HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
      </Grid>
      <TextBlock Text="Click the photo to choose one (optional)" FontSize="12" Foreground="#6B7186" HorizontalAlignment="Center" Margin="0,10,0,14"/>
      <TextBox x:Name="pName" Style="{StaticResource WizText}" Width="280" Height="42" FontSize="16" MaxLength="24" TextAlignment="Center" VerticalContentAlignment="Center"/>
      <TextBlock x:Name="pErr" Text="" FontSize="12" Foreground="#FF6B6B" HorizontalAlignment="Center" Margin="0,8,0,8" MinHeight="18"/>
      <Button x:Name="pSave" Style="{StaticResource WizBtn}" Content="Save" HorizontalAlignment="Center"/>
    </StackPanel>
  </Grid>
</Window>
"@
    $x = Convert-XamlTheme -Text $x -Literal
    [xml]$xd = $x
    $w = [Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlNodeReader $xd))
    $script:PeWin = $w
    $script:Pe = @{}
    foreach ($n in $xd.SelectNodes('//*[@*[local-name()="Name"]]')) {
        $nm = $n.GetAttribute('Name', 'http://schemas.microsoft.com/winfx/2006/xaml')
        if ($nm) { $script:Pe[$nm] = $w.FindName($nm) }
    }
    $script:PeBitmap = $null
    $ap = [string]$script:Settings['Avatar']
    if ($ap -and (Test-Path -LiteralPath $ap)) { $script:PeBitmap = Load-AvatarBitmap $ap }
    $script:Pe['pName'].Text = (Get-UserDisplayName)
    $paint = {
        $nm = ([string]$script:Pe['pName'].Text).Trim(); $ch = 'A'
        if ($nm) { $ch = $nm.Substring(0, 1).ToUpper() }
        if ($script:PeBitmap) {
            $ib = New-Object System.Windows.Media.ImageBrush($script:PeBitmap); $ib.Stretch = 'UniformToFill'
            $script:Pe['pAvEl'].Fill = $ib; $script:Pe['pAvLetter'].Visibility = 'Collapsed'
        } else { $script:Pe['pAvEl'].Fill = Wz-Brush '#B8C0CC'; $script:Pe['pAvLetter'].Visibility = 'Visible' }
    }
    $script:PePaint = $paint
    & $paint
    $script:Pe['pName'].Add_TextChanged({ $script:Pe['pErr'].Text = ''; & $script:PePaint })
    $script:Pe['pBtnClose'].Add_Click({ $script:PeWin.Close() })
    $script:Pe['pAvatarGrid'].Add_MouseLeftButtonUp({
        try {
            $dlg = New-Object Microsoft.Win32.OpenFileDialog
            $dlg.Filter = 'Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif'; $dlg.Title = 'Choose your photo'
            if ($dlg.ShowDialog($script:PeWin)) {
                $bmp = Load-AvatarBitmap $dlg.FileName
                if ($bmp) { $script:PeBitmap = $bmp; & $script:PePaint }
            }
        } catch { }
    })
    $script:Pe['pSave'].Add_Click({
        $nm = ([string]$script:Pe['pName'].Text).Trim()
        if (-not $nm) { $script:Pe['pErr'].Text = 'Enter your name first'; return }
        Save-UserProfile $nm $script:PeBitmap
        Update-ProfileCard
        $script:PeWin.Close()
    })
    $w.Add_Closed({ $script:PeWin = $null })
    try { if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    try { $w.Owner = $window } catch { }
    $w.Show()     # not modal: a modal window disables the whole big main window, which is what made it freeze
}

function Save-WizProfile {
    $script:Settings['UserName'] = ([string]$script:Wz['wName'].Text).Trim()
    if ($script:WizBitmap) {
        try {
            $png = Join-Path $AppDir 'avatar.png'
            if (-not (Test-Path -LiteralPath $AppDir)) { New-Item -ItemType Directory -Force -Path $AppDir | Out-Null }
            $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
            $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($script:WizBitmap))
            $fs = [System.IO.File]::Create($png)
            try { $enc.Save($fs) } finally { $fs.Dispose() }
            $script:Settings['Avatar'] = $png
        } catch { Log ('Could not save the profile photo: ' + $_.Exception.Message) 'WARN' 'UI' }
    }
}

function Show-WelcomeWizard([string]$Mode = 'Full') {
    $script:WizMode = $Mode; $script:WizResult = $false; $script:WizReplay = $false; $script:WizBusy = $false
    $script:WizLang = [string]$script:Settings['Language']
    $script:WizBitmap = $null
    $ap = [string]$script:Settings['Avatar']
    if ($ap -and (Test-Path -LiteralPath $ap)) { $script:WizBitmap = Load-AvatarBitmap $ap }
    $x = Convert-XamlTheme -Text $script:WizXaml -Literal
    [xml]$xd = $x
    $w = [Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlNodeReader $xd))
    $script:WizWin = $w
    $script:Wz = @{}
    foreach ($n in $xd.SelectNodes('//*[@*[local-name()="Name"]]')) {
        $nm = $n.GetAttribute('Name', 'http://schemas.microsoft.com/winfx/2006/xaml')
        if ($nm) { $script:Wz[$nm] = $w.FindName($nm) }
    }
    $script:Wz['wVer'].Text = [string]$script:RockVersion
    $script:Wz['wName'].Text = (Get-UserDisplayName)

    # language icons (iPhone home-screen style)
    $palette = @('#0A84FF', '#30D158', '#FF9F0A', '#BF5AF2', '#FF453A', '#5AC8FA', '#FF375F', '#5E5CE6', '#FFB800', '#32ADE6', '#AC8E68', '#2DD4BF')
    $ci = 0
    foreach ($k in $script:LangList.Keys) {
        $b = New-Object System.Windows.Controls.Button
        $b.Style = $w.FindResource('WizIcon'); $b.Tag = [string]$k
        $sp = New-Object System.Windows.Controls.StackPanel
        $g = New-Object System.Windows.Controls.Grid; $g.Width = 62; $g.Height = 46
        $ic = New-FlagVisual ([string]$script:LangList[$k].Code) 62 46 3
        $ck = New-Object System.Windows.Controls.Border
        $ck.Width = 22; $ck.Height = 22; $ck.CornerRadius = New-Object System.Windows.CornerRadius 11
        $ck.Background = [System.Windows.Media.Brushes]::White; $ck.HorizontalAlignment = 'Right'; $ck.VerticalAlignment = 'Top'
        $ck.Margin = '0,-5,-5,0'; $ck.Visibility = 'Collapsed'
        $ct = New-Object System.Windows.Controls.TextBlock
        $ct.Text = [string][char]0x2713; $ct.FontSize = 13; $ct.FontWeight = 'Bold'; $ct.Foreground = Wz-Brush '#2F6FB0'
        $ct.HorizontalAlignment = 'Center'; $ct.VerticalAlignment = 'Center'
        $ck.Child = $ct
        [void]$g.Children.Add($ic); [void]$g.Children.Add($ck)
        $lb = New-Object System.Windows.Controls.TextBlock
        $lb.Text = [string]$script:LangList[$k].Native; $lb.FontSize = 12; $lb.Margin = '0,7,0,0'
        $lb.HorizontalAlignment = 'Center'; $lb.TextTrimming = 'CharacterEllipsis'; $lb.MaxWidth = 84
        [void]$sp.Children.Add($g); [void]$sp.Children.Add($lb)
        $b.Content = $sp
        $b.Add_Click({ param($s, $e)
            if ($script:WizBusy) { return }
            $script:WizLang = [string]$s.Tag; Set-WizLangChips
            Wz-Bounce $s.Content.Children[0]
            $script:Wz['wLangStatus'].Text = ''
        })
        [void]$script:Wz['wLangGrid'].Children.Add($b)
        $ci++
    }
    Set-WizLangChips
    Update-WizAvatars

    $script:Wz['wName'].Add_TextChanged({ $script:Wz['wNameErr'].Text = ''; Update-WizAvatars })
    $script:Wz['wBtnClose'].Add_Click({ $script:WizWin.Close() })

    $script:Wz['wBtnStart'].Add_Click({ Show-WizPage 'Lang' })
    $script:Wz['wGreetHost'].Add_MouseLeftButtonUp({ if (-not $script:WzGreetDone) { Finish-WizGreet } })

    $script:Wz['wBtnLangNext'].Add_Click({
        if ($script:WizBusy) { return }
        $lang = $script:WizLang
        if ($lang -ne 'English' -and -not (Test-LangPack $lang)) {
            $script:WizBusy = $true
            $script:Wz['wLangStatus'].Text = 'Downloading the ' + $lang + ' language pack from the official source...'
            $script:Wz['wLangPb'].Visibility = 'Visible'
            $ok = $false
            try { $ok = [bool](Get-LangPack $lang) } catch { $ok = $false }
            $script:Wz['wLangPb'].Visibility = 'Collapsed'
            $script:WizBusy = $false
            if (-not $ok) {
                $why = [string]$script:LastLangError
                if ($why.Length -gt 110) { $why = $why.Substring(0, 110) + '...' }
                $script:Wz['wLangStatus'].Text = 'Could not download the ' + $lang + ' pack. Check your internet connection and try again, or choose another language.' + $(if ($why) { "`n(" + $why + ')' } else { '' })
                return
            }
        }
        $script:Wz['wLangStatus'].Text = ''
        Show-WizPage 'Profile'
    })

    $script:Wz['wAvatarGrid'].Add_MouseLeftButtonUp({
        try {
            $dlg = New-Object Microsoft.Win32.OpenFileDialog
            $dlg.Filter = 'Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif'
            $dlg.Title = 'Choose your photo'
            if ($dlg.ShowDialog($script:WizWin)) {
                $bmp = Load-AvatarBitmap $dlg.FileName
                if ($bmp) { $script:WizBitmap = $bmp; Update-WizAvatars; Wz-Pop $script:Wz['wAvatarGrid'] 0 }
            }
        } catch { }
    })

    $script:Wz['wBtnProfNext'].Add_Click({
        $nm = ([string]$script:Wz['wName'].Text).Trim()
        if (-not $nm) { $script:Wz['wNameErr'].Text = 'Enter your name first'; return }
        if ($script:WizMode -eq 'Profile') {
            Save-WizProfile; Save-Settings
            $script:WizResult = $false
            $script:WizWin.Close()
            return
        }
        $script:Wz['wDoneTitle'].Text = 'Welcome, ' + $nm
        Update-WizAvatars
        Show-WizPage 'Done'
    })

    $script:Wz['wBtnFinish'].Add_Click({
        if ($script:WizBusy) { return }
        $script:WizBusy = $true
        Save-WizProfile
        $script:Settings['Language'] = $script:WizLang
        $script:Settings['WelcomeSeen'] = $script:WelcomeVersion
        Save-Settings
        Show-WizPage 'Restart'
        for ($i = 0; $i -le 100; $i += 4) { $script:Wz['wRestartPb'].Value = $i; Pump-Wait 45 }
        Restart-Rock
        $script:WizResult = $true
        $script:WizWin.Close()
    })

    $w.Add_Loaded({
        # wait until WPF has finished laying out and drawing the window, then start the animations (smooth from the first frame)
        [void]$script:WizWin.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::ContextIdle, [Action]{
            if ($script:WizMode -eq 'Profile') {
                $script:Wz['wBtnProfNext'].Content = 'Save'
                Show-WizPage 'Profile'
            } else {
                Show-WizPage 'Hi'
                $t = New-Object System.Windows.Threading.DispatcherTimer
                $t.Interval = [TimeSpan]::FromMilliseconds(1500)
                $t.Add_Tick({ param($ts, $te) $ts.Stop(); if ($script:Wz['pgHi'].Visibility -eq 'Visible') { Show-WizPage 'Welcome' } })
                $script:WizHiTimer = $t
                $t.Start()
            }
        })
    })
    try { if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    if ($Mode -eq 'Profile') { try { $w.Owner = $window; $w.WindowStartupLocation = 'CenterOwner' } catch { } }
    [void]$w.ShowDialog()
    try { if ($script:WizHiTimer) { $script:WizHiTimer.Stop() } } catch { }
    try { if ($script:WzGreetTimer) { $script:WzGreetTimer.Stop() } } catch { }
    return [bool]$script:WizResult
}

# first run of this version: show the welcome tour before the main window appears
if ([string]$script:Settings['WelcomeSeen'] -ne $script:WelcomeVersion) {
    # pause every background timer (icon loader, update checks...) so the welcome animations get the whole UI thread
    $wizPaused = @()
    foreach ($tm in @($script:IconTimer, $script:PrereqBoot, $script:UpdBoot, $script:AppBuildTimer)) {
        if ($tm -and $tm.IsEnabled) { $tm.Stop(); $wizPaused += $tm }
    }
    $wizDone = $false
    try { $wizDone = (@(Show-WelcomeWizard 'Full') -contains $true) } catch { Log ('Welcome screen error: ' + $_.Exception.Message) 'WARN' 'UI' }
    if ($wizDone) { exit }
    foreach ($tm in $wizPaused) { try { $tm.Start() } catch { } }
}

[void]$window.ShowDialog()


