$lvDrvUn.Add_MouseDoubleClick({ $it = $lvDrvUn.SelectedItem; if ($it) { $it.Selected = -not $it.Selected; Update-DrvUCount } })
