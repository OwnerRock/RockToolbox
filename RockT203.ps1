$chkMaxStart.Add_Click({ $script:Settings['StartMaximized'] = [bool]$chkMaxStart.IsChecked; Save-Settings })
