$btnWpSave.Add_Click({
    if (-not $script:wpSel) { Log 'Select a wallpaper first.'; return }
    $item = $script:wpSel.Tag
    Start-Work {
        $dir = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'WinToolbox'
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $f = Download-WpItem $item $dir
        Log "Saved: $f"
    }
})
