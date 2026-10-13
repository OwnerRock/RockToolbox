$cmbPageAnim.Add_SelectionChanged({
    if ($script:SettingsBusy -or -not $cmbPageAnim.SelectedItem) { return }
    $script:Settings['PageAnim'] = [string]$cmbPageAnim.SelectedItem
    Save-Settings
})
