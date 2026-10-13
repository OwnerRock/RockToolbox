$btnUpgSelAll.Add_Click({ foreach ($u in @(Get-UpgItems)) { $u.Selected = $true }; Update-UpgCount })
