$btnBgImgPick.Add_Click({
    $dlg = New-Object Microsoft.Win32.OpenFileDialog
    $dlg.Title = 'Choose a background image'
    $dlg.Filter = 'Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif'
    if ($dlg.ShowDialog($window) -ne $true) { return }
    try {
        Get-ChildItem -LiteralPath $AppDir -Filter 'background*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        $dest = Join-Path $AppDir ('background_' + [DateTime]::Now.ToString('yyyyMMddHHmmssfff') + [System.IO.Path]::GetExtension($dlg.FileName).ToLower())
        Copy-Item -LiteralPath $dlg.FileName -Destination $dest -Force
        $script:Settings['BgImage'] = $dest
        Apply-Background; Apply-Theme; Sync-SettingsUi; Save-Settings
        Log 'Background image applied.' 'INFO' 'Settings'
    } catch { Log ('Could not set the image: ' + $_.Exception.Message) 'ERROR' 'Settings' }
})
