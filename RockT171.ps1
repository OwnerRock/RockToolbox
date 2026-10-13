$btnDrvSelNone.Add_Click({ foreach ($d in @(Get-DrvItems $lvDrvInstall)) { $d.Selected = $false }; Update-DrvCount })
