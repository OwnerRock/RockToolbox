# Module: features/Background - Background color / image / opacity
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- background color / image / opacity
$script:SaveTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:SaveTimer.Interval = [TimeSpan]::FromMilliseconds(400)
$script:SaveTimer.Add_Tick({ $script:SaveTimer.Stop(); Save-Settings })
function Save-Soon { $script:SaveTimer.Stop(); $script:SaveTimer.Start() }

function Apply-Background {
    $ok = $false
    if (Test-BgImage) {
        try {
            # read the bytes ourselves so WPF never serves an older image from its URI cache
            $bytes = [System.IO.File]::ReadAllBytes([string]$script:Settings['BgImage'])
            $ms = New-Object System.IO.MemoryStream -ArgumentList @(, $bytes)
            # BitmapFrame decodes straight from the stream (no URI cache key needed, no stale image)
            $bmp = [System.Windows.Media.Imaging.BitmapFrame]::Create($ms,
                [System.Windows.Media.Imaging.BitmapCreateOptions]::IgnoreImageCache,
                [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
            if ($bmp.CanFreeze) { $bmp.Freeze() }
            $ms.Dispose()
            $imgBg.Source = $bmp
            $imgBg.Opacity = [double]$script:Settings['BgOpacity'] / 100.0
            $imgBg.Visibility = 'Visible'
            Apply-BgBlur
            $ok = $true
        } catch { Log ('Could not load the background image: ' + $_.Exception.Message) 'ERROR' 'Settings' }
    }
    if (-not $ok) { $imgBg.Source = $null; $imgBg.Visibility = 'Collapsed' }
}

function Apply-BgBlur {
    $r = [double]$script:Settings['BgBlur']
    if ($r -le 0) {
        $imgBg.Effect = $null
        $imgBg.RenderTransform = [System.Windows.Media.Transform]::Identity
        return
    }
    $fx = $imgBg.Effect -as [System.Windows.Media.Effects.BlurEffect]
    if (-not $fx) {
        $fx = New-Object System.Windows.Media.Effects.BlurEffect
        $fx.KernelType = 'Gaussian'
        $fx.RenderingBias = 'Performance'
        $imgBg.Effect = $fx
    }
    $fx.Radius = $r
    # grow the picture a little so the blurred edges stay outside the window
    $k = 1.0 + $r / 300.0
    $imgBg.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $imgBg.RenderTransform = New-Object System.Windows.Media.ScaleTransform($k, $k)
}
$sldBgBlur.Add_ValueChanged({
    $v = [int][Math]::Round($sldBgBlur.Value)
    $txtBgBlur.Text = "$v"
    if ($script:SettingsBusy) { return }
    $script:Settings['BgBlur'] = $v
    Apply-BgBlur
    Save-Soon
})
$btnBgColorApply.Add_Click({
    $x = $txtBgHex.Text.Trim()
    if ($x -notmatch '^#?[0-9A-Fa-f]{6}$') { Log 'Enter a color like #1A2233.' 'WARN' 'Settings'; return }
    if (-not $x.StartsWith('#')) { $x = '#' + $x }
    $script:Settings['BgColor'] = $x.ToUpper()
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log ('Background color: ' + $x.ToUpper()) 'INFO' 'Settings'
})
$btnBgColorClear.Add_Click({
    $script:Settings['BgColor'] = ''
    Apply-Theme; Sync-SettingsUi; Save-Settings
    Log 'Custom background color cleared.' 'INFO' 'Settings'
})
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
$btnBgImgClear.Add_Click({
    try { Get-ChildItem -LiteralPath $AppDir -Filter 'background*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue } catch { }
    $script:Settings['BgImage'] = ''
    Apply-Background; Apply-Theme; Sync-SettingsUi; Save-Settings
    Log 'Background image removed.' 'INFO' 'Settings'
})
$btnBgRestore.Add_Click({
    try { Get-ChildItem -LiteralPath $AppDir -Filter 'background*' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue } catch { }
    $script:Settings['BgImage'] = ''; $script:Settings['BgColor'] = ''
    $script:Settings['Background'] = 'Default'; $script:Settings['BgOpacity'] = 40; $script:Settings['BgBlur'] = 0
    Apply-Background; Apply-Theme; Sync-SettingsUi; Save-Settings
    Log 'Background restored to default.' 'INFO' 'Settings'
})
$sldBgOpacity.Add_ValueChanged({
    $v = [int][Math]::Round($sldBgOpacity.Value)
    $txtBgOpacity.Text = "$v%"
    if ($script:SettingsBusy) { return }
    $script:Settings['BgOpacity'] = $v
    $imgBg.Opacity = $v / 100.0
    Save-Soon
})

Build-Swatches
Sync-SettingsUi
Apply-Settings
Apply-Scroll
Apply-Background
Update-TaskbarCheck
if ([bool]$script:Settings['HideConsole']) { Set-ConsoleVisible $false }

