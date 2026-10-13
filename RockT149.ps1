$lvWU.Add_MouseDoubleClick({
    $it = $lvWU.SelectedItem
    if ($it) { $it.Selected = -not $it.Selected; Update-WUCount }
})

# ------------------------------------------------------------------
# Uninstall Apps page
