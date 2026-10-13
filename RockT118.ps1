$btnUpgSelNone.Add_Click({ foreach ($u in @(Get-UpgItems)) { $u.Selected = $false }; Update-UpgCount })
