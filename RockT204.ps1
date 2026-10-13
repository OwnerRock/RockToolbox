$chkStatusBar.Add_Click({ $script:Settings['StatusBar'] = [bool]$chkStatusBar.IsChecked; Apply-Settings; Save-Settings })
