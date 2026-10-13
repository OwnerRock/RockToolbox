$lvUpg.Add_MouseDoubleClick({
    $it = $lvUpg.SelectedItem
    if ($it) { $it.Selected = -not $it.Selected; Update-UpgCount }
})

# ------------------------------------------------------------------
# God Mode page: every entry of the GodMode.{ED7BA470-8E54-465E-825C-99712043E01C} folder
