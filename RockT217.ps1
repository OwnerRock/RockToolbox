$cmbScrollAnim.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbScrollAnim.SelectedItem) { return }
    $script:Settings['ScrollAnim'] = [string]$cmbScrollAnim.SelectedItem
    Apply-Scroll; Save-Settings
})
