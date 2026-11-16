# ==============================================================================
# MODULE: src\pages\Install.ps1
# ==============================================================================
# Module: pages/Install - Install page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Build UI: Install
# ------------------------------------------------------------------
$script:Selected  = @{}
$script:Sections  = New-Object System.Collections.ArrayList
$script:AppCards  = New-Object System.Collections.ArrayList
$script:Chips     = New-Object System.Collections.ArrayList
$script:CatFilter = 'All'
$palette = '#E5484D', '#F5A524', '#17C964', '#0E8AAA', '#7B5CFF', '#F31260', '#06B6D4', '#8E4EC6', '#3E63DD', '#12A594'

function Set-CardLook {
    param($Card, [bool]$On)
    if ($On) {
        $ac = '#4FD1FF'; if (Test-Modern) { $ac = '#FF375F' }
        Set-Res $Card 'Background' '#1E3A4C'; Set-Res $Card 'BorderBrush' $ac
        $Card.BorderThickness = New-Object System.Windows.Thickness(1.5)   # glow effect removed: it re-rendered a blur for every selected card
    } else {
        Set-Res $Card 'Background' '#23272D'; Set-Res $Card 'BorderBrush' '#343A44'
        $Card.BorderThickness = New-Object System.Windows.Thickness(1)
    }
}
function Find-ChildImage($El) {
    if ($El -is [System.Windows.Controls.Image]) { return $El }
    $kids = @()
    if ($El -is [System.Windows.Controls.Panel]) { $kids = @($El.Children) }
    elseif ($El -is [System.Windows.Controls.Decorator]) { if ($El.Child) { $kids = @($El.Child) } }
    elseif ($El -is [System.Windows.Controls.ContentControl]) { if ($El.Content -is [System.Windows.UIElement]) { $kids = @($El.Content) } }
    foreach ($k in $kids) { $r = Find-ChildImage $k; if ($r) { return $r } }
    return $null
}
function Get-AppCardImage([string]$Id) {
    foreach ($it in $script:AppCards) { if ([string]$it.Card.Tag.Id -eq $Id) { return (Find-ChildImage $it.Card.Child) } }
    return $null
}
# the real app icon (shared with the app card, so it appears the moment the card's icon has loaded), letter badge as the fallback
function New-AppIconVisual([string]$Id, [string]$Name, [double]$Size) {
    $pc = [string]$script:CartPalette[[Math]::Abs($Id.GetHashCode() % $script:CartPalette.Count)]
    $b = New-Object System.Windows.Controls.Border
    $b.Width = $Size; $b.Height = $Size; $b.CornerRadius = New-Object System.Windows.CornerRadius ($Size / 3.2)
    $b.Background = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($pc))
    $g = New-Object System.Windows.Controls.Grid
    $lt = New-Object System.Windows.Controls.TextBlock
    $lt.Text = $Name.Substring(0, 1).ToUpper(); $lt.FontSize = $Size * 0.45; $lt.FontWeight = 'Bold'; $lt.Foreground = [System.Windows.Media.Brushes]::White
    $lt.HorizontalAlignment = 'Center'; $lt.VerticalAlignment = 'Center'
    [void]$g.Children.Add($lt)
    $src = Get-AppCardImage $Id
    if ($src) {
        $img = New-Object System.Windows.Controls.Image
        $img.Width = $Size * 0.78; $img.Height = $Size * 0.78; $img.HorizontalAlignment = 'Center'; $img.VerticalAlignment = 'Center'
        [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($img, [System.Windows.Media.BitmapScalingMode]::HighQuality)
        $bind = New-Object System.Windows.Data.Binding('Source'); $bind.Source = $src; $bind.Mode = 'OneWay'
        [void]$img.SetBinding([System.Windows.Controls.Image]::SourceProperty, $bind)
        [void]$g.Children.Add($img)
    }
    $b.Child = $g
    return $b
}
$script:CartPalette = @('#0A84FF', '#30D158', '#FF9F0A', '#BF5AF2', '#FF453A', '#5AC8FA', '#FF375F', '#5E5CE6')
$script:AppNameById = $null
function Get-AppNameById([string]$Id) {
    if (-not $script:AppNameById) { $h = @{}; foreach ($a in $Apps) { $h[[string]$a.Id] = [string]$a.Name }; $script:AppNameById = $h }
    if ($script:AppNameById.ContainsKey($Id)) { return $script:AppNameById[$Id] }
    return $Id
}
function Remove-CartItem([string]$Id) {
    [void]$script:Selected.Remove($Id)
    foreach ($it in $script:AppCards) { if ([string]$it.Card.Tag.Id -eq $Id) { Set-CardLook $it.Card $false; break } }
    Update-SelCount
}
function Update-CartPanel {
    if (-not $spCart) { return }
    $spCart.Children.Clear()
    if ($txtCartTotal) { $txtCartTotal.Text = '' }
    $ids = @($script:Selected.Keys | ForEach-Object { [string]$_ } | Sort-Object)
    if ($ids.Count -eq 0) {
        $t = New-Object System.Windows.Controls.TextBlock
        $t.Text = L 'Pick apps to build your list.'; $t.TextWrapping = 'Wrap'; $t.FontSize = 12; $t.Margin = '4,6,4,0'
        Set-Res $t 'Foreground' '#9AA0B8'
        [void]$spCart.Children.Add($t)
        return
    }
    $sumBytes = 0.0; $unknown = 0
    foreach ($id in $ids) {
        $row = New-Object System.Windows.Controls.Border
        $row.CornerRadius = New-Object System.Windows.CornerRadius 10; $row.Padding = '8,6'; $row.Margin = '0,0,0,5'
        Set-Res $row 'Background' '#23272D'
        $dp = New-Object System.Windows.Controls.DockPanel
        $x = New-Object System.Windows.Controls.TextBlock
        $x.Text = [string][char]0x00D7; $x.FontSize = 17; $x.Cursor = [System.Windows.Input.Cursors]::Hand; $x.Margin = '8,-3,2,0'; $x.VerticalAlignment = 'Center'; $x.Tag = $id
        Set-Res $x 'Foreground' '#9AA0B8'
        [System.Windows.Controls.DockPanel]::SetDock($x, [System.Windows.Controls.Dock]::Right)
        $x.Add_MouseLeftButtonUp({ param($s, $e) Remove-CartItem ([string]$s.Tag) })
        $name = Get-AppNameById $id
        $ic = New-AppIconVisual $id $name 28
        [System.Windows.Controls.DockPanel]::SetDock($ic, [System.Windows.Controls.Dock]::Left)
        $nm = New-Object System.Windows.Controls.TextBlock
        $nm.Text = $name; $nm.FontSize = 12.5; $nm.TextTrimming = 'CharacterEllipsis'; $nm.VerticalAlignment = 'Center'; $nm.Margin = '9,0,0,0'
        Set-Res $nm 'Foreground' '#F2F4FF'
        [void]$dp.Children.Add($x)
        if ($script:SizeCache.ContainsKey($id)) {
            $szt = New-Object System.Windows.Controls.TextBlock
            $szt.Text = Format-InstSize $script:SizeCache[$id]; $szt.FontSize = 11; $szt.FontFamily = 'Consolas'; $szt.VerticalAlignment = 'Center'
            Set-Res $szt 'Foreground' '#9AA0B8'
            [System.Windows.Controls.DockPanel]::SetDock($szt, [System.Windows.Controls.Dock]::Right)
            [void]$dp.Children.Add($szt)
            $sumBytes += [double]$script:SizeCache[$id]
        } else { $unknown++ }
        [void]$dp.Children.Add($ic); [void]$dp.Children.Add($nm)
        $row.Child = $dp
        [void]$spCart.Children.Add($row)
    }
    if ($txtCartTotal) {
        if ($sumBytes -gt 0) {
            $tt = (L 'Total') + ' ' + (Format-InstSize $sumBytes)
            if ($unknown -gt 0) { $tt += '  +  ' + [string]$unknown + ' ?' }
            $txtCartTotal.Text = $tt
        } else { $txtCartTotal.Text = '' }
    }
}
$script:SizeCachePath = Join-Path $AppDir 'sizes.json'
$script:SizeCache = @{}
$script:SizeQ = New-Object System.Collections.ArrayList
$script:SizeRun = @{}
$script:SizeFail = @{}
$script:SizeTimer = $null
$script:WgQuiet = $null
$script:ProbeScript = {
    param($wg, $id)
    try { [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072 } catch { }
    $len = [int64]0; $url = ''
    try { $o = (& $wg show --id $id --exact --accept-source-agreements 2>$null | Out-String); if ($o -match '(?im)^\s*Installer Url:\s*(https?://\S+)') { $url = $Matches[1] } } catch { }
    if ($url) {
        try { $q = [Net.HttpWebRequest]::Create($url); $q.Method = 'HEAD'; $q.Timeout = 8000; $q.UserAgent = 'Mozilla/5.0 RockToolbox'; $s = $q.GetResponse(); $len = [int64]$s.ContentLength; $s.Close() } catch { }
        if ($len -le 0) {
            try { $q = [Net.HttpWebRequest]::Create($url); $q.Timeout = 8000; $q.UserAgent = 'Mozilla/5.0 RockToolbox'; $q.AddRange(0, 0); $s = $q.GetResponse(); $cr = [string]$s.Headers['Content-Range']; $s.Close(); if ($cr -match '/(\d+)\s*$') { $len = [int64]$Matches[1] } } catch { }
        }
    }
    $len
    $url
}
function Load-SizeCache {
    try {
        if (Test-Path -LiteralPath $script:SizeCachePath) {
            $j = Read-LangJson $script:SizeCachePath
            foreach ($k in @($j.Keys)) { $script:SizeCache[[string]$k] = [double]$j[$k] }
        }
    } catch { }
}
function Save-SizeCache {
    try {
        $o = [ordered]@{}
        foreach ($k in @($script:SizeCache.Keys | Sort-Object)) { $o[[string]$k] = [int64]$script:SizeCache[$k] }
        if (-not (Test-Path -LiteralPath $AppDir)) { New-Item -ItemType Directory -Force -Path $AppDir | Out-Null }
        [System.IO.File]::WriteAllText($script:SizeCachePath, ($o | ConvertTo-Json -Compress), (New-Object System.Text.UTF8Encoding($false)))
    } catch { }
}
function Get-WingetPathQuiet {
    if ($script:WgQuiet) { return $script:WgQuiet }
    try { $c = Get-Command winget.exe -ErrorAction SilentlyContinue; if ($c) { $script:WgQuiet = [string]$c.Source } } catch { }
    return $script:WgQuiet
}
function Poll-AppSizes {
    $changed = $false
    foreach ($id in @($script:SizeRun.Keys)) {
        $run = $script:SizeRun[$id]
        if ($run.Ar.IsCompleted) {
            $len = [int64]0
            try { $res = @($run.Ps.EndInvoke($run.Ar)); if ($res.Count -ge 1) { $len = [int64]$res[0] } } catch { }
            try { $run.Ps.Dispose() } catch { }
            [void]$script:SizeRun.Remove($id)
            if ($len -gt 0) { $script:SizeCache[[string]$id] = [double]$len; $changed = $true } else { $script:SizeFail[[string]$id] = $true }
        }
    }
    $wg = Get-WingetPathQuiet
    while ($wg -and $script:SizeRun.Count -lt 3 -and $script:SizeQ.Count -gt 0) {
        $id = [string]$script:SizeQ[0]; $script:SizeQ.RemoveAt(0)
        if (-not $script:Selected.ContainsKey($id)) { continue }
        try {
            $ps = [powershell]::Create()
            [void]$ps.AddScript($script:ProbeScript).AddArgument($wg).AddArgument($id)
            $script:SizeRun[$id] = @{ Ps = $ps; Ar = $ps.BeginInvoke() }
        } catch { $script:SizeFail[$id] = $true }
    }
    if ($changed) { Save-SizeCache; Update-CartPanel }
    if ($script:SizeQ.Count -eq 0 -and $script:SizeRun.Count -eq 0 -and $script:SizeTimer) { $script:SizeTimer.Stop() }
}
function Request-AppSizes {
    if (-not (Get-WingetPathQuiet)) { return }
    foreach ($k in @($script:Selected.Keys)) {
        $id = [string]$k
        if ($script:SizeCache.ContainsKey($id) -or $script:SizeFail.ContainsKey($id) -or $script:SizeRun.ContainsKey($id) -or $script:SizeQ.Contains($id)) { continue }
        [void]$script:SizeQ.Add($id)
    }
    if ($script:SizeQ.Count -gt 0 -or $script:SizeRun.Count -gt 0) {
        if (-not $script:SizeTimer) {
            $t = New-Object System.Windows.Threading.DispatcherTimer
            $t.Interval = [TimeSpan]::FromMilliseconds(400)
            $t.Add_Tick({ Poll-AppSizes })
            $script:SizeTimer = $t
        }
        if (-not $script:SizeTimer.IsEnabled) { $script:SizeTimer.Start() }
        Poll-AppSizes
    }
}
Load-SizeCache
function Update-SelCount { $txtSelCount.Text = "$(L 'Selected Apps'): $($script:Selected.Count)"; Update-CartPanel; Request-AppSizes }
function Toggle-AppCard {
    param($Card)
    $id = $Card.Tag.Id
    if ($script:Selected.ContainsKey($id)) { $script:Selected.Remove($id) } else { $script:Selected[$id] = $true }
    Set-CardLook $Card ($script:Selected.ContainsKey($id))
    Update-SelCount
}

# ---- card animations (hover lift, press, selection pop, fade-in)
$script:CardAnimOn = $false
function Initialize-CardAnim {
    param($Card)
    $tg = [System.Windows.Media.TransformGroup]::new()
    [void]$tg.Children.Add([System.Windows.Media.ScaleTransform]::new(1.0, 1.0))
    [void]$tg.Children.Add([System.Windows.Media.TranslateTransform]::new(0.0, 0.0))
    $Card.RenderTransform = $tg
    $Card.RenderTransformOrigin = [System.Windows.Point]::new(0.5, 0.5)
}
function Animate-Card {
    param($Card, [double]$Scale, [double]$Y, [int]$Ms = 130, [string]$Ease = 'cubic')
    if (-not [bool]$script:Settings['CardAnim']) { return }
    $tg = $Card.RenderTransform
    if ($tg -isnot [System.Windows.Media.TransformGroup]) { return }
    if ($Ease -eq 'back') { $e = New-Object System.Windows.Media.Animation.BackEase; $e.Amplitude = 0.7 }
    else { $e = New-Object System.Windows.Media.Animation.CubicEase }
    $e.EasingMode = 'EaseOut'
    $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds($Ms))
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation
    $a.To = $Scale; $a.Duration = $dur; $a.EasingFunction = $e
    $tg.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $a)
    $tg.Children[0].BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $a)
    $b = New-Object System.Windows.Media.Animation.DoubleAnimation
    $b.To = $Y; $b.Duration = $dur; $b.EasingFunction = $e
    $tg.Children[1].BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $b)
}
function Start-CardFade {
    param($Card, [int]$DelayMs = 0)
    if (-not [bool]$script:Settings['CardAnim']) { return }
    try {
        $e = New-Object System.Windows.Media.Animation.CubicEase
        $e.EasingMode = 'EaseOut'
        $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(280))
        $begin = [TimeSpan]::FromMilliseconds($DelayMs)
        $Card.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $null)
        $Card.Opacity = 0.0
        $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
        $fade.BeginTime = $begin; $fade.EasingFunction = $e
        $Card.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
        $tg = $Card.RenderTransform
        if ($tg -is [System.Windows.Media.TransformGroup]) {
            $slide = New-Object System.Windows.Media.Animation.DoubleAnimation(14.0, 0.0, $dur)
            $slide.BeginTime = $begin; $slide.EasingFunction = $e
            $tg.Children[1].BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $slide)
        }
    } catch { }
}
function Start-CardsIntro {
    $i = 0
    foreach ($it in $script:AppCards) {
        if ($it.Card.Visibility -ne 'Visible') { continue }
        Start-CardFade $it.Card (120 + $i * 16)
        $i++
        if ($i -ge 40) { break }
    }
    $script:CardAnimOn = $true
}

# ---- app icons: downloaded in the background (4 at a time), several sources per app, cached on disk ----
$IconDir = Join-Path $AppDir 'Icons'
New-Item -ItemType Directory -Force -Path $IconDir | Out-Null
$IconUrls = @(
    'https://www.google.com/s2/favicons?domain={0}&sz=128',
    'https://www.{0}/apple-touch-icon.png',
    'https://{0}/apple-touch-icon.png',
    'https://icons.duckduckgo.com/ip3/{0}.ico',
    'https://icon.horse/icon/{0}',
    'https://www.google.com/s2/favicons?domain_url=https://www.{0}&sz=64',
    'https://{0}/favicon.ico',
    'https://www.{0}/favicon.ico'
)
$script:IconQueue = New-Object System.Collections.Queue
$script:IconJobs  = New-Object System.Collections.ArrayList
$script:DomFrame  = @{}
$script:DomWait   = @{}

function Read-IconBytes {
    param([byte[]]$Bytes)
    try {
        $ms = New-Object System.IO.MemoryStream(, $Bytes)
        $dec = [System.Windows.Media.Imaging.BitmapDecoder]::Create($ms, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        $best = $null
        foreach ($f in $dec.Frames) { if (-not $best -or $f.PixelWidth -gt $best.PixelWidth) { $best = $f } }
        if (-not $best) { return $null }
        try { $best.Freeze() } catch { }
        return @{ Frame = $best; W = [int]$best.PixelWidth }
    } catch { return $null }
}

function Apply-AppIcon {
    param($Slot, $Frame)
    $Slot.Img.Source = $Frame
    $Slot.Letter.Visibility = 'Collapsed'
    $Slot.Badge.Background = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(238, 241, 248))
    $Slot.Badge.CornerRadius = New-Object System.Windows.CornerRadius(9)
}

function Complete-Domain {
    param([string]$Dom, $Frame, [byte[]]$Bytes)
    $script:DomFrame[$Dom] = $Frame
    if ($script:DomWait.ContainsKey($Dom)) { foreach ($s in $script:DomWait[$Dom]) { Apply-AppIcon $s $Frame } }
    try { [System.IO.File]::WriteAllBytes((Join-Path $IconDir (($Dom -replace '[^\w\.-]', '_') + '.img')), $Bytes) } catch { }
}

function Register-AppIcon {
    param($Slot)
    $dom = $Slot.Dom
    if ($script:DomFrame.ContainsKey($dom)) { Apply-AppIcon $Slot $script:DomFrame[$dom]; return }
    if ($script:DomWait.ContainsKey($dom)) { [void]$script:DomWait[$dom].Add($Slot); return }
    $wait = New-Object System.Collections.ArrayList
    [void]$wait.Add($Slot)
    $script:DomWait[$dom] = $wait
    $cache = Join-Path $IconDir (($dom -replace '[^\w\.-]', '_') + '.img')
    if (Test-Path $cache) {
        # decode happens on the icon timer (after the window is up), not on the startup thread
        [void]$script:IconQueue.Enqueue(@{ Dom = $dom; Index = -1; Cache = $cache; Best = $null; BestW = 0; BestBytes = $null })
        return
    }
    [void]$script:IconQueue.Enqueue(@{ Dom = $dom; Index = 0; Best = $null; BestW = 0; BestBytes = $null })
}

function Step-IconLoader {
    # cache-first records (Index -1): read + decode on the timer, a few per tick
    $budget = 10
    while ($budget -gt 0 -and $script:IconQueue.Count -gt 0 -and $script:IconQueue.Peek().Index -eq -1) {
        $rec = $script:IconQueue.Dequeue()
        $budget--
        try {
            $bytes = [System.IO.File]::ReadAllBytes([string]$rec.Cache)
            $r = Read-IconBytes $bytes
            if ($r) {
                $script:DomFrame[$rec.Dom] = $r.Frame
                if ($script:DomWait.ContainsKey($rec.Dom)) { foreach ($s in $script:DomWait[$rec.Dom]) { Apply-AppIcon $s $r.Frame } }
            }
        } catch { }
    }
    for ($i = $script:IconJobs.Count - 1; $i -ge 0; $i--) {
        $j = $script:IconJobs[$i]
        $done = $j.Task.IsCompleted
        $timeout = (([DateTime]::UtcNow - $j.Started).TotalSeconds -gt 12)
        if (-not $done -and -not $timeout) { continue }
        $script:IconJobs.RemoveAt($i)
        $bytes = $null
        if ($done -and -not $j.Task.IsFaulted -and -not $j.Task.IsCanceled) { $bytes = $j.Task.Result }
        elseif (-not $done) { try { $j.Wc.CancelAsync() } catch { } }
        try { $j.Wc.Dispose() } catch { }
        $rec = $j.Rec
        $accepted = $false
        if ($bytes -and $bytes.Length -gt 80) {
            $r = Read-IconBytes $bytes
            if ($r) {
                if ($r.W -ge 24) { Complete-Domain $rec.Dom $r.Frame $bytes; $accepted = $true }
                elseif ($r.W -gt $rec.BestW) { $rec.Best = $r.Frame; $rec.BestW = $r.W; $rec.BestBytes = $bytes }
            }
        }
        if (-not $accepted) {
            $rec.Index++
            if ($rec.Index -lt $IconUrls.Count) { [void]$script:IconQueue.Enqueue($rec) }
            elseif ($rec.Best) { Complete-Domain $rec.Dom $rec.Best $rec.BestBytes }
        }
    }
    while ($script:IconJobs.Count -lt 4 -and $script:IconQueue.Count -gt 0) {
        $rec = $script:IconQueue.Dequeue()
        $url = [string]::Format($IconUrls[$rec.Index], $rec.Dom)
        try {
            $wc = New-Object System.Net.WebClient
            $wc.Headers['User-Agent'] = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) RockToolbox'
            $task = $wc.DownloadDataTaskAsync([Uri]$url)
            [void]$script:IconJobs.Add(@{ Rec = $rec; Wc = $wc; Task = $task; Started = [DateTime]::UtcNow })
        } catch {
            $rec.Index++
            if ($rec.Index -lt $IconUrls.Count) { [void]$script:IconQueue.Enqueue($rec) }
        }
    }
}
$script:IconTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:IconTimer.Interval = [TimeSpan]::FromMilliseconds(120)
$script:IconTimer.Add_Tick({ try { Step-IconLoader } catch { } })

# shared per-card state: one frozen brush per badge color, one localized 'installed' label,
# and one set of event-handler delegates reused by all 400+ cards (attaching a fresh
# scriptblock to every card costs ~3 ms each — this is the single biggest card-loop cost)
$script:BadgeBrushes = @{}
$script:TxtInstalled = L 'installed'
$script:HCardEnter = [System.Windows.Input.MouseEventHandler]{ param($s, $e) Animate-Card $s 1.035 -2 130 }
$script:HCardLeave = [System.Windows.Input.MouseEventHandler]{ param($s, $e) Animate-Card $s 1.0 0 170 }
$script:HCardDown  = [System.Windows.Input.MouseButtonEventHandler]{ param($s, $e) Animate-Card $s 0.96 0 70 }
$script:HCardUp    = [System.Windows.Input.MouseButtonEventHandler]{ param($s, $e) Toggle-AppCard $s; Animate-Card $s 1.035 -2 300 'back' }

function New-AppCard {
    param($App)
    $sum = 0
    foreach ($ch in $App.Name.ToCharArray()) { $sum += [int]$ch }

    $letter = [System.Windows.Controls.TextBlock]::new()
    $letter.Text = $App.Name.Substring(0, 1).ToUpper()
    Set-Res $letter 'Foreground' '#FFFFFF'; $letter.FontWeight = 'Bold'; $letter.FontSize = 16
    $letter.HorizontalAlignment = 'Center'; $letter.VerticalAlignment = 'Center'

    $img = [System.Windows.Controls.Image]::new()
    $img.Width = 28; $img.Height = 28
    $img.HorizontalAlignment = 'Center'; $img.VerticalAlignment = 'Center'
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($img, [System.Windows.Media.BitmapScalingMode]::HighQuality)

    $grid = [System.Windows.Controls.Grid]::new()
    [void]$grid.Children.Add($letter); [void]$grid.Children.Add($img)

    $badge = [System.Windows.Controls.Border]::new()
    $badge.Width = 38; $badge.Height = 38
    $badge.CornerRadius = [System.Windows.CornerRadius]::new(19)
    $hexB = [string]$palette[$sum % $palette.Count]
    $brushB = $script:BadgeBrushes[$hexB]
    if (-not $brushB) {
        $brushB = Get-Brush $hexB
        try { $brushB.Freeze() } catch { }
        $script:BadgeBrushes[$hexB] = $brushB
    }
    $badge.Background = $brushB
    $badge.Margin = '8,0,10,0'; $badge.VerticalAlignment = 'Center'
    $badge.Child = $grid

    if ($App.Dom) { Register-AppIcon @{ Img = $img; Letter = $letter; Badge = $badge; Dom = $App.Dom } }

    $name = [System.Windows.Controls.TextBlock]::new()
    $name.Text = $App.Name; $name.FontWeight = 'SemiBold'; Set-Res $name 'Foreground' '#F2F4FF'
    $name.TextTrimming = 'CharacterEllipsis'; $name.MaxWidth = 185

    $inst = [System.Windows.Controls.TextBlock]::new()
    $inst.Text = $script:TxtInstalled; $inst.FontSize = 10; Set-Res $inst 'Foreground' '#3DDC84'; $inst.Visibility = 'Collapsed'

    $stack = [System.Windows.Controls.StackPanel]::new()
    $stack.VerticalAlignment = 'Center'
    [void]$stack.Children.Add($name); [void]$stack.Children.Add($inst)

    $row = [System.Windows.Controls.StackPanel]::new()
    $row.Orientation = 'Horizontal'
    [void]$row.Children.Add($badge); [void]$row.Children.Add($stack)

    $card = [System.Windows.Controls.Border]::new()
    $card.Width = 262; $card.Height = 56; $card.Margin = '4'
    $card.CornerRadius = [System.Windows.CornerRadius]::new($(if (Test-Modern) { 12 } else { 4 }))
    $card.BorderThickness = [System.Windows.Thickness]::new(1)
    $card.Cursor = [System.Windows.Input.Cursors]::Hand
    $card.ToolTip = $App.Id
    $card.Child = $row
    $App['Badge'] = $inst
    $card.Tag = $App
    Set-CardLook $card $false
    Initialize-CardAnim $card
    $card.add_MouseEnter($script:HCardEnter)
    $card.add_MouseLeave($script:HCardLeave)
    $card.add_MouseLeftButtonDown($script:HCardDown)
    $card.add_MouseLeftButtonUp($script:HCardUp)
    return $card
}

# Cards are built AFTER the window is up, two categories per dispatcher pass at
# Background priority, so the interface shows in ~2 s and this page fills in while
# the rest of the app is already clickable. A note stands in until the first batch.
$script:AppsBuilt = $false

function New-AppSectionSkeleton($g) {
    $wrap = New-Object System.Windows.Controls.WrapPanel
    $hdr = New-Object System.Windows.Controls.TextBlock
    $hdr.Style = $window.FindResource('H')
    $hdr.Text = '- ' + $g.Name
    $hdr.Cursor = [System.Windows.Input.Cursors]::Hand
    $sec = @{ Cat = $g.Name; Wrap = $wrap; Header = $hdr; Panel = $null; Cards = (New-Object System.Collections.ArrayList) }
    $hdr.Tag = $sec
    $hdr.Add_MouseLeftButtonUp({
        param($s, $e)
        $sc = $s.Tag
        if ($sc.Wrap.Visibility -eq 'Visible') { $sc.Wrap.Visibility = 'Collapsed'; $s.Text = '+ ' + $sc.Cat }
        else { $sc.Wrap.Visibility = 'Visible'; $s.Text = '- ' + $sc.Cat; Start-PageAnim $sc.Wrap 10 }
    })
    $panel = New-Object System.Windows.Controls.StackPanel
    [void]$panel.Children.Add($hdr); [void]$panel.Children.Add($wrap)
    $sec.Panel = $panel
    [void]$appsPanel.Children.Add($panel)
    [void]$script:Sections.Add($sec)
    return $sec
}
function Add-AppSectionCard($sec, $a, [string]$CatName) {
    $card = New-AppCard $a
    [void]$sec.Wrap.Children.Add($card)
    $item = @{ Card = $card; App = $a; Cat = $CatName; Sec = $sec }
    [void]$sec.Cards.Add($item)
    [void]$script:AppCards.Add($item)
}
function Build-AppSection($g) {
    $sec = New-AppSectionSkeleton $g
    foreach ($a in $g.Group) { Add-AppSectionCard $sec $a $g.Name }
}

function Start-AppBuild {
    if ($script:AppsBuilt) { return }
    $script:AppBuildState = @{ Groups = @($Apps | Group-Object { $_.Cat }); GI = 0; CI = 0; Cur = $null }
    $note = [System.Windows.Controls.TextBlock]::new()
    $note.Text = L 'Loading apps…'
    Set-Res $note 'Foreground' '#9AA0B8'
    $note.FontSize = 13; $note.Margin = '4,4,0,10'
    [void]$appsPanel.Children.Add($note)
    $script:AppBuildNote = $note
    $script:AppBuildTimer = New-Object System.Windows.Threading.DispatcherTimer('Background', [System.Windows.Threading.Dispatcher]::CurrentDispatcher)
    $script:AppBuildTimer.Interval = [TimeSpan]::FromMilliseconds(4)
    # script-scope state only: a closure here would copy the variables, and the built flag
    # would never reach the rest of the app (GetNewClosure copies script vars into its scope)
    # every pass builds cards for at most ~7 ms and then gives the UI thread back, so scrolling and animations never stutter
    $script:AppBuildTimer.Add_Tick({
        try {
            $st = $script:AppBuildState
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            while ($st.GI -lt $st.Groups.Count -and $sw.ElapsedMilliseconds -lt 7) {
                $g = $st.Groups[$st.GI]
                $grp = @($g.Group)
                if (-not $st.Cur) { $st.Cur = New-AppSectionSkeleton $g; $st.CI = 0 }
                if ($st.CI -lt $grp.Count) { Add-AppSectionCard $st.Cur $grp[$st.CI] ([string]$g.Name); $st.CI++ }
                if ($st.CI -ge $grp.Count) { $st.Cur = $null; $st.GI++ }
            }
            if ($st.GI -ge $st.Groups.Count) {
                $script:AppBuildTimer.Stop()
                [void]$appsPanel.Children.Remove($script:AppBuildNote)
                $script:AppBuildState = $null
                $script:AppBuildNote = $null
                $script:AppsBuilt = $true
                Apply-AppFilter
            }
        } catch { }
    })
    $script:AppBuildTimer.Start()
}

function Apply-AppFilter {
    $q = $txtAppSearch.Text.Trim()
    $shown = 0
    foreach ($sec in $script:Sections) {
        $any = $false
        foreach ($it in $sec.Cards) {
            $okCat = ($script:CatFilter -eq 'All') -or ($it.Cat -eq $script:CatFilter)
            $okQ   = (-not $q) -or ($it.App.Name -like "*$q*") -or ($it.App.Id -like "*$q*")
            if ($okCat -and $okQ) {
                $wasHidden = ($it.Card.Visibility -ne 'Visible')
                $it.Card.Visibility = 'Visible'; $any = $true
                if ($wasHidden -and $script:CardAnimOn) { Start-CardFade $it.Card ([Math]::Min($shown, 24) * 14); $shown++ }
            } else { $it.Card.Visibility = 'Collapsed' }
        }
        $sec.Panel.Visibility = if ($any) { 'Visible' } else { 'Collapsed' }
    }
}
function Set-ChipLook {
    foreach ($c in $script:Chips) {
        if ([string]$c.Tag -eq $script:CatFilter) { Set-Res $c 'Background' '#4A78B0'; Set-Res $c 'Foreground' '#FFFFFF' } else { Set-Res $c 'Background' '#1E3547'; Set-Res $c 'Foreground' '#F0F4FF' }
    }
}
foreach ($cat in (@('All') + @($Apps | ForEach-Object { $_.Cat } | Select-Object -Unique))) {
    $chip = New-Object System.Windows.Controls.Button
    $chip.Content = $cat; $chip.Tag = $cat; $chip.Padding = '12,4'; $chip.Margin = '0,0,6,6'
    $chip.Add_Click({
        param($s, $e)
        $script:CatFilter = [string]$s.Tag
        Set-ChipLook
        Apply-AppFilter
    })
    [void]$chipPanel.Children.Add($chip)
    [void]$script:Chips.Add($chip)
}
Set-ChipLook
function Register-Debounce {
    param($Box, [scriptblock]$Action, [int]$Ms = 160)
    $t = New-Object System.Windows.Threading.DispatcherTimer
    $t.Interval = [TimeSpan]::FromMilliseconds($Ms)
    $t.Tag = $Action
    $t.Add_Tick({ param($sender, $e) $sender.Stop(); try { & $sender.Tag } catch { } })
    $Box.Add_TextChanged(({ $t.Stop(); $t.Start() }).GetNewClosure())
}
Register-Debounce $txtAppSearch { Apply-AppFilter }

$btnInstall.Add_Click({ Show-InstallFind $false; Start-Work {
    $ids = @($script:Selected.Keys)
    if ($ids.Count -eq 0) { Log 'No apps selected.' 'WARN' 'Winget'; return }
    Invoke-Pkg 'install' $ids
    if ($script:WorkFailed -gt 0) { Log ("Install queue finished: $($script:WorkFailed) of $($ids.Count) app(s) were NOT installed - see the errors above.") 'WARN' 'Winget' }
    else { Log 'Install queue finished.' 'INFO' 'Winget' }
} })
$btnUninstall.Add_Click({ Start-Work {
    $ids = @($script:Selected.Keys)
    if ($ids.Count -eq 0) { Log 'No apps selected.' 'WARN' 'Winget'; return }
    Invoke-Pkg 'uninstall' $ids
    Log 'Uninstall queue finished.' 'INFO' 'Winget'
} })
$btnUpgrade.Add_Click({ Start-Work {
    if (Test-PkgChoco) {
        $ch = Ensure-Choco
        if (-not $ch) { Log 'Chocolatey (choco) is not available and could not be installed automatically. Install it from chocolatey.org, or switch back to WinGet.' 'WARN' 'Pkg'; return }
        Log 'Checking which packages are outdated (choco outdated)...' 'INFO' 'Pkg'
        $tmpO = Join-Path $env:TEMP 'rock_choco_outdated.txt'
        Remove-Item -LiteralPath $tmpO -Force -ErrorAction SilentlyContinue
        [void](Invoke-External $ch 'outdated -r' $tmpO)
        $cids = @()
        if (Test-Path -LiteralPath $tmpO) {
            foreach ($ln in @(Get-Content -LiteralPath $tmpO -ErrorAction SilentlyContinue)) {
                if ([string]$ln -match '^([A-Za-z0-9\.\-_\+]+)\|[^|]*\|[^|]*\|') { $cids += $Matches[1] }
            }
        }
        if ($cids.Count -eq 0) { Log 'Every Chocolatey package is up to date.' 'INFO' 'Pkg'; return }
        Log ("Upgrading $($cids.Count) package(s)...") 'INFO' 'Pkg'
        Invoke-Choco 'upgrade' $cids -Native
        $script:upgLoaded = $false
        Log 'Upgrade finished.' 'INFO' 'Pkg'
        return
    }
    $wg = Ensure-Winget
    if (-not $wg) { Log 'winget is not available and could not be installed automatically.' 'WARN' 'Winget'; return }
    Log 'Checking which apps have updates...' 'INFO' 'Winget'
    $tmpU = Join-Path $env:TEMP 'rock_winget_upgrade_all.txt'
    [void](Invoke-WingetTable 'upgrade --accept-source-agreements' $tmpU)
    $found = @(Read-WingetUpgrades $tmpU | ForEach-Object { $_.Id })
    if ($found.Count -gt 0) {
        Log ("Upgrading $($found.Count) app(s)...") 'INFO' 'Winget'
        Invoke-Winget 'upgrade' $found
        $script:upgLoaded = $false
        Log 'Upgrade finished.' 'INFO' 'Winget'
    } else {
        # the list could not be read: fall back to winget's own "upgrade --all" (no per-app progress)
        Log 'Upgrading all packages...' 'INFO' 'Winget'
        $code = Invoke-PkgWindow 'winget upgrade --all' $wg 'upgrade --all --silent --accept-source-agreements --accept-package-agreements'
        Log "Upgrade finished (exit code $code)." 'INFO' 'Winget'
    }
} })
$btnStopAction.Add_Click({
    if (-not $script:Busy) {
        Log 'No action is running right now.' 'WARN' 'UI'
        return
    }
    $script:CancelRequested = $true
    Log 'Stop requested — cancelling current action...' 'WARN' 'UI'
})
# "More options" fold-out and mouse-wheel scrolling of the one-row category strip (new look only)
if ($lnkMore) {
    $lnkMore.Text = (L 'More options') + ' ' + [string][char]0x25BE
    $lnkMore.Add_MouseLeftButtonUp({
        if ($spMore.Visibility -eq 'Visible') { $spMore.Visibility = 'Collapsed'; $lnkMore.Text = (L 'More options') + ' ' + [string][char]0x25BE }
        else { $spMore.Visibility = 'Visible'; $lnkMore.Text = (L 'More options') + ' ' + [string][char]0x25B4; Start-PageAnim $spMore 8 }
    })
}
if ($svChips) { $svChips.Add_PreviewMouseWheel({ param($s, $e) $s.ScrollToHorizontalOffset($s.HorizontalOffset - $e.Delta); $e.Handled = $true }) }
Update-CartPanel
$btnAppsClear.Add_Click({
    Show-InstallFind $false
    $script:Selected.Clear()
    foreach ($it in $script:AppCards) { Set-CardLook $it.Card $false }
    Update-SelCount
})
$btnCollapse.Add_Click({
    Show-InstallFind $false
    foreach ($sec in $script:Sections) { $sec.Wrap.Visibility = 'Collapsed'; $sec.Header.Text = '+ ' + $sec.Cat }
})
$btnExpand.Add_Click({
    Show-InstallFind $false
    foreach ($sec in $script:Sections) { $sec.Wrap.Visibility = 'Visible'; $sec.Header.Text = '- ' + $sec.Cat }
})
$btnInstallCustom.Add_Click({ Start-Work {
    $id = $txtCustomId.Text.Trim()
    if (-not $id) {
        if (Test-PkgChoco) { Log 'Type a Chocolatey package ID first (example: steam).' 'WARN' 'Pkg' }
        else               { Log 'Type a winget ID first (example: Valve.Steam).' 'WARN' 'Winget' }
        return
    }
    Invoke-Pkg 'install' @($id)
} })
$btnShowInstalled.Add_Click({ Show-InstallFind $false; Start-Work {
    if (-not $script:AppsBuilt) { Log 'The app list is still loading — try again in a few seconds.' 'WARN' 'Install'; return }
    if (Test-PkgChoco) {
        $ch = Ensure-Choco
        if (-not $ch) { Log 'Chocolatey (choco) is not available and could not be installed automatically. Install it from chocolatey.org, or switch back to WinGet.' 'WARN' 'Pkg'; return }
        Log 'Scanning installed apps (choco list)...' 'INFO' 'Pkg'
        $lines = @()
        try { $lines = @(& $ch 'list' '--limit-output' 2>$null) } catch { }
        $installed = @{}
        foreach ($ln in $lines) {
            $s = [string]$ln
            if ($s -match '^([^|]+)\|') { $installed[$s.Split('|')[0].Trim().ToLower()] = $true }
        }
        $n = 0
        foreach ($it in $script:AppCards) {
            $cid = ConvertTo-ChocoId $it.App.Id
            $hit = $installed.ContainsKey($cid)
            if ($hit) { $it.App.Badge.Visibility = 'Visible'; $n++ } else { $it.App.Badge.Visibility = 'Collapsed' }
        }
        Log "$n of the listed apps are already installed (marked in green)." 'INFO' 'Pkg'
        return
    }
    $wg = Ensure-Winget
    if (-not $wg) { Log 'winget is not available and could not be installed automatically.' 'WARN' 'Winget'; return }
    Log 'Scanning installed apps (winget list)...' 'INFO' 'Winget'
    $tmp = Join-Path $env:TEMP 'rock_winget_list.txt'
    [void](Invoke-WingetTable 'list --accept-source-agreements' $tmp)
    $txt = ''
    if (Test-Path $tmp) { $txt = Get-Content $tmp -Raw -ErrorAction SilentlyContinue }
    $n = 0
    $regNames = Get-InstalledDisplayNames
    foreach ($it in $script:AppCards) {
        $hit = $false
        if ($txt) { $hit = [bool]($txt -match ('(?<![\w.-])' + [regex]::Escape($it.App.Id) + '(?![\w.-])')) }
        # winget does not always know the ID of an app installed by hand (e.g. Chrome) - also match its name in Windows' installed list
        if (-not $hit) { $hit = Test-NameInList $regNames @($it.App.Name) }
        if ($hit) { $it.App.Badge.Visibility = 'Visible'; $n++ } else { $it.App.Badge.Visibility = 'Collapsed' }
    }
    Log "$n of the listed apps are already installed (marked in green)." 'INFO' 'Winget'
} })

# ---- Package Manager switch (WinGet / Chocolatey)
function Update-PkgUi {
    if (Test-PkgChoco) { $lblCustomHdr.Text = L 'Custom Chocolatey ID' } else { $lblCustomHdr.Text = L 'Custom winget ID' }
}
$rdPkgWinget.IsChecked = (-not (Test-PkgChoco))
$rdPkgChoco.IsChecked  = [bool](Test-PkgChoco)
$rdPkgWinget.Add_Checked({ $script:Settings['PkgManager'] = 'WinGet'; Save-Settings; Update-PkgUi; Log 'Package manager set to WinGet.' 'INFO' 'Pkg'; if (-not (Get-Winget)) { Start-Work { [void](Ensure-Winget) } 'Installing winget (App Installer)...' } })
$rdPkgChoco.Add_Checked({ $script:Settings['PkgManager'] = 'Chocolatey'; Save-Settings; Update-PkgUi; Log 'Package manager set to Chocolatey (choco).' 'INFO' 'Pkg'; if (-not (Get-Choco)) { Start-Work { [void](Ensure-Choco) } 'Installing Chocolatey...' } })
Update-PkgUi

# ---- Instant apply: tell Windows/Explorer that settings changed - Explorer is NEVER restarted
# Explorer re-reads taskbar / theme / policy / shell settings when it gets these notifications,
# so toggles take effect right after you press Yes. (The manual "Restart Explorer" button is still on the page.)
function Send-RockSettingChange {
    if (-not ('RockToolbox.ShellNotify' -as [type])) {
        try {
            Add-Type -Namespace RockToolbox -Name ShellNotify -MemberDefinition @'
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern System.IntPtr SendMessageTimeout(System.IntPtr hWnd, uint Msg, System.UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out System.UIntPtr lpdwResult);
[DllImport("user32.dll", CharSet = CharSet.Auto)]
public static extern System.IntPtr FindWindow(string lpClassName, string lpWindowName);
[DllImport("user32.dll", CharSet = CharSet.Auto)]
public static extern System.IntPtr FindWindowEx(System.IntPtr hwndParent, System.IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
[DllImport("shell32.dll")]
public static extern void SHChangeNotify(int wEventId, uint uFlags, System.IntPtr dwItem1, System.IntPtr dwItem2);
'@
        } catch { }
    }
    if (-not ('RockToolbox.ShellNotify' -as [type])) { return }
    # runs on its own thread so the window never freezes while Windows answers
    $worker = {
        try {
            $res = [System.UIntPtr]::Zero
            $bcast = [System.IntPtr]0xFFFF
            foreach ($k in 'TraySettings', 'ImmersiveColorSet', 'WindowMetrics', 'Policy', 'Environment', 'intl') {
                [void][RockToolbox.ShellNotify]::SendMessageTimeout($bcast, 0x1A, [System.UIntPtr]::Zero, $k, 2, 1000, [ref]$res)
            }
            # also poke the taskbar windows directly
            foreach ($cls in 'Shell_TrayWnd', 'Shell_SecondaryTrayWnd', 'Progman') {
                $h = [RockToolbox.ShellNotify]::FindWindow($cls, $null)
                if ($h -ne [System.IntPtr]::Zero) {
                    foreach ($k in 'TraySettings', 'Policy') {
                        [void][RockToolbox.ShellNotify]::SendMessageTimeout($h, 0x1A, [System.UIntPtr]::Zero, $k, 2, 1000, [ref]$res)
                    }
                }
            }
            # the taskbar clock (and tray) is a child window of the taskbar: tell it to re-read its format / seconds setting
            $tray = [RockToolbox.ShellNotify]::FindWindow('Shell_TrayWnd', $null)
            if ($tray -ne [System.IntPtr]::Zero) {
                $notify = [RockToolbox.ShellNotify]::FindWindowEx($tray, [System.IntPtr]::Zero, 'TrayNotifyWnd', $null)
                $targets = @($notify)
                if ($notify -ne [System.IntPtr]::Zero) { $targets += [RockToolbox.ShellNotify]::FindWindowEx($notify, [System.IntPtr]::Zero, 'TrayClockWClass', $null) }
                foreach ($h in $targets) {
                    if ($h -eq [System.IntPtr]::Zero) { continue }
                    foreach ($k in 'intl', 'TraySettings', 'ShellState') {
                        [void][RockToolbox.ShellNotify]::SendMessageTimeout($h, 0x1A, [System.UIntPtr]::Zero, $k, 2, 1000, [ref]$res)
                    }
                    [void][RockToolbox.ShellNotify]::SendMessageTimeout($h, 0x1E, [System.UIntPtr]::Zero, $null, 2, 1000, [ref]$res)
                }
            }
            # SHCNE_ASSOCCHANGED: refresh shell icons / views
            [RockToolbox.ShellNotify]::SHChangeNotify(0x08000000, 0, [System.IntPtr]::Zero, [System.IntPtr]::Zero)
        } catch { }
    }
    # one refresh at a time; the previous runspace is disposed (they used to pile up, one per tweak)
    try {
        if ($script:ShellRefreshAsync -and -not $script:ShellRefreshAsync.IsCompleted) { return }
        if ($script:ShellRefreshPs) { try { $script:ShellRefreshPs.Dispose() } catch { } }
        $ps = [powershell]::Create()
        [void]$ps.AddScript($worker.ToString())
        $script:ShellRefreshPs = $ps
        $script:ShellRefreshAsync = $ps.BeginInvoke()
    } catch { }
}

function Request-ShellRefresh {
    try { Send-RockSettingChange } catch { }
}

# Themed Yes/No confirmation dialog (dark, accent-styled) shown before registry changes.
# ---- dark custom title bar for the small dialog windows (confirm / add download)
function Add-RockDialogChrome {
    param([string]$Text, [string]$Title)
    $Text = $Text.Replace('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"', 'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"')
    $Text = $Text.Replace('Background="#1F2329" FontFamily', 'Background="#1F2329" WindowStyle="None" BorderBrush="#2B3038" BorderThickness="1" FontFamily')
    $style = @'
    <Style x:Key="TitleClose" TargetType="Button">
      <Setter Property="Width" Value="44"/><Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="#C9CDE0"/>
      <Setter Property="Focusable" Value="False"/><Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#E5484D"/><Setter Property="Foreground" Value="#FFFFFF"/></Trigger>
        <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="#C23B40"/></Trigger>
      </Style.Triggers>
    </Style>
  </Window.Resources>
  <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight="36" ResizeBorderThickness="0" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/></shell:WindowChrome.WindowChrome>
'@
    $bar = @'
<DockPanel>
    <Border DockPanel.Dock="Top" Height="36" Background="#181B20" BorderBrush="#2B3038" BorderThickness="0,0,0,1">
      <DockPanel>
        <Button x:Name="btnTitleClose" DockPanel.Dock="Right" Style="{StaticResource TitleClose}" shell:WindowChrome.IsHitTestVisibleInChrome="True">
          <Path Data="M0,0 L10,10 M10,0 L0,10" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.3" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Width="10" Height="10"/>
        </Button>
        <TextBlock Text="__TITLE__" Foreground="#C9CDE0" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center" Margin="14,0,0,0"/>
      </DockPanel>
    </Border>
'@.Replace('__TITLE__', $Title)
    $endRes = '</Window.Resources>'
    $i = $Text.IndexOf($endRes)
    $j = $Text.LastIndexOf('</Window>')
    if ($i -lt 0 -or $j -lt 0) { return $Text }
    $head = $Text.Substring(0, $i)
    $body = $Text.Substring($i + $endRes.Length, $j - $i - $endRes.Length)
    return ($head + $style + $bar + $body + '</DockPanel>' + "`r`n" + '</Window>')
}
# lets a dialog window use the main window's implicit styles + theme brushes (scroll bars, check boxes, text boxes, combos...)
function Merge-RockResources {
    param($Win)
    try { if ($window -and $window.Resources) { $Win.Resources.MergedDictionaries.Add($window.Resources) } } catch { }
}
function Initialize-RockDialogClose {
    param($Win)
    Merge-RockResources $Win
    try {
        # handlers use the sender (no closures), so the script's own functions stay reachable
        $Win.FindName('btnTitleClose').Add_Click({ param($sender, $e) try { [System.Windows.Window]::GetWindow($sender).Close() } catch { } })
        $Win.Add_Loaded({ param($sender, $e) Start-DialogAnim $sender.Content })
    } catch { }
}

# ---- themed message box (replaces the white Windows MessageBox everywhere)
$script:DiaStylesXaml = @'
    <Style x:Key="DiaBtn" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#2F6FB0" CornerRadius="6" Padding="28,8">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#FFFFFF"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="0.82" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="DiaBtn2" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#23272D" BorderBrush="#35506A" BorderThickness="1" CornerRadius="6" Padding="28,8">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#E6E8F2"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="0.82" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
'@

function Get-MsgIconXaml {
    param([string]$Kind)
    $ns = 'xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"'
    switch -Regex ($Kind) {
        'Warn' {
            return ('<Grid ' + $ns + ' Width="46" Height="46"><Polygon Points="23,3 44,41 2,41" Fill="#FFB454" Stroke="#FFB454" StrokeThickness="3" StrokeLineJoin="Round"/>' +
                    '<TextBlock Text="!" FontWeight="Bold" FontSize="20" Foreground="#1F2329" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,6"/></Grid>')
        }
        'Error|Stop|Hand' {
            return ('<Grid ' + $ns + ' Width="46" Height="46"><Ellipse Fill="#FF5555"/>' +
                    '<Path Data="M15,15 L31,31 M31,15 L15,31" Stroke="#FFFFFF" StrokeThickness="3.2" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/></Grid>')
        }
        'Question' {
            return ('<Grid ' + $ns + ' Width="46" Height="46"><Ellipse Fill="#4FD1FF"/>' +
                    '<TextBlock Text="?" FontWeight="Bold" FontSize="24" Foreground="#1F2329" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,-2,0,0"/></Grid>')
        }
        default {
            return ('<Grid ' + $ns + ' Width="46" Height="46"><Ellipse Fill="#4FD1FF"/>' +
                    '<TextBlock Text="i" FontWeight="Bold" FontSize="24" Foreground="#1F2329" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,-2,0,0"/></Grid>')
        }
    }
}

function Show-RockMessage {
    param([string]$Text, [string]$Caption = 'Rock Toolbox', [string]$Buttons = 'OK', [string]$Icon = 'Information')
    $xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Rock Toolbox" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" FontFamily="Segoe UI" FontSize="13" MinWidth="470" MaxWidth="680">
  <Window.Resources>
__STYLES__
  </Window.Resources>
  <Border BorderBrush="#2B3038" BorderThickness="1" Padding="24,22">
    <StackPanel>
      <DockPanel LastChildFill="True">
        <Grid x:Name="msgIcon" DockPanel.Dock="Left" Width="46" Height="46" VerticalAlignment="Top" Margin="0,0,16,0"/>
        <StackPanel>
          <TextBlock x:Name="msgTitle" FontSize="16" FontWeight="Bold" Foreground="White" TextWrapping="Wrap" MaxWidth="520"/>
          <Border x:Name="msgBodyBox" Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Margin="0,10,0,0" Padding="12,10">
            <ScrollViewer MaxHeight="300" VerticalScrollBarVisibility="Auto">
              <TextBlock x:Name="msgBody" Foreground="#C9CDE0" TextWrapping="Wrap" MaxWidth="500" LineHeight="20"/>
            </ScrollViewer>
          </Border>
        </StackPanel>
      </DockPanel>
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,20,0,0">
        <Button x:Name="msgYes" Style="{StaticResource DiaBtn}" IsDefault="True" Cursor="Hand"/>
        <Button x:Name="msgNo" Style="{StaticResource DiaBtn2}" IsCancel="True" Margin="10,0,0,0" Cursor="Hand"/>
      </StackPanel>
    </StackPanel>
  </Border>
</Window>
'@
    $xaml = $xaml.Replace('__STYLES__', $script:DiaStylesXaml)
    $xaml = Add-RockDialogChrome -Text $xaml -Title $Caption
    $xaml = Convert-XamlTheme -Text $xaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($xaml)
    Initialize-RockDialogClose $w
    # icon
    try {
        $ix = Convert-XamlTheme -Text (Get-MsgIconXaml $Icon) -Literal
        [void]$w.FindName('msgIcon').Children.Add([Windows.Markup.XamlReader]::Parse($ix))
    } catch { }
    # first line = headline, the rest = details (scrolls when it is long)
    $t = ([string]$Text).Trim()
    $nl = $t.IndexOf("`n")
    if ($nl -gt 0) { $head = $t.Substring(0, $nl).Trim(); $body = $t.Substring($nl + 1).Trim() } else { $head = $t; $body = '' }
    $w.FindName('msgTitle').Text = $head
    if ($body) { $w.FindName('msgBody').Text = $body } else { $w.FindName('msgBodyBox').Visibility = 'Collapsed' }
    $yes = $w.FindName('msgYes'); $no = $w.FindName('msgNo')
    if ($Buttons -match 'YesNo') {
        $yes.Content = L 'Yes'; $yes.Tag = 'Yes'; $no.Content = L 'No'; $no.Tag = 'No'
        $w.Tag = 'No'
    } else {
        $yes.Content = L 'OK'; $yes.Tag = 'OK'; $no.Visibility = 'Collapsed'
        $w.Tag = 'OK'
    }
    $click = { param($sender, $e) $win = [System.Windows.Window]::GetWindow($sender); $win.Tag = [string]$sender.Tag; $win.Close() }
    $yes.Add_Click($click); $no.Add_Click($click)
    if (Test-Rtl) { $w.FlowDirection = 'RightToLeft' }
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    [void]$w.ShowDialog()
    return [string]$w.Tag
}

# .NET-style entry point so existing [RockMsg]::Show(text, caption, 'YesNo', 'Warning') calls get the themed dialog
if (-not ('RockMsg' -as [type])) {
    Add-Type -TypeDefinition @'
public static class RockMsg {
    public static System.Func<string, string, string, string, string> Handler;
    public static string Show(string text, string caption, string buttons, string icon) {
        if (Handler != null) return Handler(text, caption, buttons, icon);
        return "No";
    }
    public static string Show(string text, string caption) { return Show(text, caption, "OK", "Information"); }
    public static string Show(string text) { return Show(text, "Rock Toolbox", "OK", "Information"); }
}
'@
}
[RockMsg]::Handler = [System.Func[string, string, string, string, string]]{ param($t, $c, $b, $i) Show-RockMessage -Text $t -Caption $c -Buttons $b -Icon $i }

function Show-RockConfirm {
    param([string]$Title, [string]$Desc, [string]$Question, [string]$YesText, [string]$NoText)
    $xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Rock Toolbox" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" FontFamily="Segoe UI" FontSize="13" MinWidth="430">
  <Window.Resources>
    <Style x:Key="DiaBtn" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#2F6FB0" CornerRadius="3" Padding="28,7">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#FFFFFF"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="0.82" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="DiaBtn2" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="#23272D" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="3" Padding="28,7">
              <ContentPresenter HorizontalAlignment="Center" TextElement.Foreground="#E6E8F2"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="0.82" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="bd" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.2"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <Border BorderBrush="#2B3038" BorderThickness="1" Padding="22,20">
    <StackPanel MaxWidth="420">
      <StackPanel Orientation="Horizontal">
        <Grid Width="40" Height="38" VerticalAlignment="Top" Margin="0,0,14,0">
          <Polygon Points="20,2 38,36 2,36" Fill="#FFB454"/>
          <TextBlock Text="!" FontWeight="Bold" Foreground="#1F2329" FontSize="15" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,6"/>
        </Grid>
        <StackPanel>
          <TextBlock x:Name="diaTitle" FontSize="15" FontWeight="Bold" Foreground="White" TextWrapping="Wrap"/>
          <TextBlock x:Name="diaDesc" Foreground="#9AA0B8" TextWrapping="Wrap" Margin="0,4,0,0"/>
          <TextBlock x:Name="diaQ" Foreground="#E6E8F2" TextWrapping="Wrap" Margin="0,10,0,0" FontWeight="SemiBold"/>
        </StackPanel>
      </StackPanel>
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,18,0,0">
        <Button x:Name="diaYes" Style="{StaticResource DiaBtn}" IsDefault="True" Cursor="Hand"/>
        <Button x:Name="diaNo" Style="{StaticResource DiaBtn2}" IsCancel="True" Margin="10,0,0,0" Cursor="Hand"/>
      </StackPanel>
    </StackPanel>
  </Border>
</Window>
'@
    $xaml = Add-RockDialogChrome -Text $xaml -Title 'Rock Toolbox'
    $xaml = Convert-XamlTheme -Text $xaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($xaml)
    Initialize-RockDialogClose $w
    $w.FindName('diaTitle').Text = $Title
    $d = $w.FindName('diaDesc')
    if ($Desc) { $d.Text = $Desc } else { $d.Visibility = 'Collapsed' }
    $w.FindName('diaQ').Text = $Question
    $w.FindName('diaYes').Content = $YesText
    $w.FindName('diaNo').Content = $NoText
    if (Test-Rtl) { $w.FlowDirection = 'RightToLeft' }
    $script:DiaWin = $w
    $script:DiaResult = $false
    $w.FindName('diaYes').Add_Click({ $script:DiaResult = $true; $script:DiaWin.Close() })
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    [void]$w.ShowDialog()
    $script:DiaWin = $null
    return $script:DiaResult
}


