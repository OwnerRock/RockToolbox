$btnWUSelNone.Add_Click({ foreach ($u in @(Get-WUItems)) { $u.Selected = $false }; Update-WUCount })
