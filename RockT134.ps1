$btnGodDesktop.Add_Click({
    try {
        $path = Join-Path ([Environment]::GetFolderPath('Desktop')) ('GodMode.' + $script:GodGuid)
        if (Test-Path -LiteralPath $path) { Log 'GodMode folder already exists on the Desktop.' 'INFO' 'GodMode' }
        else { New-Item -ItemType Directory -Path $path | Out-Null; Log 'GodMode folder created on the Desktop.' 'INFO' 'GodMode' }
    } catch { Log ('Could not create the folder: ' + $_.Exception.Message) 'ERROR' 'GodMode' }
})

# ------------------------------------------------------------------
# Windows Update page (Windows Update Agent API, worked in a background runspace
