$btnWUSelAll.Add_Click({ foreach ($u in @(Get-WUItems)) { $u.Selected = $true }; Update-WUCount })
