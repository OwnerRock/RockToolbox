$btnFindSend.Add_Click({
    $it = $lvFind.SelectedItem
    if (-not $it) { Log 'Select an app first.' 'WARN' 'Find'; return }
    $txtCustomId.Text = $it.Id
    Show-Page 'Install'
    Log ('ID sent to the Install tab (Custom winget ID): ' + $it.Id) 'INFO' 'Find'
})
