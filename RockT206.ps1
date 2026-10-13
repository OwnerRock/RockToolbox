$chkRemember.Add_Click({
    $script:Settings['RememberTab'] = [bool]$chkRemember.IsChecked
    $cmbStartTab.IsEnabled = (-not [bool]$chkRemember.IsChecked)
    Save-Settings
})
