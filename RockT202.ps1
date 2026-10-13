$chkTopmost.Add_Click({ $script:Settings['Topmost'] = [bool]$chkTopmost.IsChecked; Apply-Settings; Save-Settings })
