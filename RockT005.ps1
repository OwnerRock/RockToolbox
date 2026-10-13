# ------------------------------------------------------------------
$xamlText = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Rock Toolbox" Width="1200" Height="780" MinWidth="1100" MinHeight="650"
        WindowStartupLocation="CenterScreen" Background="#1F2329" FontFamily="Segoe UI" FontSize="13">
  <Window.Resources>
    <Style TargetType="Button">
      <Setter Property="Background" Value="#1E3547"/>
      <Setter Property="BorderBrush" Value="#35506A"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Foreground" Value="#F0F4FF"/>
      <Setter Property="Padding" Value="12,6"/>
      <Setter Property="Margin" Value="3"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="3" Padding="{TemplateBinding Padding}"
                    RenderTransformOrigin="0.5,0.5">
              <Border.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Border.RenderTransform>
              <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="bd" Property="Opacity" Value="0.85"/>
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1.04" Duration="0:0:0.12">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1.04" Duration="0:0:0.12">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.16">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.16">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="bd" Property="Opacity" Value="0.65"/>
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="0.95" Duration="0:0:0.07"/><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="0.95" Duration="0:0:0.07"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.12"/><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.12"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False"><Setter TargetName="bd" Property="Opacity" Value="0.4"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType="CheckBox">
      <Setter Property="Foreground" Value="#E6E8F2"/>
      <Setter Property="Margin" Value="6,3"/>
      <Setter Property="VerticalContentAlignment" Value="Center"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="CheckBox">
            <StackPanel x:Name="root" Orientation="Horizontal" Background="Transparent">
              <Grid x:Name="box" Width="18" Height="18" VerticalAlignment="Center" RenderTransformOrigin="0.5,0.5">
                <Grid.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Grid.RenderTransform>
                <Border x:Name="boxBorder" CornerRadius="4" BorderThickness="1.5" BorderBrush="#9AA0B8" Background="#14171B"/>
                <Border x:Name="boxFill" CornerRadius="4" Background="#2F8BFF" Opacity="0"/>
                <Path x:Name="tick" Data="M 4.2,9.4 L 7.6,12.8 L 13.8,5.4" Stroke="#FFFFFF" StrokeThickness="2.2"
                      StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" RenderTransformOrigin="0.5,0.5" Opacity="0">
                  <Path.RenderTransform><ScaleTransform ScaleX="0.3" ScaleY="0.3"/></Path.RenderTransform>
                </Path>
              </Grid>
              <ContentPresenter x:Name="cp" Margin="8,0,0,0" VerticalAlignment="Center"/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property="HasContent" Value="False"><Setter TargetName="cp" Property="Margin" Value="0"/></Trigger>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="boxBorder" Property="BorderBrush" Value="#4FD1FF"/>
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="box" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1.12" Duration="0:0:0.12"><DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction></DoubleAnimation><DoubleAnimation Storyboard.TargetName="box" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1.12" Duration="0:0:0.12"><DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction></DoubleAnimation></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="box" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.16"><DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction></DoubleAnimation><DoubleAnimation Storyboard.TargetName="box" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.16"><DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction></DoubleAnimation></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="box" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="0.86" Duration="0:0:0.07"></DoubleAnimation><DoubleAnimation Storyboard.TargetName="box" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="0.86" Duration="0:0:0.07"></DoubleAnimation></Storyboard></BeginStoryboard></Trigger.EnterActions>
              </Trigger>
              <Trigger Property="IsChecked" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="boxFill" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.16"></DoubleAnimation><DoubleAnimation Storyboard.TargetName="tick" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.1"></DoubleAnimation><DoubleAnimation Storyboard.TargetName="tick" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.3"><DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.7"/></DoubleAnimation.EasingFunction></DoubleAnimation><DoubleAnimation Storyboard.TargetName="tick" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.3"><DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.7"/></DoubleAnimation.EasingFunction></DoubleAnimation></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="boxFill" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.16"></DoubleAnimation><DoubleAnimation Storyboard.TargetName="tick" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.1"></DoubleAnimation><DoubleAnimation Storyboard.TargetName="tick" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="0.3" Duration="0:0:0.12"></DoubleAnimation><DoubleAnimation Storyboard.TargetName="tick" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="0.3" Duration="0:0:0.12"></DoubleAnimation></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False"><Setter TargetName="root" Property="Opacity" Value="0.45"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key="Switch" TargetType="CheckBox">
      <Setter Property="Foreground" Value="#E6E8F2"/>
      <Setter Property="Margin" Value="4,5"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="CheckBox">
            <StackPanel Orientation="Horizontal" Background="Transparent">
              <Border x:Name="track" Width="42" Height="21" CornerRadius="10.5" Background="#3A3F4B" Margin="0,0,10,0">
                <Grid>
                  <Border x:Name="trackOn" CornerRadius="10.5" Background="#2F8BFF" Opacity="0"/>
                  <Ellipse x:Name="thumb" Width="15" Height="15" Fill="White" HorizontalAlignment="Left" Margin="3,0,0,0">
                    <Ellipse.RenderTransform><TranslateTransform X="0"/></Ellipse.RenderTransform>
                  </Ellipse>
                </Grid>
              </Border>
              <ContentPresenter VerticalAlignment="Center"/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property="IsChecked" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="thumb" Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)" To="21" Duration="0:0:0.2">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation><DoubleAnimation Storyboard.TargetName="trackOn" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="thumb" Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)" To="0" Duration="0:0:0.2">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation><DoubleAnimation Storyboard.TargetName="trackOn" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType="ListViewItem">
      <Setter Property="Foreground" Value="#E6E8F2"/>
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
      <Setter Property="VerticalContentAlignment" Value="Center"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ListViewItem">
            <Border x:Name="bd" Background="{TemplateBinding Background}" Padding="2,3">
              <GridViewRowPresenter Content="{TemplateBinding Content}" Columns="{TemplateBinding GridView.ColumnCollection}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
            </Border>
            <ControlTemplate.Triggers>
              <EventTrigger RoutedEvent="FrameworkElement.Loaded">
                <BeginStoryboard><Storyboard>
                  <DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" From="0" To="1" Duration="0:0:0.3"/>
                </Storyboard></BeginStoryboard>
              </EventTrigger>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#262B33"/></Trigger>
              <Trigger Property="IsSelected" Value="True">
                <Setter TargetName="bd" Property="Background" Value="#4A78B0"/>
                <Setter Property="Foreground" Value="#FFFFFF"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType="ListView">
      <Setter Property="VirtualizingPanel.ScrollUnit" Value="Pixel"/>
    </Style>

    <Style x:Key="StripeBar" TargetType="ProgressBar">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ProgressBar">
            <Grid x:Name="TemplateRoot" SnapsToDevicePixels="True">
              <Border x:Name="PART_Track" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="3"/>
              <Grid x:Name="PART_Indicator" ClipToBounds="True" HorizontalAlignment="Left">
                <Border Background="{TemplateBinding Foreground}" CornerRadius="3"/>
                <Rectangle x:Name="Stripes" Margin="-16,0,-16,0">
                  <Rectangle.RenderTransform><TranslateTransform x:Name="stripeShift" X="0"/></Rectangle.RenderTransform>
                  <Rectangle.Fill>
                    <DrawingBrush TileMode="Tile" Viewport="0,0,16,16" ViewportUnits="Absolute" Viewbox="0,0,16,16" ViewboxUnits="Absolute">
                      <DrawingBrush.Drawing>
                        <GeometryDrawing Brush="#40FFFFFF" Geometry="M0,0 L8,0 L0,8 Z M16,0 L16,8 L8,16 L0,16 Z"/>
                      </DrawingBrush.Drawing>
                    </DrawingBrush>
                  </Rectangle.Fill>
                </Rectangle>
              </Grid>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property="Tag" Value="busy">
                <Trigger.EnterActions>
                  <BeginStoryboard x:Name="sbMove">
                    <Storyboard RepeatBehavior="Forever">
                      <DoubleAnimation Storyboard.TargetName="stripeShift" Storyboard.TargetProperty="X" From="0" To="16" Duration="0:0:0.55"/>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions><StopStoryboard BeginStoryboardName="sbMove"/></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key="RockSlider" TargetType="Slider">
      <Setter Property="Height" Value="26"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Slider">
            <Grid x:Name="root" Background="Transparent">
              <Border Height="6" CornerRadius="3" Background="#2B3038" VerticalAlignment="Center"/>
              <Track x:Name="PART_Track" VerticalAlignment="Center">
                <Track.DecreaseRepeatButton>
                  <RepeatButton Command="Slider.DecreaseLarge" Focusable="False" IsTabStop="False">
                    <RepeatButton.Template>
                      <ControlTemplate TargetType="RepeatButton"><Border Height="6" CornerRadius="3" Background="#4FD1FF"/></ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton>
                  <RepeatButton Command="Slider.IncreaseLarge" Focusable="False" IsTabStop="False">
                    <RepeatButton.Template>
                      <ControlTemplate TargetType="RepeatButton"><Border Height="6" Background="Transparent"/></ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.IncreaseRepeatButton>
                <Track.Thumb>
                  <Thumb Focusable="False" Width="22" Height="22">
                    <Thumb.Template>
                      <ControlTemplate TargetType="Thumb">
                        <Ellipse x:Name="el" Width="18" Height="18" Fill="#FFFFFF" Stroke="#4FD1FF" StrokeThickness="3" RenderTransformOrigin="0.5,0.5">
                          <Ellipse.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Ellipse.RenderTransform>
                        </Ellipse>
                        <ControlTemplate.Triggers>
                          <Trigger Property="IsMouseOver" Value="True">
                            <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1.18" Duration="0:0:0.12"/><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1.18" Duration="0:0:0.12"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.15"/><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
                          </Trigger>
                          <Trigger Property="IsDragging" Value="True">
                            <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1.32" Duration="0:0:0.1"/><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1.32" Duration="0:0:0.1"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.15"/><DoubleAnimation Storyboard.TargetName="el" Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Thumb.Template>
                  </Thumb>
                </Track.Thumb>
              </Track>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property="IsEnabled" Value="False"><Setter TargetName="root" Property="Opacity" Value="0.35"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key="H" TargetType="TextBlock">
      <Setter Property="FontFamily" Value="Consolas"/>
      <Setter Property="FontSize" Value="15"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Foreground" Value="#4FD1FF"/>
      <Setter Property="Margin" Value="6,12,6,6"/>
    </Style>
  </Window.Resources>

  <Grid x:Name="rootGrid">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <Image x:Name="imgBg" Grid.RowSpan="3" Stretch="UniformToFill" IsHitTestVisible="False" Visibility="Collapsed" Opacity="0.4"/>

    <!-- TOP BAR -->
    <Border Grid.Row="0" Background="#181B20" BorderBrush="#2B3038" BorderThickness="0,0,0,1" Padding="12,8">
      <DockPanel>
        <Grid DockPanel.Dock="Right" VerticalAlignment="Center">
          <Button x:Name="btnGear" Content="&#x2699;" FontFamily="Segoe UI Symbol" FontSize="22" Padding="9,2" Margin="0"
                  Background="Transparent" BorderBrush="Transparent" Foreground="#E6E8F2" ToolTip="Menu" RenderTransformOrigin="0.5,0.5">
            <Button.RenderTransform><RotateTransform Angle="0"/></Button.RenderTransform>
            <Button.Triggers>
              <EventTrigger RoutedEvent="MouseEnter"><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(RotateTransform.Angle)" To="90" Duration="0:0:0.4">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation></Storyboard></BeginStoryboard></EventTrigger>
              <EventTrigger RoutedEvent="MouseLeave"><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(RotateTransform.Angle)" To="0" Duration="0:0:0.4">
<DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
</DoubleAnimation></Storyboard></BeginStoryboard></EventTrigger>
            </Button.Triggers>
          </Button>
        </Grid>
        <Viewbox x:Name="logoHost" Width="40" Height="40" Margin="0,0,8,0"/>
        <TextBlock Text="ROCK" FontSize="17" FontWeight="Black" Foreground="White" VerticalAlignment="Center" Margin="0,0,20,0"/>
        <TextBlock x:Name="txtCrumb" FontSize="14" FontWeight="SemiBold" Foreground="#9AA0B8" VerticalAlignment="Center"/>
      </DockPanel>
    </Border>

    <DockPanel Grid.Row="1" Margin="12,10,12,10">
      <Border DockPanel.Dock="Left" Width="205" Background="#181B20" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Padding="9,12">
        <DockPanel>
          <TextBlock DockPanel.Dock="Top" Text="TOOLBOX" FontFamily="Consolas" FontSize="11" FontWeight="Bold" Foreground="#8A93A5" Margin="11,0,0,10"/>
          <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
            <StackPanel x:Name="tabBar"/>
          </ScrollViewer>
        </DockPanel>
      </Border>
      <Grid Margin="10,0,0,0">

      <!-- INSTALL -->
      <Grid x:Name="pnlInstall">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="240"/>
          <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <Border Grid.Column="0" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4" Margin="0,0,10,0" Padding="8">
          <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel>
              <TextBlock Style="{StaticResource H}" Text="Actions" Margin="3,4,3,6"/>
              <Button x:Name="btnInstall" Content="Install / Upgrade Apps" Background="#2F6FB0"/>
              <Button x:Name="btnUninstall" Content="Uninstall Applications"/>
              <Button x:Name="btnUpgrade" Content="Upgrade All Applications"/>
              <Button x:Name="btnStopAction" Content="Stop Action" Background="#B0343A" Foreground="#FFEEEE"/>
              <TextBlock Style="{StaticResource H}" Text="Selection" Margin="3,14,3,6"/>
              <Button x:Name="btnAppsClear" Content="Clear Selection"/>
              <Button x:Name="btnCollapse" Content="Collapse All Categories"/>
              <Button x:Name="btnExpand" Content="Expand All Categories"/>
              <Border Background="#1E3547" BorderBrush="#35506A" BorderThickness="1" CornerRadius="3" Margin="3" Padding="12,6">
                <TextBlock x:Name="txtSelCount" Text="Selected Apps: 0" Foreground="#F0F4FF" HorizontalAlignment="Center"/>
              </Border>
              <Button x:Name="btnShowInstalled" Content="Show Installed Apps"/>
              <TextBlock Style="{StaticResource H}" Text="Custom winget ID" Margin="3,14,3,6"/>
              <TextBox x:Name="txtCustomId" Height="28" Margin="3" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"/>
              <Button x:Name="btnInstallCustom" Content="Install ID"/>
            </StackPanel>
          </ScrollViewer>
        </Border>
        <DockPanel Grid.Column="1">
          <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
            <TextBox x:Name="txtAppSearch" DockPanel.Dock="Right" Width="240" Height="30" Margin="8,0,0,0" VerticalContentAlignment="Center"
                     Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" ToolTip="Search apps"/>
            <WrapPanel x:Name="chipPanel"/>
          </DockPanel>
          <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
              <StackPanel x:Name="appsPanel" Margin="8"/>
            </ScrollViewer>
          </Border>
        </DockPanel>
      </Grid>

      <!-- TWEAKS -->
      <Grid x:Name="pnlTweaks" Visibility="Collapsed">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="470"/>
        </Grid.ColumnDefinitions>
        <Border Grid.Column="0" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4" Margin="0,0,10,0">
          <DockPanel>
            <WrapPanel DockPanel.Dock="Bottom" Margin="6">
              <Button x:Name="btnTweakApply" Content="Run Tweaks" Background="#2F6FB0" Padding="22,7"/>
              <Button x:Name="btnTweakUndo" Content="Undo Selected Tweaks" Padding="22,7"/>
              <Button x:Name="btnTweakRec" Content="Select Recommended" Padding="22,7"/>
              <Button x:Name="btnTweakClear" Content="Clear" Padding="22,7"/>
            </WrapPanel>
            <ScrollViewer VerticalScrollBarVisibility="Auto">
              <StackPanel x:Name="tweaksPanel" Margin="6"/>
            </ScrollViewer>
          </DockPanel>
        </Border>
        <Border Grid.Column="1" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="10">
              <TextBlock Style="{StaticResource H}" Text="Customize Preferences" Margin="4,4,4,6"/>
              <StackPanel x:Name="customizePanel"/>
              <TextBlock Style="{StaticResource H}" Text="Performance Plans - NOT FOR LAPTOPS"/>
              <Button x:Name="btnUltOff" Content="Ultimate Performance Profile - Disable"/>
              <Button x:Name="btnUltOn" Content="Ultimate Performance Profile - Enable"/>
              <TextBlock Style="{StaticResource H}" Text="DNS"/>
              <WrapPanel>
                <ComboBox x:Name="cmbDns" Width="240" Height="30" Margin="3" Foreground="Black" VerticalContentAlignment="Center"/>
                <Button x:Name="btnDns" Content="Set DNS"/>
              </WrapPanel>
              <TextBlock Style="{StaticResource H}" Text="Explorer"/>
              <Button x:Name="btnExplorer" Content="Restart Explorer (apply toggles now)"/>
            </StackPanel>
          </ScrollViewer>
        </Border>
      </Grid>

      <!-- DEBLOAT -->
      <DockPanel x:Name="pnlDebloat" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <Button x:Name="btnDebloat" Content="Remove Selected" Background="#B0343A" Padding="20,7"/>
          <Button x:Name="btnDebRec" Content="Select Recommended" Padding="20,7"/>
          <Button x:Name="btnDebInst" Content="Select Installed" Padding="20,7"/>
          <Button x:Name="btnDebAll" Content="Select All" Padding="20,7"/>
          <Button x:Name="btnDebClear" Content="Clear" Padding="20,7"/>
        </WrapPanel>
        <TextBlock DockPanel.Dock="Top" Foreground="#9AA0B8" Margin="8,2,8,8" TextWrapping="Wrap"
                   Text="Removes the app for all users and from the provisioned list (so it does not come back for new users). You can reinstall from the Microsoft Store."/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ScrollViewer VerticalScrollBarVisibility="Auto">
            <Grid x:Name="debloatPanel" Margin="8">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <StackPanel x:Name="debloatColL" Grid.Column="0" Margin="0,0,10,0"/>
              <StackPanel x:Name="debloatColR" Grid.Column="1" Margin="10,0,0,0"/>
            </Grid>
          </ScrollViewer>
        </Border>
      </DockPanel>

      <!-- CONFIG -->
      <Grid x:Name="pnlConfig" Visibility="Collapsed">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <Border Grid.Column="0" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4" Margin="0,0,10,0">
          <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="10">
              <TextBlock Style="{StaticResource H}" Text="Features" Margin="4,4,4,6"/>
              <StackPanel x:Name="featuresPanel"/>
              <Button x:Name="btnFeatures" Content="Install Features" Margin="3,8,3,3"/>
              <TextBlock Style="{StaticResource H}" Text="Fixes"/>
              <StackPanel x:Name="fixesPanel"/>
            </StackPanel>
          </ScrollViewer>
        </Border>
        <Border Grid.Column="1" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="10">
              <TextBlock Style="{StaticResource H}" Text="Legacy Windows Panels" Margin="4,4,4,6"/>
              <StackPanel x:Name="panelsPanel"/>
              <TextBlock Style="{StaticResource H}" Text="Remote Access"/>
              <Button x:Name="btnSsh" Content="OpenSSH Server - Enable"/>
              <TextBlock Style="{StaticResource H}" Text="Mouse Cursor"/>
              <Button x:Name="btnCurApply" Content="Windows 11 Dark Cursors - Apply"/>
              <Button x:Name="btnCurReset" Content="Default Windows Cursors - Restore"/>
              <TextBlock Style="{StaticResource H}" Text="Shortcut"/>
              <Button x:Name="btnShortcut" Content="Rock Toolbox Shortcut (Rock icon) - Create"/>
            </StackPanel>
          </ScrollViewer>
        </Border>
      </Grid>

      <!-- UPDATES -->
      <ScrollViewer x:Name="pnlUpdates" Visibility="Collapsed" VerticalScrollBarVisibility="Auto">
        <StackPanel MaxWidth="1180" Margin="20,10">
          <TextBlock Text="Windows Update Profiles" FontSize="26" FontWeight="SemiBold" Foreground="White"/>
          <TextBlock Text="Choose how Windows receives updates. Each profile replaces the Windows Update settings managed by Rock Toolbox." Foreground="#C9CDE0" Margin="0,4,0,18" TextWrapping="Wrap"/>
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <Border Grid.Column="0" Background="#23272D" BorderBrush="#5CE08A" BorderThickness="2" CornerRadius="6" Margin="0,0,8,0" Padding="20" MinHeight="340">
              <DockPanel>
                <Button x:Name="btnUpdRec" DockPanel.Dock="Bottom" Content="Apply Recommended" Margin="0,14,0,0" Padding="12,8"/>
                <StackPanel>
                  <TextBlock Text="Recommended" FontSize="21" FontWeight="SemiBold" Foreground="White"/>
                  <TextBlock Text="Balanced security and stability" Foreground="#C9CDE0" Margin="0,2,0,12"/>
                  <TextBlock TextWrapping="Wrap" Foreground="#D5D9E6" LineHeight="23">
                    <Run Text="- Defers feature updates for 365 days"/><LineBreak/>
                    <Run Text="- Defers quality updates for 4 days"/><LineBreak/>
                    <Run Text="- Excludes drivers from quality updates"/><LineBreak/>
                    <Run Text="- Notifies when downloaded updates are ready to install"/>
                  </TextBlock>
                  <TextBlock Text="Deferral policies need Windows Pro, Enterprise or Education." FontStyle="Italic" Foreground="#9AA0B8" FontSize="11.5" Margin="0,12,0,0" TextWrapping="Wrap"/>
                </StackPanel>
              </DockPanel>
            </Border>

            <Border Grid.Column="1" Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Margin="4,0,4,0" Padding="20" MinHeight="340">
              <DockPanel>
                <Button x:Name="btnUpdDefault" DockPanel.Dock="Bottom" Content="Restore Defaults" Margin="0,14,0,0" Padding="12,8"/>
                <StackPanel>
                  <TextBlock Text="Windows Default" FontSize="21" FontWeight="SemiBold" Foreground="White"/>
                  <TextBlock Text="Return control to Windows" Foreground="#C9CDE0" Margin="0,2,0,12"/>
                  <TextBlock TextWrapping="Wrap" Foreground="#D5D9E6" LineHeight="23">
                    <Run Text="- Removes Windows Update policies applied by Rock Toolbox"/><LineBreak/>
                    <Run Text="- Restores update service startup settings"/><LineBreak/>
                    <Run Text="- Re-enables update scheduled tasks"/>
                  </TextBlock>
                  <TextBlock Text="Use this to undo the Recommended or Disable profile." FontStyle="Italic" Foreground="#9AA0B8" FontSize="11.5" Margin="0,12,0,0" TextWrapping="Wrap"/>
                </StackPanel>
              </DockPanel>
            </Border>

            <Border Grid.Column="2" Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Margin="8,0,0,0" Padding="20" MinHeight="340">
              <DockPanel>
                <Button x:Name="btnUpdDisable" DockPanel.Dock="Bottom" Content="Disable Updates" Foreground="#FF5555" Margin="0,14,0,0" Padding="12,8"/>
                <StackPanel>
                  <TextBlock Text="Disable Updates" FontSize="21" FontWeight="SemiBold" Foreground="#FF4D4D"/>
                  <TextBlock Text="Advanced use only" Foreground="#FF4D4D" Margin="0,2,0,12"/>
                  <TextBlock TextWrapping="Wrap" Foreground="#D5D9E6" LineHeight="23">
                    <Run Text="- Disables automatic update policy"/><LineBreak/>
                    <Run Text="- Stops update services and scheduled tasks"/><LineBreak/>
                    <Run Text="- Clears downloaded update files"/>
                  </TextBlock>
                  <TextBlock Text="Security updates will not be installed while this profile is active." FontStyle="Italic" Foreground="#FF4D4D" FontSize="11.5" Margin="0,12,0,0" TextWrapping="Wrap"/>
                </StackPanel>
              </DockPanel>
            </Border>
          </Grid>
          <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4" Margin="0,14,0,0" Padding="14">
            <TextBlock Text="Changes apply system-wide. Restart Windows after switching profiles. Use Restore Defaults to undo the update policies." Foreground="#D5D9E6" HorizontalAlignment="Center" TextWrapping="Wrap"/>
          </Border>
          <WrapPanel Margin="0,10,0,0" HorizontalAlignment="Center">
            <Button x:Name="btnDrvOff" Content="Block drivers via Windows Update"/>
            <Button x:Name="btnDrvOn" Content="Allow drivers via Windows Update"/>
            <Button x:Name="btnUpdOpen" Content="Open Windows Update"/>
          </WrapPanel>
        </StackPanel>
      </ScrollViewer>

      <!-- WALLPAPERS -->
      <DockPanel x:Name="pnlWall" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <TextBox x:Name="txtWpQuery" Width="400" Height="34" Margin="3" FontSize="14" Padding="8,0" VerticalContentAlignment="Center"
                   Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"
                   ToolTip="Type what you want to see: mountains at night, cyberpunk city, red sports car..."/>
          <Button x:Name="btnWpSearch" Content="Search" Background="#2F6FB0" Padding="28,7"/>
          <Button x:Name="btnWpSimilar" Content="Find similar" Padding="18,7" ToolTip="Click a picture first, then press this to see more like it"/>
          <Button x:Name="btnWpMore" Content="More results" Padding="18,7"/>
        </WrapPanel>
        <WrapPanel x:Name="wpChips" DockPanel.Dock="Top" Margin="0,0,0,4"/>
        <WrapPanel DockPanel.Dock="Top">
          <ComboBox x:Name="cmbWpStyle" Width="110" Height="30" Margin="3" Foreground="Black" VerticalContentAlignment="Center"/>
          <Button x:Name="btnWpSet" Content="Set as wallpaper"/>
          <Button x:Name="btnWpLock" Content="Set as lock screen"/>
          <Button x:Name="btnWpBoth" Content="Set both" Background="#2F6FB0"/>
          <Button x:Name="btnWpRandom" Content="Random from list"/>
          <Button x:Name="btnWpSave" Content="Save to Pictures"/>
          <Button x:Name="btnWpLocal" Content="Desktop from local image..."/>
          <Button x:Name="btnWpLockLocal" Content="Lock screen from local image..."/>
          <Button x:Name="btnWpDailyOn" Content="Enable daily Bing wallpaper"/>
          <Button x:Name="btnWpDailyOff" Content="Disable daily"/>
        </WrapPanel>
        <TextBlock x:Name="txtWpInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap"
                   Text="Type a word and press Search. Click a picture to select it, double-click to apply it, or press Find similar to see more like it."/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ScrollViewer x:Name="wpScroll" VerticalScrollBarVisibility="Auto">
            <Grid x:Name="wpPanel" Margin="6"/>
          </ScrollViewer>
        </Border>
      </DockPanel>


      <!-- WINGET ID LIST -->
      <DockPanel x:Name="pnlFind" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <ComboBox x:Name="cmbFindSource" Width="190" Height="30" Margin="3" Foreground="Black" VerticalContentAlignment="Center"/>
          <TextBox x:Name="txtFindQuery" Width="220" Height="30" Margin="3" VerticalContentAlignment="Center"
                   Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" ToolTip="Optional: narrow the list by name"/>
          <Button x:Name="btnFindCopy" Content="Copy ID" Background="#2F6FB0" Padding="22,6"/>
          <Button x:Name="btnFindSend" Content="Use in Install tab"/>
          <Button x:Name="btnFindInstall" Content="Install selected"/>
        </WrapPanel>
        <TextBlock x:Name="txtFindInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap" Text=""/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ListView x:Name="lvFind" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="Name" DisplayMemberBinding="{Binding Name}" Width="330"/>
                <GridViewColumn Header="Id (winget)" DisplayMemberBinding="{Binding Id}" Width="380"/>
                <GridViewColumn Header="Category" DisplayMemberBinding="{Binding Category}" Width="180"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </DockPanel>

      <!-- WINGET UPGRADE -->
      <DockPanel x:Name="pnlUpg" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <Button x:Name="btnUpgScan" Content="Check for updates" Padding="22,6"/>
          <Button x:Name="btnUpgRun" Content="Update selected" Background="#2F6FB0" Padding="22,6"/>
          <Button x:Name="btnUpgAll" Content="Update all" Padding="22,6"/>
          <Button x:Name="btnUpgSelAll" Content="Select all"/>
          <Button x:Name="btnUpgSelNone" Content="Select none"/>
          <TextBlock x:Name="txtUpgCount" Text="Selected: 0 of 0" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="10,0"/>
        </WrapPanel>
        <TextBlock x:Name="txtUpgInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap"
                   Text="Press Check for updates to list the apps that have a newer version."/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ListView x:Name="lvUpg" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
            <ListView.View>
              <GridView>
                <GridViewColumn Width="46">
                  <GridViewColumn.CellTemplate>
                    <DataTemplate>
                      <CheckBox IsChecked="{Binding Selected, Mode=TwoWay}" HorizontalAlignment="Center" Margin="0"/>
                    </DataTemplate>
                  </GridViewColumn.CellTemplate>
                </GridViewColumn>
                <GridViewColumn Header="Name" DisplayMemberBinding="{Binding Name}" Width="300"/>
                <GridViewColumn Header="Id" DisplayMemberBinding="{Binding Id}" Width="300"/>
                <GridViewColumn Header="Current" DisplayMemberBinding="{Binding Version}" Width="120"/>
                <GridViewColumn Header="Available" DisplayMemberBinding="{Binding Available}" Width="120"/>
                <GridViewColumn Header="Source" DisplayMemberBinding="{Binding Source}" Width="80"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </DockPanel>

      <!-- WINDOWS UPDATE -->
      <DockPanel x:Name="pnlWU" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <Button x:Name="btnWUScan" Content="Scan for updates" Padding="22,6"/>
          <Button x:Name="btnWUInstall" Content="Install selected" Background="#2F6FB0" Padding="22,6"/>
          <Button x:Name="btnWUSelAll" Content="Select all"/>
          <Button x:Name="btnWUSelNone" Content="Select none"/>
          <CheckBox x:Name="chkWUReboot" Style="{StaticResource Switch}" Content="Restart automatically if needed" VerticalAlignment="Center" Margin="12,0,0,0"/>
          <TextBlock x:Name="txtWUCount" Text="Selected: 0 of 0" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="10,0"/>
          <Button x:Name="btnWUAutoOff" Content="Disable auto updates"/>
          <Button x:Name="btnWUAutoOn" Content="Enable auto updates"/>
          <TextBlock x:Name="txtWUAuto" FontWeight="SemiBold" VerticalAlignment="Center" Margin="8,0"/>
        </WrapPanel>
        <TextBlock x:Name="txtWUInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap"
                   Text="Press Scan for updates to list everything Windows Update has for this PC."/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ListView x:Name="lvWU" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
            <ListView.View>
              <GridView>
                <GridViewColumn Width="46">
                  <GridViewColumn.CellTemplate>
                    <DataTemplate>
                      <CheckBox IsChecked="{Binding Selected, Mode=TwoWay}" HorizontalAlignment="Center" Margin="0"/>
                    </DataTemplate>
                  </GridViewColumn.CellTemplate>
                </GridViewColumn>
                <GridViewColumn Header="Update" DisplayMemberBinding="{Binding Title}" Width="520"/>
                <GridViewColumn Header="KB" DisplayMemberBinding="{Binding KB}" Width="100"/>
                <GridViewColumn Header="Size" DisplayMemberBinding="{Binding Size}" Width="90"/>
                <GridViewColumn Header="Type" DisplayMemberBinding="{Binding Type}" Width="80"/>
                <GridViewColumn Header="Category" DisplayMemberBinding="{Binding Category}" Width="260"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </DockPanel>

      <!-- UNINSTALL APPS -->
      <DockPanel x:Name="pnlUnApps" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <TextBox x:Name="txtUnQuery" Width="240" Height="30" Margin="3" VerticalContentAlignment="Center"
                   Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" ToolTip="Search installed apps"/>
          <ComboBox x:Name="cmbUnType" Width="160" Height="30" Margin="3" Foreground="Black" VerticalContentAlignment="Center"/>
          <Button x:Name="btnUnRefresh" Content="Refresh"/>
          <Button x:Name="btnUnSelNone" Content="Select none"/>
          <Button x:Name="btnUnGo" Content="Uninstall selected" Background="#B0343A" Padding="22,6"/>
          <TextBlock x:Name="txtUnCount" Text="Selected: 0" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="10,0"/>
        </WrapPanel>
        <TextBlock x:Name="txtUnInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap"
                   Text="Apps installed on this PC (desktop programs and Store apps). Tick what you want to remove."/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ListView x:Name="lvUnApps" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
            <ListView.View>
              <GridView>
                <GridViewColumn Width="46">
                  <GridViewColumn.CellTemplate>
                    <DataTemplate><CheckBox IsChecked="{Binding Selected, Mode=TwoWay}" HorizontalAlignment="Center" Margin="0"/></DataTemplate>
                  </GridViewColumn.CellTemplate>
                </GridViewColumn>
                <GridViewColumn Header="Name" DisplayMemberBinding="{Binding Name}" Width="380"/>
                <GridViewColumn Header="Version" DisplayMemberBinding="{Binding Version}" Width="130"/>
                <GridViewColumn Header="Publisher" DisplayMemberBinding="{Binding Publisher}" Width="240"/>
                <GridViewColumn Header="Type" DisplayMemberBinding="{Binding Source}" Width="80"/>
                <GridViewColumn Header="Size" DisplayMemberBinding="{Binding Size}" Width="90"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </DockPanel>

      <!-- DRIVERS -->
      <DockPanel x:Name="pnlDrv" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <Button x:Name="btnDrvModeInstall" Content="Install drivers" Padding="22,6"/>
          <Button x:Name="btnDrvModeUninstall" Content="Uninstall drivers" Padding="22,6"/>
        </WrapPanel>
        <Grid>
          <StackPanel x:Name="pnlDrvWelcome" HorizontalAlignment="Center" VerticalAlignment="Center">
            <TextBlock Text="What do you want to do with your drivers?" FontSize="20" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" Margin="0,0,0,18"/>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
              <Button x:Name="btnDrvPickInstall" Width="280" Height="170" Margin="10" Padding="16">
                <StackPanel>
                  <TextBlock Text="&#xE896;" FontFamily="Segoe MDL2 Assets" FontSize="44" Foreground="#4FD1FF" HorizontalAlignment="Center"/>
                  <TextBlock Text="Install" FontSize="20" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" Margin="0,10,0,4"/>
                  <TextBlock Text="Find missing or newer drivers and choose which ones to install" TextWrapping="Wrap" TextAlignment="Center" Foreground="#C9CDE0"/>
                </StackPanel>
              </Button>
              <Button x:Name="btnDrvPickUninstall" Width="280" Height="170" Margin="10" Padding="16">
                <StackPanel>
                  <TextBlock Text="&#xE74D;" FontFamily="Segoe MDL2 Assets" FontSize="44" Foreground="#FF6B6B" HorizontalAlignment="Center"/>
                  <TextBlock Text="Uninstall" FontSize="20" FontWeight="SemiBold" Foreground="White" HorizontalAlignment="Center" Margin="0,10,0,4"/>
                  <TextBlock Text="See the drivers installed on this PC and remove the ones you pick" TextWrapping="Wrap" TextAlignment="Center" Foreground="#C9CDE0"/>
                </StackPanel>
              </Button>
            </StackPanel>
          </StackPanel>

          <DockPanel x:Name="pnlDrvInstall" Visibility="Collapsed">
            <WrapPanel DockPanel.Dock="Top">
              <Button x:Name="btnDrvScan" Content="Scan for drivers" Padding="22,6"/>
              <Button x:Name="btnDrvSelAll" Content="Select all"/>
              <Button x:Name="btnDrvSelNone" Content="Select none"/>
              <Button x:Name="btnDrvInstall" Content="Install selected" Background="#2F6FB0" Padding="22,6"/>
              <TextBlock x:Name="txtDrvCount" Text="Selected: 0 of 0" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="10,0"/>
            </WrapPanel>
            <TextBlock x:Name="txtDrvInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap"/>
            <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
              <ListView x:Name="lvDrvInstall" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
                <ListView.View>
                  <GridView>
                    <GridViewColumn Width="46">
                      <GridViewColumn.CellTemplate>
                        <DataTemplate><CheckBox IsChecked="{Binding Selected, Mode=TwoWay}" HorizontalAlignment="Center" Margin="0"/></DataTemplate>
                      </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                    <GridViewColumn Header="Driver" DisplayMemberBinding="{Binding Title}" Width="400"/>
                    <GridViewColumn Header="Used for" DisplayMemberBinding="{Binding Kind}" Width="190"/>
                    <GridViewColumn Header="Class" DisplayMemberBinding="{Binding DrvClass}" Width="110"/>
                    <GridViewColumn Header="Provider" DisplayMemberBinding="{Binding Provider}" Width="190"/>
                    <GridViewColumn Header="Date" DisplayMemberBinding="{Binding DrvDate}" Width="100"/>
                    <GridViewColumn Header="Size" DisplayMemberBinding="{Binding Size}" Width="90"/>
                  </GridView>
                </ListView.View>
              </ListView>
            </Border>
          </DockPanel>

          <DockPanel x:Name="pnlDrvUninstall" Visibility="Collapsed">
            <WrapPanel DockPanel.Dock="Top">
              <Button x:Name="btnDrvUScan" Content="Refresh list" Padding="22,6"/>
              <Button x:Name="btnDrvUSelNone" Content="Select none"/>
              <Button x:Name="btnDrvUninstall" Content="Uninstall selected" Background="#B0343A" Padding="22,6"/>
              <TextBlock x:Name="txtDrvUCount" Text="Selected: 0 of 0" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="10,0"/>
            </WrapPanel>
            <TextBlock x:Name="txtDrvUInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap"/>
            <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
              <ListView x:Name="lvDrvUn" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
                <ListView.View>
                  <GridView>
                    <GridViewColumn Width="46">
                      <GridViewColumn.CellTemplate>
                        <DataTemplate><CheckBox IsChecked="{Binding Selected, Mode=TwoWay}" HorizontalAlignment="Center" Margin="0"/></DataTemplate>
                      </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                    <GridViewColumn Header="Driver file" DisplayMemberBinding="{Binding Title}" Width="250"/>
                    <GridViewColumn Header="Used for" DisplayMemberBinding="{Binding Kind}" Width="190"/>
                    <GridViewColumn Header="Provider" DisplayMemberBinding="{Binding Provider}" Width="200"/>
                    <GridViewColumn Header="Class" DisplayMemberBinding="{Binding DrvClass}" Width="110"/>
                    <GridViewColumn Header="Version" DisplayMemberBinding="{Binding Version}" Width="140"/>
                    <GridViewColumn Header="Date" DisplayMemberBinding="{Binding DrvDate}" Width="100"/>
                    <GridViewColumn Header="INF" DisplayMemberBinding="{Binding Id}" Width="100"/>
                  </GridView>
                </ListView.View>
              </ListView>
            </Border>
          </DockPanel>
        </Grid>
      </DockPanel>

      <!-- GOD MODE -->
      <DockPanel x:Name="pnlGod" Visibility="Collapsed">
        <WrapPanel DockPanel.Dock="Top">
          <ComboBox x:Name="cmbGodCat" Width="230" Height="30" Margin="3" Foreground="Black" VerticalContentAlignment="Center"/>
          <TextBox x:Name="txtGodQuery" Width="220" Height="30" Margin="3" VerticalContentAlignment="Center"
                   Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A" ToolTip="Search by name or description"/>
          <Button x:Name="btnGodOpen" Content="Open selected" Background="#2F6FB0" Padding="22,6"/>
          <Button x:Name="btnGodFolder" Content="Open GodMode folder"/>
          <Button x:Name="btnGodDesktop" Content="Create GodMode folder on Desktop"/>
          <Button x:Name="btnGodRefresh" Content="Refresh"/>
        </WrapPanel>
        <TextBlock x:Name="txtGodInfo" DockPanel.Dock="Bottom" Foreground="#9AA0B8" Margin="8,6" TextWrapping="Wrap" Text=""/>
        <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="4">
          <ListView x:Name="lvGod" Background="#1B1F24" Foreground="#E6E8F2" BorderThickness="0" SelectionMode="Single">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="Setting" DisplayMemberBinding="{Binding Name}" Width="340"/>
                <GridViewColumn Header="Category" DisplayMemberBinding="{Binding Category}" Width="230"/>
                <GridViewColumn Header="Description" DisplayMemberBinding="{Binding Description}" Width="520"/>
              </GridView>
            </ListView.View>
          </ListView>
        </Border>
      </DockPanel>

      <!-- SETTINGS -->
      <ScrollViewer x:Name="pnlSettings" Visibility="Collapsed" VerticalScrollBarVisibility="Auto">
        <StackPanel MaxWidth="1100" Margin="20,10">
          <TextBlock Text="Settings" FontSize="26" FontWeight="SemiBold" Foreground="White"/>
          <TextBlock Text="Customize how Rock Toolbox looks and behaves. Changes apply instantly and are saved automatically." Foreground="#C9CDE0" Margin="0,4,0,18" TextWrapping="Wrap"/>

          <Border Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Padding="20" Margin="0,0,0,12">
            <StackPanel>
              <TextBlock Text="Appearance" Style="{StaticResource H}" Margin="0,0,0,8"/>
              <CheckBox x:Name="chkLight" Style="{StaticResource Switch}" Content="Light mode (turn off for dark mode)"/>

              <TextBlock Text="Accent color" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <WrapPanel x:Name="swatchPanel"/>
              <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                <TextBlock Text="Custom color (#RRGGBB):" Foreground="#C9CDE0" VerticalAlignment="Center" Margin="4,0,8,0"/>
                <TextBox x:Name="txtAccentHex" Width="110" Height="28" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"/>
                <Button x:Name="btnAccentApply" Content="Apply" Padding="18,5"/>
                <Button x:Name="btnAccentReset" Content="Default color" Padding="18,5"/>
              </StackPanel>

              <TextBlock Text="Text size" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <ComboBox x:Name="cmbFontSize" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,0" Foreground="Black" VerticalContentAlignment="Center"/>

              <TextBlock Text="Interface scale" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <ComboBox x:Name="cmbScale" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,0" Foreground="Black" VerticalContentAlignment="Center"/>
            </StackPanel>
          </Border>

          <Border Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Padding="20" Margin="0,0,0,12">
            <StackPanel>
              <TextBlock Text="Background" Style="{StaticResource H}" Margin="0,0,0,8"/>

              <TextBlock Text="Background style (dark mode only)" Foreground="#C9CDE0" Margin="4,6,0,6"/>
              <ComboBox x:Name="cmbBgStyle" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,0" Foreground="Black" VerticalContentAlignment="Center"/>

              <TextBlock Text="Custom background color (#RRGGBB)" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <StackPanel Orientation="Horizontal">
                <TextBox x:Name="txtBgHex" Width="110" Height="28" Margin="4,0,0,0" VerticalContentAlignment="Center" Background="#14171B" Foreground="White" CaretBrush="White" BorderBrush="#35506A"/>
                <Button x:Name="btnBgColorApply" Content="Apply" Padding="18,5"/>
                <Button x:Name="btnBgColorClear" Content="Clear" Padding="18,5"/>
              </StackPanel>

              <TextBlock Text="Background image" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <WrapPanel>
                <Button x:Name="btnBgImgPick" Content="Choose image..." Background="#2F6FB0" Padding="18,5"/>
                <Button x:Name="btnBgImgClear" Content="Remove image" Padding="18,5"/>
                <Button x:Name="btnBgRestore" Content="Restore default" Padding="18,5"/>
                <TextBlock x:Name="txtBgImgName" Foreground="#9AA0B8" VerticalAlignment="Center" Margin="8,0,0,0"/>
              </WrapPanel>

              <TextBlock Text="Image opacity" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <StackPanel Orientation="Horizontal">
                <Slider x:Name="sldBgOpacity" Style="{StaticResource RockSlider}" Minimum="0" Maximum="100" Value="40" Width="340" TickFrequency="5" IsSnapToTickEnabled="True" IsMoveToPointEnabled="True" AutoToolTipPlacement="TopLeft" AutoToolTipPrecision="0" VerticalAlignment="Center" Margin="4,0,0,0"/>
                <TextBlock x:Name="txtBgOpacity" Text="40%" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="14,0,0,0" Width="50"/>
              </StackPanel>

              <TextBlock Text="Image blur" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <StackPanel Orientation="Horizontal">
                <Slider x:Name="sldBgBlur" Style="{StaticResource RockSlider}" Minimum="0" Maximum="40" Value="0" Width="340" TickFrequency="1" IsSnapToTickEnabled="True" IsMoveToPointEnabled="True" AutoToolTipPlacement="TopLeft" AutoToolTipPrecision="0" VerticalAlignment="Center" Margin="4,0,0,0"/>
                <TextBlock x:Name="txtBgBlur" Text="0" Foreground="#4FD1FF" FontFamily="Consolas" VerticalAlignment="Center" Margin="14,0,0,0" Width="50"/>
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Padding="20" Margin="0,0,0,12">
            <StackPanel>
              <TextBlock Text="Animations" Style="{StaticResource H}" Margin="0,0,0,8"/>

              <TextBlock Text="Scroll animation" Foreground="#C9CDE0" Margin="4,6,0,6"/>
              <ComboBox x:Name="cmbScrollAnim" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,0" Foreground="Black" VerticalContentAlignment="Center"/>

              <TextBlock Text="Scroll speed" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <ComboBox x:Name="cmbScrollSpeed" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,0" Foreground="Black" VerticalContentAlignment="Center"/>

              <TextBlock Text="Page transition (when you switch tabs)" Foreground="#C9CDE0" Margin="4,16,0,6"/>
              <ComboBox x:Name="cmbPageAnim" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,10" Foreground="Black" VerticalContentAlignment="Center"/>

              <CheckBox x:Name="chkCardAnim" Style="{StaticResource Switch}" Content="Animate app cards (hover, select, fade-in)"/>
            </StackPanel>
          </Border>

          <Border Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Padding="20" Margin="0,0,0,12">
            <StackPanel>
              <TextBlock Text="Behavior" Style="{StaticResource H}" Margin="0,0,0,8"/>
              <CheckBox x:Name="chkTopmost" Style="{StaticResource Switch}" Content="Keep Rock Toolbox always on top"/>
              <CheckBox x:Name="chkMaxStart" Style="{StaticResource Switch}" Content="Start maximized"/>
              <CheckBox x:Name="chkStatusBar" Style="{StaticResource Switch}" Content="Show the status / progress bar"/>
              <CheckBox x:Name="chkHideConsole" Style="{StaticResource Switch}" Content="Hide the console window (log keeps running)"/>
              <CheckBox x:Name="chkRemember" Style="{StaticResource Switch}" Content="Reopen the last tab I used"/>
              <CheckBox x:Name="chkTaskbar" Style="{StaticResource Switch}" Content="Add to taskbar (pin Rock Toolbox)"/>
              <TextBlock Text="Open this tab on start" Foreground="#C9CDE0" Margin="4,14,0,6"/>
              <ComboBox x:Name="cmbStartTab" Width="230" Height="30" HorizontalAlignment="Left" Margin="4,0,0,0" Foreground="Black" VerticalContentAlignment="Center"/>
            </StackPanel>
          </Border>

        </StackPanel>
      </ScrollViewer>

      </Grid>
    </DockPanel>

    <!-- PROGRESS BAR (replaces the old log box; messages go to the Rock console) -->
    <Border x:Name="statusBar" Grid.Row="2" Background="#181B20" BorderBrush="#2B3038" BorderThickness="0,1,0,0" Padding="16,9">
      <DockPanel>
        <TextBlock x:Name="txtPct" DockPanel.Dock="Right" Text="" Width="52" TextAlignment="Right" Foreground="#4FD1FF" FontFamily="Consolas" FontSize="13" VerticalAlignment="Center"/>
        <TextBlock x:Name="txtStatus" DockPanel.Dock="Left" Text="Ready" Width="430" Foreground="#C9CDE0" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" Margin="0,0,14,0"/>
        <ProgressBar x:Name="pbMain" Style="{StaticResource StripeBar}" Height="10" Minimum="0" Maximum="100" Value="0" Foreground="#4FD1FF" Background="#0F1116" BorderBrush="#2B3038" BorderThickness="1" VerticalAlignment="Center"/>
      </DockPanel>
    </Border>
    <Border x:Name="menuGear" Grid.Row="1" Panel.ZIndex="99" Visibility="Collapsed" HorizontalAlignment="Right" VerticalAlignment="Top"
            Width="230" Margin="0,8,14,0" Background="#23272D" BorderBrush="#35506A" BorderThickness="1" CornerRadius="6" Padding="10">
      <Border.Effect><DropShadowEffect BlurRadius="18" ShadowDepth="3" Opacity="0.45"/></Border.Effect>
      <StackPanel>
        <CheckBox x:Name="chkGearLight" Style="{StaticResource Switch}" Content="Light mode" Margin="4,4,4,10"/>
        <Button x:Name="btnGearAbout" Content="About" HorizontalContentAlignment="Left" Padding="12,7"/>
      </StackPanel>
    </Border>
  </Grid>
</Window>
'@

$xamlText = Convert-XamlTheme -Text $xamlText -Inject
