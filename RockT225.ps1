$sldBgBlur.Add_ValueChanged({
    $v = [int][Math]::Round($sldBgBlur.Value)
    $txtBgBlur.Text = "$v"
    if ($script:SettingsBusy) { return }
    $script:Settings['BgBlur'] = $v
    Apply-BgBlur
    Save-Soon
})
