$btnWpRandom.Add_Click({
    if ($script:wpItems.Count -eq 0) { Log 'Search for some wallpapers first.'; return }
    Apply-WpItem (Get-Random -InputObject $script:wpItems)
})
