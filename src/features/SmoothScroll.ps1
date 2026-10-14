# Module: features/SmoothScroll - Smooth scrolling
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- smooth scrolling (mouse wheel / touchpad) for every scroll area
$script:ScrollOk = $false
$script:ScrollModes  = @{ 'Off' = 0; 'Smooth' = 1; 'Glide' = 2; 'Snappy' = 3; 'Bouncy' = 4 }
$script:ScrollSpeeds = @{ 'Slow' = 0.6; 'Normal' = 1.0; 'Fast' = 1.7 }
$scrollCode = @'
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
public static class RockScroll {
    public static int Mode = 1;
    public static double Speed = 1.0;
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached("Offset", typeof(double), typeof(RockScroll), new PropertyMetadata(0.0, OnOffset));
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(double), typeof(RockScroll), new PropertyMetadata(-1.0));
    public static readonly DependencyProperty LastProperty = DependencyProperty.RegisterAttached("Last", typeof(long), typeof(RockScroll), new PropertyMetadata(0L));
    static void OnOffset(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        ScrollViewer sv = d as ScrollViewer;
        if (sv != null) sv.ScrollToVerticalOffset((double)e.NewValue);
    }
    public static void Attach(UIElement root) { root.PreviewMouseWheel += OnWheel; }
    static DependencyObject Parent(DependencyObject o) {
        if (o is Visual || o is System.Windows.Media.Media3D.Visual3D) return VisualTreeHelper.GetParent(o);
        FrameworkContentElement fce = o as FrameworkContentElement;
        if (fce != null) return fce.Parent;
        return LogicalTreeHelper.GetParent(o);
    }
    static ScrollViewer Find(DependencyObject o, int delta) {
        while (o != null) {
            ScrollViewer sv = o as ScrollViewer;
            if (sv != null && sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && sv.ScrollableHeight > 0) {
                ItemsControl ic = sv.TemplatedParent as ItemsControl;
                if (sv.CanContentScroll && ic != null && VirtualizingPanel.GetScrollUnit(ic) != ScrollUnit.Pixel) return null;
                bool atTop = sv.VerticalOffset <= 0.5;
                bool atEnd = sv.VerticalOffset >= sv.ScrollableHeight - 0.5;
                if (!((delta > 0 && atTop) || (delta < 0 && atEnd))) return sv;
            }
            o = Parent(o);
        }
        return null;
    }
    static void OnWheel(object sender, MouseWheelEventArgs e) {
        if (Mode == 0 || Keyboard.Modifiers != ModifierKeys.None) return;
        ScrollViewer sv = Find(e.OriginalSource as DependencyObject, e.Delta);
        if (sv == null) return;
        e.Handled = true;
        long now = Environment.TickCount;
        double cur = sv.VerticalOffset;
        double dur = 380;
        IEasingFunction ease;
        if (Mode == 2) { PowerEase p = new PowerEase(); p.Power = 5; p.EasingMode = EasingMode.EaseOut; ease = p; dur = 750; }
        else if (Mode == 3) { CubicEase c = new CubicEase(); c.EasingMode = EasingMode.EaseOut; ease = c; dur = 200; }
        else if (Mode == 4) { BackEase b = new BackEase(); b.Amplitude = 0.4; b.EasingMode = EasingMode.EaseOut; ease = b; dur = 520; }
        else { CubicEase c = new CubicEase(); c.EasingMode = EasingMode.EaseOut; ease = c; dur = 380; }
        double target = (double)sv.GetValue(TargetProperty);
        long last = (long)sv.GetValue(LastProperty);
        double baseV = (target < 0 || now - last > dur + 150) ? cur : target;
        double step = e.Delta / 120.0 * 100.0 * Speed;
        double t = Math.Max(0, Math.Min(sv.ScrollableHeight, baseV - step));
        sv.SetValue(TargetProperty, t);
        sv.SetValue(LastProperty, now);
        sv.BeginAnimation(OffsetProperty, null);
        sv.SetValue(OffsetProperty, cur);
        DoubleAnimation a = new DoubleAnimation(cur, t, new Duration(TimeSpan.FromMilliseconds(dur)));
        a.EasingFunction = ease;
        sv.BeginAnimation(OffsetProperty, a);
    }
}
'@
try {
    if (-not ('RockScroll' -as [type])) { Add-Type -TypeDefinition $scrollCode -ReferencedAssemblies 'PresentationFramework', 'PresentationCore', 'WindowsBase' }
    [RockScroll]::Attach($window)
    $script:ScrollOk = $true
} catch { Log ('Smooth scrolling is unavailable: ' + $_.Exception.Message) 'WARN' 'Settings' }

function Apply-Scroll {
    if (-not $script:ScrollOk) { return }
    $m = $script:ScrollModes[[string]$script:Settings['ScrollAnim']]
    if ($null -eq $m) { $m = 1 }
    $sp = $script:ScrollSpeeds[[string]$script:Settings['ScrollSpeed']]
    if ($null -eq $sp) { $sp = 1.0 }
    [RockScroll]::Mode = [int]$m
    [RockScroll]::Speed = [double]$sp
}

foreach ($t in 'Off', 'Smooth', 'Glide', 'Snappy', 'Bouncy') { [void]$cmbScrollAnim.Items.Add($t) }
foreach ($t in 'Slow', 'Normal', 'Fast') { [void]$cmbScrollSpeed.Items.Add($t) }
foreach ($t in 'Slide up', 'Fade', 'Zoom', 'Off') { [void]$cmbPageAnim.Items.Add($t) }
$cmbScrollAnim.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbScrollAnim.SelectedItem) { return }
    $script:Settings['ScrollAnim'] = [string]$cmbScrollAnim.SelectedItem
    Apply-Scroll; Save-Settings
})
$cmbScrollSpeed.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbScrollSpeed.SelectedItem) { return }
    $script:Settings['ScrollSpeed'] = [string]$cmbScrollSpeed.SelectedItem
    Apply-Scroll; Save-Settings
})
$cmbPageAnim.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbPageAnim.SelectedItem) { return }
    $script:Settings['PageAnim'] = [string]$cmbPageAnim.SelectedItem
    Save-Settings
})
$chkCardAnim.Add_Click({ $script:Settings['CardAnim'] = [bool]$chkCardAnim.IsChecked; Save-Settings })

