$lvUnApps.Add_MouseDoubleClick({
    $it = $lvUnApps.SelectedItem
    if ($it) { $it.Selected = -not $it.Selected; Update-UnCount }
})

# ------------------------------------------------------------------
# Drivers page (Install / Uninstall)
