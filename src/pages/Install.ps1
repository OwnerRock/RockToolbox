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
        Set-Res $Card 'Background' '#1E3A4C'; Set-Res $Card 'BorderBrush' '#4FD1FF'
        try {
            $fx = New-Object System.Windows.Media.Effects.DropShadowEffect
            $fx.Color = [System.Windows.Media.ColorConverter]::ConvertFromString([string]$script:Pal['4FD1FF'])
            $fx.BlurRadius = 14; $fx.ShadowDepth = 0; $fx.Opacity = 0.55
            $Card.Effect = $fx
        } catch { }
    } else {
        Set-Res $Card 'Background' '#23272D'; Set-Res $Card 'BorderBrush' '#343A44'
        $Card.Effect = $null
    }
}
function Update-SelCount { $txtSelCount.Text = "$(L 'Selected Apps'): $($script:Selected.Count)" }
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
    $tg = New-Object System.Windows.Media.TransformGroup
    [void]$tg.Children.Add((New-Object System.Windows.Media.ScaleTransform(1.0, 1.0)))
    [void]$tg.Children.Add((New-Object System.Windows.Media.TranslateTransform(0.0, 0.0)))
    $Card.RenderTransform = $tg
    $Card.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
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
        try {
            $bytes = [System.IO.File]::ReadAllBytes($cache)
            $r = Read-IconBytes $bytes
            if ($r) { $script:DomFrame[$dom] = $r.Frame; Apply-AppIcon $Slot $r.Frame; return }
        } catch { }
    }
    [void]$script:IconQueue.Enqueue(@{ Dom = $dom; Index = 0; Best = $null; BestW = 0; BestBytes = $null })
}

function Step-IconLoader {
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

function New-AppCard {
    param($App)
    $sum = 0
    foreach ($ch in $App.Name.ToCharArray()) { $sum += [int]$ch }

    $letter = New-Object System.Windows.Controls.TextBlock
    $letter.Text = $App.Name.Substring(0, 1).ToUpper()
    Set-Res $letter 'Foreground' '#FFFFFF'; $letter.FontWeight = 'Bold'; $letter.FontSize = 16
    $letter.HorizontalAlignment = 'Center'; $letter.VerticalAlignment = 'Center'

    $img = New-Object System.Windows.Controls.Image
    $img.Width = 28; $img.Height = 28
    $img.HorizontalAlignment = 'Center'; $img.VerticalAlignment = 'Center'
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($img, [System.Windows.Media.BitmapScalingMode]::HighQuality)

    $grid = New-Object System.Windows.Controls.Grid
    [void]$grid.Children.Add($letter); [void]$grid.Children.Add($img)

    $badge = New-Object System.Windows.Controls.Border
    $badge.Width = 38; $badge.Height = 38
    $badge.CornerRadius = New-Object System.Windows.CornerRadius(19)
    $badge.Background = Get-Brush $palette[$sum % $palette.Count]
    $badge.Margin = '8,0,10,0'; $badge.VerticalAlignment = 'Center'
    $badge.Child = $grid

    if ($App.Dom) { Register-AppIcon @{ Img = $img; Letter = $letter; Badge = $badge; Dom = $App.Dom } }

    $name = New-Object System.Windows.Controls.TextBlock
    $name.Text = $App.Name; $name.FontWeight = 'SemiBold'; Set-Res $name 'Foreground' '#F2F4FF'
    $name.TextTrimming = 'CharacterEllipsis'; $name.MaxWidth = 185

    $inst = New-Object System.Windows.Controls.TextBlock
    $inst.Text = L 'installed'; $inst.FontSize = 10; Set-Res $inst 'Foreground' '#3DDC84'; $inst.Visibility = 'Collapsed'

    $stack = New-Object System.Windows.Controls.StackPanel
    $stack.VerticalAlignment = 'Center'
    [void]$stack.Children.Add($name); [void]$stack.Children.Add($inst)

    $row = New-Object System.Windows.Controls.StackPanel
    $row.Orientation = 'Horizontal'
    [void]$row.Children.Add($badge); [void]$row.Children.Add($stack)

    $card = New-Object System.Windows.Controls.Border
    $card.Width = 262; $card.Height = 56; $card.Margin = '4'
    $card.CornerRadius = New-Object System.Windows.CornerRadius(4)
    $card.BorderThickness = New-Object System.Windows.Thickness(1)
    $card.Cursor = [System.Windows.Input.Cursors]::Hand
    $card.ToolTip = $App.Id
    $card.Child = $row
    $App['Badge'] = $inst
    $card.Tag = $App
    Set-CardLook $card $false
    Initialize-CardAnim $card
    $card.Add_MouseEnter({ param($s, $e) Animate-Card $s 1.035 -2 130 })
    $card.Add_MouseLeave({ param($s, $e) Animate-Card $s 1.0 0 170 })
    $card.Add_MouseLeftButtonDown({ param($s, $e) Animate-Card $s 0.96 0 70 })
    $card.Add_MouseLeftButtonUp({ param($s, $e) Toggle-AppCard $s; Animate-Card $s 1.035 -2 300 'back' })
    return $card
}

foreach ($g in ($Apps | Group-Object { $_.Cat })) {
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
    foreach ($a in $g.Group) {
        $card = New-AppCard $a
        [void]$wrap.Children.Add($card)
        $item = @{ Card = $card; App = $a; Cat = $g.Name; Sec = $sec }
        [void]$sec.Cards.Add($item)
        [void]$script:AppCards.Add($item)
    }
    $panel = New-Object System.Windows.Controls.StackPanel
    [void]$panel.Children.Add($hdr); [void]$panel.Children.Add($wrap)
    $sec.Panel = $panel
    [void]$appsPanel.Children.Add($panel)
    [void]$script:Sections.Add($sec)
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
$txtAppSearch.Add_TextChanged({ Apply-AppFilter })

$btnInstall.Add_Click({ Start-Work {
    $ids = @($script:Selected.Keys)
    if ($ids.Count -eq 0) { Log 'No apps selected.' 'WARN' 'Winget'; return }
    Invoke-Pkg 'install' $ids
    Log 'Install queue finished.' 'INFO' 'Winget'
} })
$btnUninstall.Add_Click({ Start-Work {
    $ids = @($script:Selected.Keys)
    if ($ids.Count -eq 0) { Log 'No apps selected.' 'WARN' 'Winget'; return }
    Invoke-Pkg 'uninstall' $ids
    Log 'Uninstall queue finished.' 'INFO' 'Winget'
} })
$btnUpgrade.Add_Click({ Start-Work {
    if (Test-PkgChoco) {
        $ch = Get-Choco
        if (-not $ch) { Log 'Chocolatey (choco) was not found. Install it from chocolatey.org, or switch back to WinGet.' 'WARN' 'Pkg'; return }
        Log 'Upgrading all packages (choco upgrade all)...' 'INFO' 'Pkg'
        $code = Invoke-External $ch 'upgrade all -y'
        Log "Upgrade finished (exit code $code)." 'INFO' 'Pkg'
        return
    }
    $wg = Get-Winget
    if (-not $wg) { Log 'winget not found.' 'WARN' 'Winget'; return }
    Log 'Upgrading all packages...' 'INFO' 'Winget'
    $code = Invoke-External $wg 'upgrade --all --silent --accept-source-agreements --accept-package-agreements'
    Log "Upgrade finished (exit code $code)." 'INFO' 'Winget'
} })
$btnStopAction.Add_Click({
    if (-not $script:Busy) {
        Log 'No action is running right now.' 'WARN' 'UI'
        return
    }
    $script:CancelRequested = $true
    Log 'Stop requested — cancelling current action...' 'WARN' 'UI'
})
$btnAppsClear.Add_Click({
    $script:Selected.Clear()
    foreach ($it in $script:AppCards) { Set-CardLook $it.Card $false }
    Update-SelCount
})
$btnCollapse.Add_Click({
    foreach ($sec in $script:Sections) { $sec.Wrap.Visibility = 'Collapsed'; $sec.Header.Text = '+ ' + $sec.Cat }
})
$btnExpand.Add_Click({
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
$btnShowInstalled.Add_Click({ Start-Work {
    if (Test-PkgChoco) {
        $ch = Get-Choco
        if (-not $ch) { Log 'Chocolatey (choco) was not found. Install it from chocolatey.org, or switch back to WinGet.' 'WARN' 'Pkg'; return }
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
    $wg = Get-Winget
    if (-not $wg) { Log 'winget not found.' 'WARN' 'Winget'; return }
    Log 'Scanning installed apps (winget list)...' 'INFO' 'Winget'
    $tmp = Join-Path $env:TEMP 'rock_winget_list.txt'
    [void](Invoke-WingetTable 'list --accept-source-agreements' $tmp)
    $txt = ''
    if (Test-Path $tmp) { $txt = Get-Content $tmp -Raw -ErrorAction SilentlyContinue }
    $n = 0
    foreach ($it in $script:AppCards) {
        $hit = $false
        if ($txt) { $hit = [bool]($txt -match [regex]::Escape($it.App.Id)) }
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
$rdPkgWinget.Add_Checked({ $script:Settings['PkgManager'] = 'WinGet'; Save-Settings; Update-PkgUi; Log 'Package manager set to WinGet.' 'INFO' 'Pkg' })
$rdPkgChoco.Add_Checked({ $script:Settings['PkgManager'] = 'Chocolatey'; Save-Settings; Update-PkgUi; Log 'Package manager set to Chocolatey (choco).' 'INFO' 'Pkg' })
Update-PkgUi

