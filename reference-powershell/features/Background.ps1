<<<<<<< HEAD
﻿# ==============================================================================
# MODULE: src\features\Background.ps1
# ==============================================================================
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
            $script:BgOriginal = $bmp
            $script:BgBlurCache = @{}
            $imgBg.Source = $bmp
            $imgBg.Opacity = [double]$script:Settings['BgOpacity'] / 100.0
            $imgBg.Visibility = 'Visible'
            Apply-BgBlur
            $ok = $true
        } catch { Log ('Could not load the background image: ' + $_.Exception.Message) 'ERROR' 'Settings' }
    }
    if (-not $ok) { $script:BgOriginal = $null; $imgBg.Source = $null; $imgBg.Visibility = 'Collapsed' }
}

# the blurred background is computed ONCE: shrink the picture, box-blur the small copy in C#, show it stretched.
# (a live BlurEffect on a window-sized image was re-rendered on every frame = the main source of stutter)
$script:BgOriginal = $null
$script:BgBlurCache = @{}
function Get-BgBlurSource {
    param([double]$Radius)
    $src = $script:BgOriginal
    if (-not $src) { return $null }
    if ($Radius -le 0) { return $src }
    $key = [string][int]$Radius
    if ($script:BgBlurCache.ContainsKey($key)) { return $script:BgBlurCache[$key] }
    try {
        if (-not ('RockBlur' -as [type])) {
            Add-Type -TypeDefinition @'
public static class RockBlur {
    public static void Box(byte[] px, int w, int h, int r, int passes) {
        if (r < 1) return;
        byte[] tmp = new byte[px.Length];
        for (int p = 0; p < passes; p++) { Pass(px, tmp, w, h, r, true); Pass(tmp, px, w, h, r, false); }
    }
    static void Pass(byte[] src, byte[] dst, int w, int h, int r, bool horiz) {
        int len = horiz ? w : h; int lines = horiz ? h : w;
        int step = horiz ? 4 : w * 4; int lineStep = horiz ? w * 4 : 4;
        int win = 2 * r + 1;
        for (int l = 0; l < lines; l++) {
            int baseIdx = l * lineStep;
            for (int c = 0; c < 4; c++) {
                int sum = 0;
                for (int k = -r; k <= r; k++) { int i = k < 0 ? 0 : (k >= len ? len - 1 : k); sum += src[baseIdx + i * step + c]; }
                for (int x = 0; x < len; x++) {
                    dst[baseIdx + x * step + c] = (byte)(sum / win);
                    int addI = x + r + 1; if (addI >= len) addI = len - 1;
                    int subI = x - r; if (subI < 0) subI = 0;
                    sum += src[baseIdx + addI * step + c] - src[baseIdx + subI * step + c];
                }
            }
        }
    }
}
'@
        }
        $ww = [Math]::Min(320, [int]$src.PixelWidth)
        $sc = [double]$ww / [double]$src.PixelWidth
        $hh = [Math]::Max(8, [int][Math]::Round($src.PixelHeight * $sc))
        $dv = New-Object System.Windows.Media.DrawingVisual
        [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($dv, [System.Windows.Media.BitmapScalingMode]::Fant)
        $dc = $dv.RenderOpen()
        $dc.DrawImage($src, (New-Object System.Windows.Rect(0, 0, $ww, $hh)))
        $dc.Close()
        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($ww, $hh, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($dv)
        $stride = $ww * 4
        $px = New-Object byte[] ($stride * $hh)
        $rtb.CopyPixels($px, $stride, 0)
        $rad = [Math]::Max(1, [int][Math]::Round($Radius * $ww / 1500.0 * 1.3))
        [RockBlur]::Box($px, $ww, $hh, $rad, 3)
        $out = [System.Windows.Media.Imaging.BitmapSource]::Create($ww, $hh, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32, $null, $px, $stride)
        $out.Freeze()
        $script:BgBlurCache[$key] = $out
        return $out
    } catch { return $src }
}

function Apply-BgBlur {
    if (-not $script:BgOriginal) { return }
    $r = [double]$script:Settings['BgBlur']
    $imgBg.Effect = $null
    $imgBg.RenderTransform = [System.Windows.Media.Transform]::Identity
    $src = Get-BgBlurSource $r
    if ($src) { $imgBg.Source = $src }
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($imgBg, [System.Windows.Media.BitmapScalingMode]::HighQuality)
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


=======
﻿# ==============================================================================
# MODULE: src\features\Background.ps1
# ==============================================================================
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
            $script:BgOriginal = $bmp
            $script:BgBlurCache = @{}
            $imgBg.Source = $bmp
            $imgBg.Opacity = [double]$script:Settings['BgOpacity'] / 100.0
            $imgBg.Visibility = 'Visible'
            Apply-BgBlur
            $ok = $true
        } catch { Log ('Could not load the background image: ' + $_.Exception.Message) 'ERROR' 'Settings' }
    }
    if (-not $ok) { $script:BgOriginal = $null; $imgBg.Source = $null; $imgBg.Visibility = 'Collapsed' }
}

# the blurred background is computed ONCE: shrink the picture, box-blur the small copy in C#, show it stretched.
# (a live BlurEffect on a window-sized image was re-rendered on every frame = the main source of stutter)
$script:BgOriginal = $null
$script:BgBlurCache = @{}
function Get-BgBlurSource {
    param([double]$Radius)
    $src = $script:BgOriginal
    if (-not $src) { return $null }
    if ($Radius -le 0) { return $src }
    $key = [string][int]$Radius
    if ($script:BgBlurCache.ContainsKey($key)) { return $script:BgBlurCache[$key] }
    try {
        if (-not ('RockBlur' -as [type])) {
            Add-Type -TypeDefinition @'
public static class RockBlur {
    public static void Box(byte[] px, int w, int h, int r, int passes) {
        if (r < 1) return;
        byte[] tmp = new byte[px.Length];
        for (int p = 0; p < passes; p++) { Pass(px, tmp, w, h, r, true); Pass(tmp, px, w, h, r, false); }
    }
    static void Pass(byte[] src, byte[] dst, int w, int h, int r, bool horiz) {
        int len = horiz ? w : h; int lines = horiz ? h : w;
        int step = horiz ? 4 : w * 4; int lineStep = horiz ? w * 4 : 4;
        int win = 2 * r + 1;
        for (int l = 0; l < lines; l++) {
            int baseIdx = l * lineStep;
            for (int c = 0; c < 4; c++) {
                int sum = 0;
                for (int k = -r; k <= r; k++) { int i = k < 0 ? 0 : (k >= len ? len - 1 : k); sum += src[baseIdx + i * step + c]; }
                for (int x = 0; x < len; x++) {
                    dst[baseIdx + x * step + c] = (byte)(sum / win);
                    int addI = x + r + 1; if (addI >= len) addI = len - 1;
                    int subI = x - r; if (subI < 0) subI = 0;
                    sum += src[baseIdx + addI * step + c] - src[baseIdx + subI * step + c];
                }
            }
        }
    }
}
'@
        }
        $ww = [Math]::Min(320, [int]$src.PixelWidth)
        $sc = [double]$ww / [double]$src.PixelWidth
        $hh = [Math]::Max(8, [int][Math]::Round($src.PixelHeight * $sc))
        $dv = New-Object System.Windows.Media.DrawingVisual
        [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($dv, [System.Windows.Media.BitmapScalingMode]::Fant)
        $dc = $dv.RenderOpen()
        $dc.DrawImage($src, (New-Object System.Windows.Rect(0, 0, $ww, $hh)))
        $dc.Close()
        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($ww, $hh, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($dv)
        $stride = $ww * 4
        $px = New-Object byte[] ($stride * $hh)
        $rtb.CopyPixels($px, $stride, 0)
        $rad = [Math]::Max(1, [int][Math]::Round($Radius * $ww / 1500.0 * 1.3))
        [RockBlur]::Box($px, $ww, $hh, $rad, 3)
        $out = [System.Windows.Media.Imaging.BitmapSource]::Create($ww, $hh, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32, $null, $px, $stride)
        $out.Freeze()
        $script:BgBlurCache[$key] = $out
        return $out
    } catch { return $src }
}

function Apply-BgBlur {
    if (-not $script:BgOriginal) { return }
    $r = [double]$script:Settings['BgBlur']
    $imgBg.Effect = $null
    $imgBg.RenderTransform = [System.Windows.Media.Transform]::Identity
    $src = Get-BgBlurSource $r
    if ($src) { $imgBg.Source = $src }
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($imgBg, [System.Windows.Media.BitmapScalingMode]::HighQuality)
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


>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
