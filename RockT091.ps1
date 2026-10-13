$script:WpSearchScript = {
    param($st, $mode, $query, $title, $page)
    $ErrorActionPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $hdr = @{ 'User-Agent' = 'Mozilla/5.0 RockToolbox' }
    $wh = @(); $wm = @(); $ov = @()

    # --- Wallhaven (wallpapers, matched by tags; supports "like:ID" for similar pictures)
    try {
        $q = $query
        $sort = 'relevance'
        if ($mode -eq 'feed') { $q = ''; $sort = 'toplist' }
        elseif ($mode -eq 'like') { $q = 'like:' + $query }
        $u = 'https://wallhaven.cc/api/v1/search?q=' + [uri]::EscapeDataString($q) + '&categories=111&purity=100&sorting=' + $sort + '&order=desc&atleast=1280x720&page=' + $page
        if ($sort -eq 'toplist') { $u += '&topRange=1M' }
        $r = Invoke-RestMethod -Uri $u -Headers $hdr -TimeoutSec 25
        foreach ($w in $r.data) {
            $wh += [pscustomobject]@{ Thumb = [string]$w.thumbs.large; Full = [string]$w.path; Title = ('Wallhaven ' + $w.id); W = [int]$w.dimension_x; H = [int]$w.dimension_y; Src = 'Wallhaven'; Id = [string]$w.id }
        }
    } catch { }

    $text = $query
    if ($mode -eq 'like') { $text = $title }
    if ($mode -ne 'feed' -and $text -and ($mode -ne 'like' -or ($title -and $title -notlike 'Wallhaven *'))) {
        $qe = [uri]::EscapeDataString($text)
        # --- Wikimedia Commons (large free photos)
        try {
            $off = ($page - 1) * 20
            $u = 'https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=20&gsroffset=' + $off + '&gsrsearch=' + $qe + '%20filetype:bitmap&prop=imageinfo&iiprop=url|size|mime&iiurlwidth=520'
            $r = Invoke-RestMethod -Uri $u -Headers $hdr -TimeoutSec 25
            if ($r.query.pages) {
                foreach ($p in $r.query.pages.PSObject.Properties) {
                    $pg = $p.Value
                    $ii = $pg.imageinfo | Select-Object -First 1
                    if (-not $ii) { continue }
                    if ($ii.width -lt 1400 -or $ii.height -lt 700) { continue }
                    if ($ii.mime -ne 'image/jpeg' -and $ii.mime -ne 'image/png') { continue }
                    $t = ([string]$pg.title) -replace '^File:', '' -replace '\.[A-Za-z]+$', ''
                    $wm += [pscustomobject]@{ Thumb = [string]$ii.thumburl; Full = [string]$ii.url; Title = $t; W = [int]$ii.width; H = [int]$ii.height; Src = 'Wikimedia'; Id = '' }
                }
            }
        } catch { }
        # --- Openverse (open-licensed photos)
        try {
            $u = 'https://api.openverse.org/v1/images/?q=' + $qe + '&page=' + $page + '&page_size=20&aspect_ratio=wide&size=large&mature=false'
            $r = Invoke-RestMethod -Uri $u -Headers $hdr -TimeoutSec 25
            foreach ($x in $r.results) {
                if (-not $x.url -or -not $x.thumbnail) { continue }
                $ov += [pscustomobject]@{ Thumb = [string]$x.thumbnail; Full = [string]$x.url; Title = [string]$x.title; W = [int]$x.width; H = [int]$x.height; Src = 'Openverse'; Id = '' }
            }
        } catch { }
    }

    # mix the sources one by one so the grid looks varied
    $seen = @{}
    $max = [Math]::Max($wh.Count, [Math]::Max($wm.Count, $ov.Count))
    for ($i = 0; $i -lt $max; $i++) {
        foreach ($list in @($wh, $wm, $ov)) {
            if ($i -lt $list.Count) {
                $x = $list[$i]
                if (-not $seen.ContainsKey($x.Full)) { $seen[$x.Full] = $true; $x }
            }
        }
    }
}.ToString()

function Select-WpCard {
    param($Card)
    if ($script:wpSel) { $script:wpSel.BorderBrush = Get-Brush '#00000000' }
    $script:wpSel = $Card
    Set-Res $Card 'BorderBrush' '#4F8CFF'
    $txtWpInfo.Text = "Selected: " + $Card.Tag.Title
}

function Get-WpColCount {
    $w = [double]$wpScroll.ViewportWidth
    if ($w -lt 50) { $w = [double]$wpScroll.ActualWidth }
    if ($w -lt 50) { $w = 1000.0 }
    return [Math]::Max(1, [int][Math]::Floor(($w - 16) / 290))
}

function New-WpCard {
    param($It)
    $img = New-Object System.Windows.Controls.Image
    $img.Stretch = 'UniformToFill'; $img.Height = 160; $img.Opacity = 0
    $img.ClipToBounds = $true
    try {
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
        $bmp.BeginInit()
        $bmp.UriSource = [Uri]$It.Thumb
        $bmp.DecodePixelWidth = 520
        $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        $bmp.EndInit()
        $img.Source = $bmp
        $fadeIn = {
            $a = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, (New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(380))))
            $img.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $a)
        }.GetNewClosure()
        $failed = { $img.Opacity = 1 }.GetNewClosure()
        if ($bmp.IsDownloading) {
            $bmp.add_DownloadCompleted([System.EventHandler]$fadeIn)
            $bmp.add_DownloadFailed([System.EventHandler[System.Windows.Media.ExceptionEventArgs]]$failed)
        } else { & $fadeIn }
    } catch { $img.Opacity = 1 }
    $cap = New-Object System.Windows.Controls.TextBlock
    $cap.Text = ([string]$It.Src + '  -  ' + $It.W + 'x' + $It.H)
    Set-Res $cap 'Foreground' '#C9CDE0'; $cap.FontSize = 11
    $cap.Margin = '6,4,6,5'; $cap.TextTrimming = 'CharacterEllipsis'
    $sp = New-Object System.Windows.Controls.StackPanel
    [void]$sp.Children.Add($img); [void]$sp.Children.Add($cap)
    $card = New-Object System.Windows.Controls.Border
    $card.Child = $sp; $card.Tag = $It; $card.Margin = '6'
    $card.BorderThickness = '3'; $card.BorderBrush = Get-Brush '#00000000'
    Set-Res $card 'Background' '#12131A'; $card.Cursor = [System.Windows.Input.Cursors]::Hand
    $card.ToolTip = $It.Title
    Initialize-CardAnim $card
    $card.Add_MouseEnter({ param($s, $e) Animate-Card $s 1.03 -2 130 })
    $card.Add_MouseLeave({ param($s, $e) Animate-Card $s 1.0 0 170 })
    $card.Add_MouseLeftButtonDown({
        param($s, $e)
        Select-WpCard $s
        if ($e.ClickCount -ge 2) { Apply-WpItem $s.Tag }
    })
    return $card
}

function Place-WpCard {
    param($Card)
    $idx = 0
    for ($i = 1; $i -lt $script:wpCols; $i++) { if ($script:wpColH[$i] -lt $script:wpColH[$idx]) { $idx = $i } }
    $cw = ([double]$wpScroll.ViewportWidth - 16) / $script:wpCols - 12
    if ($cw -lt 100) { $cw = 260 }
    $it = $Card.Tag
    $ratio = 0.6
    if ($it.W -gt 0 -and $it.H -gt 0) { $ratio = [double]$it.H / [double]$it.W }
    $ih = [Math]::Max(110.0, [Math]::Min(460.0, $cw * $ratio))
    $Card.Child.Children[0].Height = $ih
    $script:wpColH[$idx] = $script:wpColH[$idx] + $ih + 34
    [void]$script:wpColPanels[$idx].Children.Add($Card)
}

function Layout-Wp {
    foreach ($sp in $script:wpColPanels) { $sp.Children.Clear() }
    $wpPanel.Children.Clear()
    $wpPanel.ColumnDefinitions.Clear()
    $cols = Get-WpColCount
    $script:wpCols = $cols
    $script:wpColPanels = @()
    $script:wpColH = New-Object 'double[]' $cols
    for ($i = 0; $i -lt $cols; $i++) {
        $cd = New-Object System.Windows.Controls.ColumnDefinition
        $cd.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
        [void]$wpPanel.ColumnDefinitions.Add($cd)
        $sp = New-Object System.Windows.Controls.StackPanel
        [System.Windows.Controls.Grid]::SetColumn($sp, $i)
        [void]$wpPanel.Children.Add($sp)
        $script:wpColPanels += $sp
    }
    foreach ($c in $script:wpCards) { Place-WpCard $c }
}

function Add-WpBatch {
    param($Items)
    if ($script:wpColPanels.Count -eq 0 -or $script:wpCols -ne (Get-WpColCount)) { Layout-Wp }
    $n = 0
    foreach ($it in $Items) {
        [void]$script:wpItems.Add($it)
        $card = New-WpCard $it
        [void]$script:wpCards.Add($card)
        Place-WpCard $card
        Start-CardFade $card ([Math]::Min($n, 20) * 30)
        $n++
    }
}

function Set-DesktopWallpaper {
    param([string]$Path, [string]$Style = 'Fill')
    $map = @{ Fill = @('10', '0'); Fit = @('6', '0'); Stretch = @('2', '0'); Tile = @('0', '1'); Center = @('0', '0'); Span = @('22', '0') }
    $m = $map[$Style]; if (-not $m) { $m = $map['Fill'] }
    Set-Reg 'HKCU:\Control Panel\Desktop' 'WallpaperStyle' $m[0] 'String'
    Set-Reg 'HKCU:\Control Panel\Desktop' 'TileWallpaper' $m[1] 'String'
    [void][WinToolboxNative]::SystemParametersInfo(20, 0, $Path, 3)
}

function Download-WpItem {
    param($Item, [string]$Folder)
    $ext = [IO.Path]::GetExtension(($Item.Full -split '\?')[0])
    if (-not $ext -or $ext.Length -gt 5) { $ext = '.jpg' }
    $file = Join-Path $Folder ("wp_{0}{1}" -f ([guid]::NewGuid().ToString('N').Substring(0, 8)), $ext)
    Log "Downloading: $($Item.Title)"
    Get-RemoteFile $Item.Full $file
    return $file
}

function Set-LockScreenImage {
    param([string]$Path)
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
    $null = [Windows.System.UserProfile.LockScreen, Windows.System.UserProfile, ContentType = WindowsRuntime]
    $ext = [System.WindowsRuntimeSystemExtensions]
    $asTaskGeneric = ($ext.GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
    $asTaskAction  = ($ext.GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and -not $_.IsGenericMethod })[0]
    $op = [Windows.Storage.StorageFile]::GetFileFromPathAsync($Path)
    $t1 = $asTaskGeneric.MakeGenericMethod([Windows.Storage.StorageFile]).Invoke($null, @($op))
    [void]$t1.Wait(-1)
    $file = $t1.Result
    $act = [Windows.System.UserProfile.LockScreen]::SetImageFileAsync($file)
    $t2 = $asTaskAction.Invoke($null, @($act))
    [void]$t2.Wait(-1)
}

function Apply-WpItem {
    param($Item, [string]$Target = 'Desktop')
    Start-Work {
        $file = Download-WpItem $Item $WpDir
        if ($Target -eq 'Desktop' -or $Target -eq 'Both') {
            Set-DesktopWallpaper $file ([string]$cmbWpStyle.SelectedItem)
            Log 'Desktop wallpaper applied.' 'INFO' 'Wallpaper'
        }
        if ($Target -eq 'Lock' -or $Target -eq 'Both') {
            try {
                Set-LockScreenImage $file
                Log 'Lock screen image applied (press Win+L to preview).' 'INFO' 'Wallpaper'
            } catch {
                Log ('Could not set the lock screen: ' + $_.Exception.Message) 'ERROR' 'Wallpaper'
            }
        }
        Get-ChildItem $WpDir -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -Skip 20 | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

function Start-WpSearch {
    param([string]$Mode, [string]$Query, [string]$Title, [bool]$Append)
    if ($script:Busy) { return }
    Start-Work {
        if ($Append) { $script:wpPage++ }
        else { $script:wpPage = 1; $script:wpMode = $Mode; $script:wpQuery = $Query; $script:wpTitle = $Title }
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Searching pictures...'
        $rows = Invoke-BgJob $script:WpSearchScript @($script:wpMode, $script:wpQuery, $script:wpTitle, $script:wpPage) $st
        $seen = @{}
        foreach ($x in $script:wpItems) { $seen[[string]$x.Full] = $true }
        $new = @($rows | Where-Object { $_ -and $_.Full -and $_.Thumb -and -not $seen.ContainsKey([string]$_.Full) })
        if ($new.Count -eq 0) {
            $script:wpHasMore = $false
            if ($Append) { $script:wpPage--; $txtWpInfo.Text = 'No more results for this search.' }
            else { $txtWpInfo.Text = 'No pictures found - try another word (for example: forest, space, sports car).'; Log 'No pictures found.' 'WARN' 'Wallpaper' }
            return
        }
        if (-not $Append) {
            foreach ($sp in $script:wpColPanels) { $sp.Children.Clear() }
            $script:wpItems.Clear(); $script:wpCards.Clear(); $script:wpSel = $null
            $wpScroll.ScrollToTop()
        }
        $script:wpHasMore = $true
        $script:wpLoaded = $true
        Add-WpBatch $new
        if ($script:wpMode -eq 'feed') { $what = 'Trending wallpapers' }
        elseif ($script:wpMode -eq 'like') { $what = 'Similar to your pick' }
        else { $what = 'Results for "' + $script:wpQuery + '"' }
        $txtWpInfo.Text = "$what - $($script:wpItems.Count) pictures. Click one to select it, double-click to set it as wallpaper, or press Find similar."
        Log "$what : $($script:wpItems.Count) pictures." 'INFO' 'Wallpaper'
    } 'Searching pictures...'
}

function Invoke-WpSearchBox {
    $q = $txtWpQuery.Text.Trim()
    if (-not $q) { Start-WpSearch 'feed' '' '' $false } else { Start-WpSearch 'search' $q '' $false }
}

$btnWpSearch.Add_Click({ Invoke-WpSearchBox })
