$btnDrvSelAll.Add_Click({ foreach ($d in @(Get-DrvItems $lvDrvInstall)) { $d.Selected = $true }; Update-DrvCount })
