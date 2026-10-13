$btnAppsClear.Add_Click({
    $script:Selected.Clear()
    foreach ($it in $script:AppCards) { Set-CardLook $it.Card $false }
    Update-SelCount
})
