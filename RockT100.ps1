$btnWpLockLocal.Add_Click({
    $dlg = New-Object Microsoft.Win32.OpenFileDialog
    $dlg.Filter = 'Images|*.jpg;*.jpeg;*.png;*.bmp'
    if ($dlg.ShowDialog()) {
        $picked = $dlg.FileName
        Start-Work {
            try {
                Set-LockScreenImage $picked
                Log "Lock screen image applied: $picked" 'INFO' 'Wallpaper'
            } catch {
                Log ('Could not set the lock screen: ' + $_.Exception.Message) 'ERROR' 'Wallpaper'
            }
        }
    }
})
