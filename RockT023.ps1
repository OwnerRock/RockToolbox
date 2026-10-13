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
