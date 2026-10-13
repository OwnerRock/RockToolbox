$btnGodFolder.Add_Click({
    try { Start-Process explorer.exe ('shell:::' + $script:GodGuid) }
    catch { Log ('Could not open the GodMode folder: ' + $_.Exception.Message) 'ERROR' 'GodMode' }
})
