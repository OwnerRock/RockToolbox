$btnDrvUSelNone.Add_Click({ foreach ($d in @(Get-DrvItems $lvDrvUn)) { $d.Selected = $false }; Update-DrvUCount })
