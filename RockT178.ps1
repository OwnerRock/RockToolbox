$lvDrvInstall.Add_MouseDoubleClick({ $it = $lvDrvInstall.SelectedItem; if ($it) { $it.Selected = -not $it.Selected; Update-DrvCount } })
