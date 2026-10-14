# Module: core/Helpers - Progress bar, Log, Start-Work, winget/tweak helpers
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------
function New-RawBrush([string]$hex) { return (New-Object System.Windows.Media.BrushConverter).ConvertFromString($hex) }
# theme-aware: canonical dark colors are mapped through the active palette
function Get-Brush([string]$hex) {
    $k = $hex.TrimStart('#').ToUpper()
    if ($script:Pal -and $k.Length -eq 6 -and $script:Pal.ContainsKey($k)) { $hex = $script:Pal[$k] }
    return (New-RawBrush $hex)
}
# binds a brush property to a theme resource so it follows live theme changes
function Set-Res {
    param($El, [string]$Prop, [string]$Hex)
    $k = $Hex.TrimStart('#').ToUpper()
    if ($script:Pal -and $script:Pal.ContainsKey($k)) {
        $t = $El.GetType(); $dp = $null
        while ($t -and -not $dp) {
            $f = $t.GetField($Prop + 'Property', [System.Reflection.BindingFlags]'Public,Static,DeclaredOnly')
            if ($f) { $dp = $f.GetValue($null) } else { $t = $t.BaseType }
        }
        if ($dp) { $El.SetResourceReference($dp, 'K_' + $k); return }
    }
    $El.$Prop = Get-Brush $Hex
}

function DoEvents {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    [void][System.Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke(
        [System.Windows.Threading.DispatcherPriority]::Background,
        [System.Windows.Threading.DispatcherOperationCallback]{ param($f) $f.Continue = $false; return $null },
        $frame)
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}

# ---- animations
function Start-PageAnim {
    param($El, [double]$FromY = 16)
    try {
        if (-not ($El.RenderTransform -is [System.Windows.Media.TranslateTransform]) -or $El.RenderTransform.IsFrozen) {
            $El.RenderTransform = New-Object System.Windows.Media.TranslateTransform
        }
        $ease = New-Object System.Windows.Media.Animation.CubicEase
        $ease.EasingMode = 'EaseOut'
        $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(280))
        $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
        $fade.EasingFunction = $ease
        $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
        $slide = New-Object System.Windows.Media.Animation.DoubleAnimation($FromY, 0.0, $dur)
        $slide.EasingFunction = $ease
        $El.RenderTransform.BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $slide)
    } catch { }
}
function Start-PageTransition {
    param($El)
    $mode = [string]$script:Settings['PageAnim']
    if ($mode -eq 'Off') { return }
    if ($mode -eq 'Fade') { Start-PageAnim $El 0; return }
    if ($mode -eq 'Zoom') {
        try {
            $sc = New-Object System.Windows.Media.ScaleTransform(0.95, 0.95)
            $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
            $El.RenderTransform = $sc
            $ease = New-Object System.Windows.Media.Animation.CubicEase
            $ease.EasingMode = 'EaseOut'
            $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(300))
            $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
            $fade.EasingFunction = $ease
            $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
            $z = New-Object System.Windows.Media.Animation.DoubleAnimation(0.95, 1.0, $dur)
            $z.EasingFunction = $ease
            $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $z)
            $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $z)
        } catch { }
        return
    }
    Start-PageAnim $El 16
}
function Get-FlashAnim {
    if (-not $script:FlashAnim) {
        $a = New-Object System.Windows.Media.Animation.DoubleAnimation
        $a.From = 0.25; $a.To = 1.0
        $a.Duration = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(240))
        $a.Freeze()
        $script:FlashAnim = $a
    }
    return $script:FlashAnim
}

# glides the progress bar to a new value (jumps back instantly when the value goes down)
function Set-PbValue {
    param([double]$V, [switch]$Instant)
    $prop = [System.Windows.Controls.Primitives.RangeBase]::ValueProperty
    $cur = [double]$pbMain.Value
    if ($Instant -or $V -lt $cur) {
        $pbMain.BeginAnimation($prop, $null)
        $pbMain.Value = $V
        return
    }
    if ([Math]::Abs($V - $cur) -lt 0.05) { return }
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation
    $a.To = $V
    $a.Duration = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(420))
    $ease = New-Object System.Windows.Media.Animation.CubicEase
    $ease.EasingMode = 'EaseOut'
    $a.EasingFunction = $ease
    $pbMain.BeginAnimation($prop, $a)
}

# waits while keeping the window rendering at ~60 fps (animations stay smooth)
function Pump-Wait {
    param([int]$Ms)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $Ms) { DoEvents; Start-Sleep -Milliseconds 16 }
}

# progress bar at the bottom of the window ( -1 = indeterminate )
function Set-Progress {
    param([double]$Pct = -1, [string]$Text)
    if ($Text) { $txtStatus.Text = L $Text }
    if ($Pct -lt 0) {
        $pbMain.IsIndeterminate = $true
        $txtPct.Text = ''
    } else {
        if ($Pct -gt 100) { $Pct = 100 }
        $pbMain.IsIndeterminate = $false
        Set-PbValue $Pct
        $txtPct.Text = ('{0:0}%' -f $Pct)
    }
    DoEvents
}

# every message goes to the Rock console; the last one is also shown next to the progress bar
function Log {
    param([string]$msg, [string]$level = 'INFO', [string]$tag = 'UI')
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $color = 'Gray'
    if ($level -eq 'ERROR') { $color = 'Red' } elseif ($level -eq 'WARN') { $color = 'Yellow' }
    Write-Host ('[' + $ts + '] [' + $level + '] [' + $tag + '] ' + $msg) -ForegroundColor $color
    if ($txtStatus -and -not $msg.StartsWith(' ')) {
        $txtStatus.Text = L ($msg.Trim())
        try { $txtStatus.BeginAnimation([System.Windows.UIElement]::OpacityProperty, (Get-FlashAnim)) } catch { }
        if ($level -eq 'ERROR') { Set-Res $txtStatus 'Foreground' '#FF6B6B' }
        elseif ($level -eq 'WARN') { Set-Res $txtStatus 'Foreground' '#FFB454' }
        else { Set-Res $txtStatus 'Foreground' '#C9CDE0' }
    }
    DoEvents
}

$script:Busy = $false
$script:CancelRequested = $false
$script:SegStart = -1.0
$script:SegEnd = -1.0
function Start-Work {
    param([scriptblock]$Block, [string]$Title = 'Working...')
    if ($script:Busy) { Log 'Another task is still running, please wait...' 'WARN' 'Task'; return }
    $script:Busy = $true
    $script:CancelRequested = $false
    $script:SegStart = -1.0; $script:SegEnd = -1.0
    $window.Cursor = [System.Windows.Input.Cursors]::Wait
    try { $btnStopGlobal.Visibility = 'Visible' } catch { }
    $pbMain.Tag = 'busy'
    Set-Res $txtStatus 'Foreground' '#C9CDE0'
    Set-Progress -1 $Title
    $ok = $true
    $cancelled = $false
    try { & $Block }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user|cancelled by user') {
            $cancelled = $true
            $ok = $false
            Log 'Action stopped by user.' 'WARN' 'Task'
        } else {
            $ok = $false
            Log $_.Exception.Message 'ERROR' 'Task'
        }
    }
    finally {
        $script:Busy = $false
        $script:CancelRequested = $false
        $script:SegStart = -1.0; $script:SegEnd = -1.0
        $window.Cursor = $null
        $pbMain.Tag = $null
        try { $btnStopGlobal.Visibility = 'Collapsed' } catch { }
    }
    if ($ok) {
        $t = $null
        if ($txtStatus.Text -eq (L $Title)) { $t = L 'Done' }
        Set-Progress 100 $t
    } else {
        $pbMain.IsIndeterminate = $false; Set-PbValue 0 -Instant; $txtPct.Text = ''
        if ($cancelled) { $txtStatus.Text = L 'Stopped'; Set-Res $txtStatus 'Foreground' '#FFB454' }
    }
}

function Set-Reg {
    param([string]$Path, [string]$Name, $Value, [string]$Type = 'DWord')
    if (-not (Test-Path -LiteralPath $Path)) { New-Item -Path $Path -Force | Out-Null }
    New-ItemProperty -LiteralPath $Path -Name $Name -Value $Value -PropertyType $Type -Force | Out-Null
}
function Remove-Reg {
    param([string]$Path, [string]$Name)
    Remove-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction SilentlyContinue
}

# runs a program hidden; while it runs the progress bar creeps through the current segment ($script:SegStart..SegEnd)
function Invoke-External {
    param([string]$File, [string]$ArgString, [string]$OutFile)
    if ($script:CancelRequested) { throw 'Cancelled by user' }
    $sp = @{ FilePath = $File; ArgumentList = $ArgString; WindowStyle = 'Hidden'; PassThru = $true }
    if ($OutFile) { $sp['RedirectStandardOutput'] = $OutFile }
    $p = Start-Process @sp
    $null = $p.Handle
    $t0 = Get-Date
    $lastSet = [DateTime]::MinValue
    while (-not $p.HasExited) {
        if ($script:CancelRequested) {
            try { $p.Kill() } catch { }
            try { $p.WaitForExit(2000) } catch { }
            throw 'Cancelled by user'
        }
        DoEvents
        Start-Sleep -Milliseconds 16
        if ($script:SegStart -ge 0 -and ([DateTime]::Now - $lastSet).TotalMilliseconds -ge 200) {
            $lastSet = [DateTime]::Now
            $el = ((Get-Date) - $t0).TotalSeconds
            $frac = 1 - [Math]::Exp(-$el / 25.0)
            Set-Progress ($script:SegStart + ($script:SegEnd - $script:SegStart) * $frac * 0.92)
        }
    }
    return $p.ExitCode
}

function Get-RemoteFile {
    param([string]$Url, [string]$Path)
    if ($script:CancelRequested) { throw 'Cancelled by user' }
    $wc = New-Object System.Net.WebClient
    $wc.Headers['User-Agent'] = 'Mozilla/5.0 RockToolbox'
    $wc.Add_DownloadProgressChanged({
        param($s, $e)
        try { Set-Progress ([double]$e.ProgressPercentage) ('Downloading... ' + [int]($e.BytesReceived / 1KB) + ' KB') } catch { }
    })
    $task = $wc.DownloadFileTaskAsync($Url, $Path)
    while (-not $task.IsCompleted) {
        if ($script:CancelRequested) {
            try { $wc.CancelAsync() } catch { }
            $wc.Dispose()
            throw 'Cancelled by user'
        }
        DoEvents
        Start-Sleep -Milliseconds 16
    }
    $wc.Dispose()
    if ($task.IsFaulted) { throw $task.Exception.InnerException }
}

function Get-Winget {
    $w = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($w) { return $w.Source }
    return $null
}

# runs winget inside its own hidden console that is 400 columns wide, so table columns (IDs) are never truncated
function Invoke-WingetTable {
    param([string]$WingetArgs, [string]$OutFile)
    Remove-Item $OutFile -Force -ErrorAction SilentlyContinue
    $cmd = '/d /c "mode con: cols=400 lines=9999 >nul 2>&1 & winget.exe ' + $WingetArgs + ' > "' + $OutFile + '""'
    return (Invoke-External 'cmd.exe' $cmd)
}

function Get-WingetMessage([int]$code) {
    if ($code -eq 0) { return 'OK' }
    if ($code -eq -1978335189) { return 'already installed (no newer version available)' }
    if ($code -eq -1978335212) { return 'package not found in the winget source' }
    if ($code -eq -1978335216) { return 'no applicable installer for this PC' }
    if ($code -eq -1978335215) { return 'installer hash mismatch' }
    if ($code -eq -1978335224) { return 'download failed (check your internet)' }
    if ($code -eq 3010 -or $code -eq 1641) { return 'installed - restart required' }
    return "finished with exit code $code"
}

function Invoke-Winget {
    param([string]$Verb, [string[]]$Ids)
    $wg = Get-Winget
    if (-not $wg) { Log 'winget not found. Install "App Installer" from the Microsoft Store, then try again.' 'WARN' 'Winget'; return }
    $n = $Ids.Count; $i = 0
    foreach ($id in $Ids) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $script:SegStart = 100.0 * $i / $n
        $script:SegEnd   = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ($Verb + ': ' + $id + '  (' + ($i + 1) + '/' + $n + ')')
        Log "winget $Verb : $id  ($($i + 1)/$n)" 'INFO' 'Winget'
        $wgArgs = "$Verb --id $id -e --silent --accept-source-agreements"
        if ($Verb -eq 'install' -or $Verb -eq 'upgrade') { $wgArgs += ' --accept-package-agreements' }
        $code = Invoke-External $wg $wgArgs
        $msg = Get-WingetMessage $code
        if ($code -eq 0 -or $code -eq -1978335189 -or $code -eq 3010) { Log ('  ' + $msg) 'INFO' 'Winget' } else { Log ('  ' + $msg) 'WARN' 'Winget' }
        $i++
    }
    $script:SegStart = -1.0
}

# ---- Chocolatey support (Install tab > Package Manager switch)
function Get-Choco {
    $c = Get-Command choco.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $p = Join-Path $env:ProgramData 'chocolatey\bin\choco.exe'
    if (Test-Path -LiteralPath $p) { return $p }
    return $null
}
function Test-PkgChoco { return ([string]$script:Settings['PkgManager'] -eq 'Chocolatey') }
# winget IDs look like Vendor.App while choco IDs are the bare app name (Valve.Steam -> steam)
function ConvertTo-ChocoId {
    param([string]$Id)
    $s = $Id
    if ($s -match '\.') { $s = ($s -split '\.')[-1] }
    return ($s -replace '[^A-Za-z0-9._-]', '').ToLower()
}
function Invoke-Choco {
    param([string]$Verb, [string[]]$Ids)
    $ch = Get-Choco
    if (-not $ch) { Log 'Chocolatey (choco) was not found. Install it from chocolatey.org, or switch back to WinGet.' 'WARN' 'Pkg'; return }
    $n = $Ids.Count; $i = 0
    foreach ($id in $Ids) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $script:SegStart = 100.0 * $i / $n
        $script:SegEnd   = 100.0 * ($i + 1) / $n
        $cid = ConvertTo-ChocoId $id
        Set-Progress $script:SegStart ($Verb + ': ' + $cid + '  (' + ($i + 1) + '/' + $n + ')')
        Log "choco $Verb : $cid  ($($i + 1)/$n)" 'INFO' 'Pkg'
        $code = Invoke-External $ch "$Verb $cid -y"
        if ($code -eq 0) { Log '  OK' 'INFO' 'Pkg' } else { Log ("  finished with exit code $code") 'WARN' 'Pkg' }
        $i++
    }
    $script:SegStart = -1.0
}
function Invoke-Pkg {
    param([string]$Verb, [string[]]$Ids)
    if (Test-PkgChoco) { Invoke-Choco $Verb $Ids } else { Invoke-Winget $Verb $Ids }
}

function New-Label([string]$title, [string]$desc) {
    $sp = New-Object System.Windows.Controls.StackPanel
    $t1 = New-Object System.Windows.Controls.TextBlock
    $t1.Text = $title; $t1.FontWeight = 'SemiBold'; Set-Res $t1 'Foreground' '#F2F4FF'
    [void]$sp.Children.Add($t1)
    if ($desc) {
        $t2 = New-Object System.Windows.Controls.TextBlock
        $t2.Text = $desc; $t2.FontSize = 11.5; Set-Res $t2 'Foreground' '#8D93AD'; $t2.TextWrapping = 'Wrap'; $t2.MaxWidth = 760
        [void]$sp.Children.Add($t2)
    }
    return $sp
}

function Reset-WindowsUpdate {
    Log 'Resetting Windows Update components...' 'INFO' 'Update'
    $svcs = 'wuauserv', 'bits', 'cryptsvc', 'msiserver'
    foreach ($s in $svcs) { Stop-Service $s -Force -ErrorAction SilentlyContinue }
    $sd = "$env:windir\SoftwareDistribution"; $cr = "$env:windir\System32\catroot2"
    Remove-Item "$sd.bak" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "$cr.bak" -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $sd) { Rename-Item $sd "$sd.bak" -ErrorAction SilentlyContinue }
    if (Test-Path $cr) { Rename-Item $cr "$cr.bak" -ErrorAction SilentlyContinue }
    foreach ($s in $svcs) { Start-Service $s -ErrorAction SilentlyContinue }
    Log 'Update components reset.' 'INFO' 'Update'
}

# tweak runner: a tweak is either a list of registry Ops (V = apply value, U = undo value, Rm = remove on undo) or Apply/Undo scriptblocks
function Invoke-TweakAction {
    param($T, [string]$Mode)
    if ($T.Ops) {
        foreach ($op in $T.Ops) {
            $type = 'DWord'
            if ($op.T) { $type = $op.T }
            if ($Mode -eq 'Apply') { Set-Reg $op.P $op.N $op.V $type }
            elseif ($op.Rm) { Remove-Reg $op.P $op.N }
            elseif ($op.ContainsKey('U')) { Set-Reg $op.P $op.N $op.U $type }
        }
        return
    }
    if ($Mode -eq 'Apply') { & $T.Apply } else { & $T.Undo }
}

