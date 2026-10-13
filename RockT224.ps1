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
