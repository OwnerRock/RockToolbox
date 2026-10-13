$window.AddHandler([System.Windows.UIElement]::PreviewMouseLeftButtonDownEvent, [System.Windows.Input.MouseButtonEventHandler]{
    if ($menuGear.Visibility -eq 'Visible' -and -not $menuGear.IsMouseOver -and -not $btnGear.IsMouseOver) { Hide-GearMenu }
})
