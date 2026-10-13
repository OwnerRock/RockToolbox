$cmbFontSize.Add_SelectionChanged({
    if ($script:SettingsBusy -or $cmbFontSize.SelectedIndex -lt 0) { return }
    $script:Settings['FontSize'] = $script:FontSizes[$cmbFontSize.SelectedIndex]
    Apply-Settings; Save-Settings
})
