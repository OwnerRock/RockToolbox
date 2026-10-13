$btnExplorer.Add_Click({
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Log 'Explorer restarted.' 'INFO' 'Customize'
})

$guidRx = '[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}'
