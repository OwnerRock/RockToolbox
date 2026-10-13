$btnWpLocal.Add_Click({
    $dlg = New-Object Microsoft.Win32.OpenFileDialog
    $dlg.Filter = 'Images|*.jpg;*.jpeg;*.png;*.bmp'
    if ($dlg.ShowDialog()) {
        Set-DesktopWallpaper $dlg.FileName ([string]$cmbWpStyle.SelectedItem)
        Log "Wallpaper applied: $($dlg.FileName)"
    }
})

$TaskName = 'Rock Daily Wallpaper'
