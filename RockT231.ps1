$sldBgOpacity.Add_ValueChanged({
    $v = [int][Math]::Round($sldBgOpacity.Value)
    $txtBgOpacity.Text = "$v%"
    if ($script:SettingsBusy) { return }
    $script:Settings['BgOpacity'] = $v
    $imgBg.Opacity = $v / 100.0
    Save-Soon
})

Build-Swatches
