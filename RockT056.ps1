foreach ($f in $Features) {
    $cb = New-Object System.Windows.Controls.CheckBox
    $cb.Content = $f.Name; $cb.Tag = $f
    [void]$featuresPanel.Children.Add($cb)
    [void]$featureBoxes.Add($cb)
}
