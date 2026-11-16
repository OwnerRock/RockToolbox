# ==============================================================================
# MODULE: src\core\Helpers.ps1
# ==============================================================================
# Module: core/Helpers - Progress bar, Log, Start-Work, winget/tweak helpers
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------
$script:RawConv = $null
function New-RawBrush([string]$hex) {
    if (-not $script:RawConv) { $script:RawConv = New-Object System.Windows.Media.BrushConverter }
    return $script:RawConv.ConvertFromString($hex)
}
# theme-aware: canonical dark colors are mapped through the active palette
function Get-Brush([string]$hex) {
    $k = $hex.TrimStart('#').ToUpper()
    if ($script:Pal -and $k.Length -eq 6 -and $script:Pal.ContainsKey($k)) { $hex = $script:Pal[$k] }
    return (New-RawBrush $hex)
}
# binds a brush property to a theme resource so it follows live theme changes
$script:ResDpCache = @{}
function Set-Res {
    param($El, [string]$Prop, [string]$Hex)
    $k = $Hex.TrimStart('#').ToUpper()
    if ($script:Pal -and $script:Pal.ContainsKey($k)) {
        $ck = $El.GetType().Name + ':' + $Prop
        $dp = $script:ResDpCache[$ck]
        if (-not $dp) {
            $t = $El.GetType()
            while ($t -and -not $dp) {
                $f = $t.GetField($Prop + 'Property', [System.Reflection.BindingFlags]'Public,Static,DeclaredOnly')
                if ($f) { $dp = $f.GetValue($null) } else { $t = $t.BaseType }
            }
            $script:ResDpCache[$ck] = $dp
        }
        if ($dp) { $El.SetResourceReference($dp, 'K_' + $k); return }
    }
    $El.$Prop = Get-Brush $Hex
}

# Log / Set-Progress used to pump the UI on EVERY call (hundreds of nested message loops during a long task = stutter).
# Pump-UiSoft pumps at most every 30 ms instead.
$script:PumpSw = [System.Diagnostics.Stopwatch]::StartNew()
function Pump-UiSoft {
    if ($script:PumpSw.ElapsedMilliseconds -ge 30) { DoEvents; $script:PumpSw.Restart() }
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
# pop-in for dialog windows: fade + scale up + slide
function Start-DialogAnim {
    param($El)
    try {
        $El.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
        $g = New-Object System.Windows.Media.TransformGroup
        $sc = New-Object System.Windows.Media.ScaleTransform(0.94, 0.94)
        $tr = New-Object System.Windows.Media.TranslateTransform(0, 14)
        [void]$g.Children.Add($sc); [void]$g.Children.Add($tr)
        $El.RenderTransform = $g
        $ease = New-Object System.Windows.Media.Animation.CubicEase
        $ease.EasingMode = 'EaseOut'
        $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(300))
        $fade = New-Object System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, $dur)
        $fade.EasingFunction = $ease
        $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $fade)
        $z = New-Object System.Windows.Media.Animation.DoubleAnimation(0.94, 1.0, $dur)
        $z.EasingFunction = $ease
        $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $z)
        $sc.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $z)
        $sl = New-Object System.Windows.Media.Animation.DoubleAnimation(14.0, 0.0, $dur)
        $sl.EasingFunction = $ease
        $tr.BeginAnimation([System.Windows.Media.TranslateTransform]::YProperty, $sl)
    } catch { }
}
# breathing red dot (update badge)
function Start-DotPulse {
    param($El)
    try {
        $ease = New-Object System.Windows.Media.Animation.SineEase
        $ease.EasingMode = 'EaseInOut'
        $dur = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(900))
        $a = New-Object System.Windows.Media.Animation.DoubleAnimation(1.0, 0.4, $dur)
        $a.AutoReverse = $true; $a.RepeatBehavior = (New-Object System.Windows.Media.Animation.RepeatBehavior(5.0)); $a.EasingFunction = $ease
        $El.BeginAnimation([System.Windows.UIElement]::OpacityProperty, $a)
        $z = New-Object System.Windows.Media.Animation.DoubleAnimation(1.0, 1.3, $dur)
        $z.AutoReverse = $true; $z.RepeatBehavior = (New-Object System.Windows.Media.Animation.RepeatBehavior(5.0)); $z.EasingFunction = $ease
        $El.RenderTransform.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleXProperty, $z)
        $El.RenderTransform.BeginAnimation([System.Windows.Media.ScaleTransform]::ScaleYProperty, $z)
    } catch { }
}
# page intro for every page: the toolbar buttons, filter rows and panels pop in one after another
function Get-StaggerTargets {
    param($Container, [System.Collections.ArrayList]$Out, [int]$Depth)
    if (-not $Container) { return }
    $kids = @()
    if ($Container -is [System.Windows.Controls.Panel]) { $kids = @($Container.Children) }
    elseif ($Container -is [System.Windows.Controls.Decorator]) { $kids = @($Container.Child) }
    elseif ($Container -is [System.Windows.Controls.ContentControl]) { $kids = @($Container.Content) }
    foreach ($c in $kids) {
        if (-not ($c -is [System.Windows.UIElement])) { continue }
        if ($c.Visibility -ne 'Visible') { continue }
        $isBar = (($c -is [System.Windows.Controls.WrapPanel]) -or ($c -is [System.Windows.Controls.DockPanel]) -or
                  (($c -is [System.Windows.Controls.StackPanel]) -and $c.Orientation -eq 'Horizontal'))
        if ($isBar -and $Depth -lt 2 -and $c.Children.Count -gt 0 -and $c.Children.Count -le 16) { Get-StaggerTargets $c $Out ($Depth + 1) }
        elseif (($c -is [System.Windows.Controls.ScrollViewer]) -and $Depth -lt 1) { Get-StaggerTargets $c $Out ($Depth + 1) }
        else { [void]$Out.Add($c) }
    }
}
function Start-PageStagger {
    param([string]$Name)
    if (-not [bool]$script:Settings['CardAnim']) { return }
    if ($Name -eq 'Install') { return }      # the Install page has its own card intro
    $page = $pages[$Name]
    if (-not $page) { return }
    $targets = New-Object System.Collections.ArrayList
    Get-StaggerTargets $page $targets 0
    $i = 0
    foreach ($c in $targets) {
        if ($i -ge 26) { break }
        try { Initialize-CardAnim $c; Start-CardFade $c (40 + $i * 30) } catch { }
        $i++
    }
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
    Pump-UiSoft
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
    Pump-UiSoft
}

$script:Busy = $false
$script:CancelRequested = $false
$script:SegStart = -1.0
$script:SegEnd = -1.0
$script:WorkFailed = 0
function Start-Work {
    param([scriptblock]$Block, [string]$Title = 'Working...')
    if ($script:Busy) { Log 'Another task is still running, please wait...' 'WARN' 'Task'; return }
    $script:Busy = $true
    $script:CancelRequested = $false
    $script:SegStart = -1.0; $script:SegEnd = -1.0
    $script:WorkFailed = 0
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
        if ($script:WorkFailed -gt 0) {
            Set-Progress 100 'Finished with errors - check the log'
            Set-Res $txtStatus 'Foreground' '#FFB454'
        } else {
            Set-Progress 100 $t
        }
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

# ---- winget / Chocolatey downloads and updates run in a VISIBLE CMD window, so you can watch every download and install.
# The window stays a few seconds at the end (longer when it failed) so the result can be read. Returns the exit code.
function Invoke-PkgWindow {
    param([string]$Title, [string]$File, [string]$ArgString)
    if ($script:CancelRequested) { throw 'Cancelled by user' }
    $ttl = ($Title -replace '[&|<>^%"]', '')
    $bat = Join-Path $env:TEMP ('rock_pkg_' + [guid]::NewGuid().ToString('N') + '.cmd')
    $lines = @(
        '@echo off',
        'chcp 65001 >nul',
        ('title Rock Toolbox - ' + $ttl),
        'echo.',
        ('echo   ' + $ttl),
        'echo.',
        ('"' + $File + '" ' + $ArgString),
        'set RC=%errorlevel%',
        'echo.',
        'echo ===== finished - exit code %RC% =====',
        'if "%RC%"=="0" (timeout /t 3 /nobreak >nul) else (timeout /t 10 >nul)',
        'exit /b %RC%'
    )
    [System.IO.File]::WriteAllText($bat, (($lines -join "`r`n") + "`r`n"), [System.Text.Encoding]::ASCII)
    $code = -1
    try {
        $p = Start-Process -FilePath 'cmd.exe' -ArgumentList ('/c ""' + $bat + '""') -WindowStyle Normal -PassThru
        $null = $p.Handle
        $t0 = Get-Date
        $lastSet = [DateTime]::MinValue
        while (-not $p.HasExited) {
            if ($script:CancelRequested) {
                try { [void](Start-Process -FilePath 'taskkill.exe' -ArgumentList ('/PID ' + $p.Id + ' /T /F') -WindowStyle Hidden -Wait) } catch { }
                throw 'Cancelled by user'
            }
            DoEvents
            Start-Sleep -Milliseconds 16
            if ($script:SegStart -ge 0 -and ([DateTime]::Now - $lastSet).TotalMilliseconds -ge 300) {
                $lastSet = [DateTime]::Now
                $el = ((Get-Date) - $t0).TotalSeconds
                $frac = 1 - [Math]::Exp(-$el / 40.0)
                Set-Progress ($script:SegStart + ($script:SegEnd - $script:SegStart) * $frac * 0.92)
            }
        }
        $code = $p.ExitCode
    } finally { Remove-Item -LiteralPath $bat -Force -ErrorAction SilentlyContinue }
    return $code
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
    $found = $null
    $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\winget.exe'
    if (Test-Path -LiteralPath $alias) { $found = $alias }
    if (-not $found) {
        try {
            $pk = Get-AppxPackage -Name Microsoft.DesktopAppInstaller -ErrorAction Stop | Sort-Object Version -Descending | Select-Object -First 1
            if ($pk) { $e = Join-Path $pk.InstallLocation 'winget.exe'; if (Test-Path -LiteralPath $e) { $found = $e } }
        } catch { }
    }
    if (-not $found) {
        try {
            $wa = Join-Path $env:ProgramFiles 'WindowsApps'
            $c = @(Get-ChildItem -Path (Join-Path $wa 'Microsoft.DesktopAppInstaller_*__8wekyb3d8bbwe') -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
            foreach ($d in $c) { $e = Join-Path $d.FullName 'winget.exe'; if (Test-Path -LiteralPath $e) { $found = $e; break } }
        } catch { }
    }
    if ($found) {
        # make sure "winget.exe" also works by name (the table helper runs it through cmd)
        $dir = Split-Path -Parent $found
        if (($env:Path -split ';') -notcontains $dir) { $env:Path = $env:Path + ';' + $dir }
        return $found
    }
    return $null
}

function Update-SessionPath {
    try {
        $m = [Environment]::GetEnvironmentVariable('Path', 'Machine'); $u = [Environment]::GetEnvironmentVariable('Path', 'User')
        $env:Path = (@($m, $u, $env:Path) | Where-Object { $_ }) -join ';'
    } catch { }
}

# runs a .ps1 in a fresh PowerShell and captures EVERYTHING it prints (output + errors) in a log file; returns the exit code
function Invoke-PSFile {
    param([string]$File, [string]$Log)
    $cmd = '/c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $File + '" > "' + $Log + '" 2>&1"'
    return (Invoke-External 'cmd.exe' $cmd)
}

function Show-InstallFailure {
    param([string]$What, [string[]]$Details, [string]$Help)
    $body = $What + "`n"
    $d = @($Details | Where-Object { $_ })
    if ($d.Count -gt 0) { $body += "`n" + ($d -join "`n") + "`n" }
    $body += "`n" + $Help
    try { [void](Show-RockMessage -Text $body -Caption 'Rock Toolbox' -Buttons 'OK' -Icon 'Error') } catch { }
}

# installs "App Installer" (= winget) when it is missing. Routes: 1) register the copy already on the PC, 2) PowerShell Gallery
# (Microsoft.WinGet.Client), 3) direct download from Microsoft. Every failure is collected and shown in a readable message.
function Install-Winget {
    try { [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12 } catch { }
    $help = "What you can do:`n1) Open the Microsoft Store, search for ""App Installer"" and press Get / Update.`n2) Or download https://aka.ms/getwinget and double-click the file.`n3) Make sure Windows is up to date and the PC is online, then restart Rock Toolbox."
    if ([Environment]::OSVersion.Version.Build -lt 17763) {
        Log 'This Windows build is too old for winget (Windows 10 version 1809 or newer is needed).' 'ERROR' 'Winget'
        if (-not $script:WingetFailShown) { $script:WingetFailShown = $true; Show-InstallFailure 'winget needs Windows 10 version 1809 or newer.' @() 'Update Windows first, then try again.' }
        return $null
    }
    Log 'winget (App Installer) was not found - installing it automatically...' 'INFO' 'Winget'
    $errs = New-Object System.Collections.ArrayList
    $tmp = Join-Path $env:TEMP 'RockWingetSetup'
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $arch = 'x64'
    switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' { $arch = 'arm64' } 'x86' { $arch = 'x86' } }

    # 1) the package is often already on the PC but not registered for this user
    $reg = Join-Path $tmp 'register.ps1'; $regLog = Join-Path $tmp 'register.log'
    Set-Content -LiteralPath $reg -Encoding UTF8 -Value "`$ErrorActionPreference = 'Stop'`r`ntry { Add-AppxPackage -RegisterByFamilyName -MainPackage 'Microsoft.DesktopAppInstaller_8wekyb3d8bbwe'; exit 0 } catch { Write-Output `$_.Exception.Message; exit 1 }"
    [void](Invoke-PSFile $reg $regLog)
    Update-SessionPath
    $w = Get-Winget
    if ($w) { Log '  winget is ready.' 'INFO' 'Winget'; return $w }

    # 2) PowerShell Gallery: Microsoft.WinGet.Client repairs / installs winget
    Log '  trying the PowerShell Gallery route (this can take a few minutes)...' 'INFO' 'Winget'
    $gal = Join-Path $tmp 'gallery.ps1'; $galLog = Join-Path $tmp 'gallery.log'
    Set-Content -LiteralPath $gal -Encoding UTF8 -Value @'
$ErrorActionPreference = 'Stop'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    try { Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null } catch { }
    try { Set-PSRepository -Name PSGallery -InstallationPolicy Trusted } catch { }
    Install-Module -Name Microsoft.WinGet.Client -Force -AllowClobber -Scope CurrentUser -Repository PSGallery | Out-Null
    Import-Module Microsoft.WinGet.Client -Force
    try { Repair-WinGetPackageManager -Latest -Force } catch { Repair-WinGetPackageManager -Force }
    exit 0
} catch { Write-Output $_.Exception.Message; exit 1 }
'@
    $code = Invoke-PSFile $gal $galLog
    Update-SessionPath
    $w = Get-Winget
    if ($w) { Log '  winget was installed and is ready.' 'INFO' 'Winget'; return $w }
    foreach ($ln in @(Get-OutputTail $galLog 2)) { if ($ln) { [void]$errs.Add('PowerShell Gallery: ' + $ln) } }

    # 3) direct download from Microsoft: runtime packages + App Installer
    $deps = @()
    $vc = Join-Path $tmp 'VCLibs.appx'
    try { Log '  downloading the Visual C++ runtime package...' 'INFO' 'Winget'; Get-RemoteFile ('https://aka.ms/Microsoft.VCLibs.' + $arch + '.14.00.Desktop.appx') $vc; $deps += $vc }
    catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; [void]$errs.Add('VCLibs download: ' + $_.Exception.Message) }
    $xaml = Join-Path $tmp 'UIXaml.appx'
    try { Log '  downloading the UI.Xaml package...' 'INFO' 'Winget'; Get-RemoteFile ('https://github.com/microsoft/microsoft-ui-xaml/releases/download/v2.8.6/Microsoft.UI.Xaml.2.8.' + $arch + '.appx') $xaml; $deps += $xaml }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user') { throw }
        # fallback: the same package from NuGet (a zip file)
        try {
            $nup = Join-Path $tmp 'uixaml.zip'
            Get-RemoteFile 'https://www.nuget.org/api/v2/package/Microsoft.UI.Xaml/2.8.6' $nup
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $zip = [System.IO.Compression.ZipFile]::OpenRead($nup)
            $entry = $zip.Entries | Where-Object { $_.FullName -like ('tools/AppX/' + $arch + '/Release/Microsoft.UI.Xaml.2.8.appx') } | Select-Object -First 1
            if ($entry) { [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $xaml, $true); $deps += $xaml }
            $zip.Dispose()
        } catch { [void]$errs.Add('UI.Xaml download: ' + $_.Exception.Message) }
    }
    $bundle = Join-Path $tmp 'AppInstaller.msixbundle'
    $got = $false
    try { Log '  downloading App Installer (about 200 MB, please wait)...' 'INFO' 'Winget'; Get-RemoteFile 'https://aka.ms/getwinget' $bundle; $got = $true }
    catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; [void]$errs.Add('App Installer download: ' + $_.Exception.Message) }
    if ($got) {
        $cmd = 'Add-AppxPackage -Path "' + $bundle + '" -ForceUpdateFromAnyVersion'
        if ($deps.Count -gt 0) { $cmd += ' -DependencyPath ' + (($deps | ForEach-Object { '"' + $_ + '"' }) -join ',') }
        $inst = Join-Path $tmp 'install.ps1'; $instLog = Join-Path $tmp 'install.log'
        Set-Content -LiteralPath $inst -Encoding UTF8 -Value ("`$ErrorActionPreference = 'Stop'`r`ntry { " + $cmd + "; exit 0 } catch { Write-Output `$_.Exception.Message; exit 1 }")
        Log '  installing App Installer...' 'INFO' 'Winget'
        $code = Invoke-PSFile $inst $instLog
        Update-SessionPath
        $w = Get-Winget
        if ($w) {
            Log '  winget was installed and is ready.' 'INFO' 'Winget'
            Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
            return $w
        }
        foreach ($ln in @(Get-OutputTail $instLog 3)) { if ($ln) { [void]$errs.Add('Install: ' + $ln) } }
    }
    foreach ($e in $errs) { Log ('  ' + $e) 'WARN' 'Winget' }
    Log '  automatic install of winget failed.' 'ERROR' 'Winget'
    if (-not $script:WingetFailShown) {
        $script:WingetFailShown = $true
        Show-InstallFailure 'winget (App Installer) could not be installed automatically.' @($errs | Select-Object -First 4) $help
    }
    return $null
}

function Ensure-Winget {
    $w = Get-Winget
    if ($w) { return $w }
    try { return (Install-Winget) }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user') { throw }
        Log ('Could not install winget: ' + $_.Exception.Message) 'ERROR' 'Winget'
        return $null
    }
}

# installs Chocolatey with the official script when it is missing (and falls back to winget)
function Install-Choco {
    try { [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12 } catch { }
    Log 'Chocolatey was not found - installing it automatically...' 'INFO' 'Choco'
    $errs = New-Object System.Collections.ArrayList
    $tmp = Join-Path $env:TEMP 'RockChocoSetup'
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    # a folder left behind by an earlier failed install makes the official installer stop - move it aside
    $root = $env:ChocolateyInstall
    if (-not $root) { $root = Join-Path $env:ProgramData 'chocolatey' }
    if ((Test-Path -LiteralPath $root) -and -not (Test-Path -LiteralPath (Join-Path $root 'bin\choco.exe'))) {
        try {
            $newName = (Split-Path -Leaf $root) + '.old-' + (Get-Date -Format 'yyyyMMddHHmmss')
            Rename-Item -LiteralPath $root -NewName $newName -ErrorAction Stop
            Log '  moved an unfinished Chocolatey folder aside.' 'INFO' 'Choco'
        } catch { try { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction Stop } catch { } }
    }
    $file = Join-Path $tmp 'install.ps1'; $out = Join-Path $tmp 'install.log'
    $ok = $false
    try {
        Log '  downloading the Chocolatey installer...' 'INFO' 'Choco'
        Get-RemoteFile 'https://community.chocolatey.org/install.ps1' $file
        $ok = $true
    } catch {
        if ($_.Exception.Message -match 'Cancelled by user') { throw }
        [void]$errs.Add('Download: ' + $_.Exception.Message)
    }
    if ($ok) {
        Log '  running the installer...' 'INFO' 'Choco'
        $code = Invoke-PSFile $file $out
        Update-SessionPath
        try { $ci = [Environment]::GetEnvironmentVariable('ChocolateyInstall', 'Machine'); if ($ci) { $env:ChocolateyInstall = $ci } } catch { }
        $c = Get-Choco
        if ($c) {
            Log '  Chocolatey was installed and is ready.' 'INFO' 'Choco'
            Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
            return $c
        }
        foreach ($ln in @(Get-OutputTail $out 3)) { if ($ln) { [void]$errs.Add('Installer: ' + $ln) } }
    }
    # second route: winget
    $wg = Get-Winget
    if ($wg) {
        Log '  trying winget (Chocolatey.Chocolatey)...' 'INFO' 'Choco'
        [void](Invoke-External $wg 'install --id Chocolatey.Chocolatey -e --silent --accept-source-agreements --accept-package-agreements')
        Update-SessionPath
        $c = Get-Choco
        if ($c) { Log '  Chocolatey was installed with winget.' 'INFO' 'Choco'; return $c }
        [void]$errs.Add('winget could not install it either.')
    }
    foreach ($e in $errs) { Log ('  ' + $e) 'WARN' 'Choco' }
    Log '  automatic install of Chocolatey failed.' 'ERROR' 'Choco'
    if (-not $script:ChocoFailShown) {
        $script:ChocoFailShown = $true
        Show-InstallFailure 'Chocolatey could not be installed automatically.' @($errs | Select-Object -First 4) "What you can do:`n1) Open https://chocolatey.org/install and follow the steps in an Administrator PowerShell.`n2) Check that the PC is online and that antivirus / firewall is not blocking community.chocolatey.org.`n3) Then restart Rock Toolbox."
    }
    return $null
}

function Ensure-Choco {
    $c = Get-Choco
    if ($c) { return $c }
    try { return (Install-Choco) }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user') { throw }
        Log ('Could not install Chocolatey: ' + $_.Exception.Message) 'ERROR' 'Choco'
        return $null
    }
}

# startup check: if winget (App Installer) or Chocolatey is missing, download and install it
function Start-PrereqCheck {
    if (-not [bool]$script:Settings['AutoInstallPkgMgrs']) { return }
    if ($script:Busy) { return }
    $needW = -not (Get-Winget)
    $needC = -not (Get-Choco)
    if (-not $needW -and -not $needC) { return }
    $miss = @()
    if ($needW) { $miss += 'winget (App Installer)' }
    if ($needC) { $miss += 'Chocolatey' }
    Log ('Missing package manager: ' + ($miss -join ' + ') + ' - installing automatically...') 'INFO' 'Setup'
    Start-Work {
        if ($needW) { [void](Ensure-Winget) }
        if ($needC) { [void](Ensure-Choco) }
    } 'Installing missing package managers...'
}

# ==============================================================================
# Full removal helpers: OneDrive, Microsoft Edge, Internet Explorer
# ==============================================================================
function Stop-ProcByName {
    param([string[]]$Names)
    foreach ($n in $Names) { foreach ($p in @(Get-Process -Name $n -ErrorAction SilentlyContinue)) { try { $p.Kill() } catch { } } }
}

# delete a file / folder even when it is protected: normal delete -> take ownership + grant Administrators -> delete again
function Remove-PathForce {
    param([string]$Path)
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) { return $true }
    try { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop } catch { }
    if (-not (Test-Path -LiteralPath $Path)) { return $true }
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    if ($item -and $item.PSIsContainer) {
        [void](Invoke-External 'takeown.exe' ('/F "' + $Path + '" /R /D Y'))
        [void](Invoke-External 'icacls.exe' ('"' + $Path + '" /grant *S-1-5-32-544:F /T /C /Q'))
    } else {
        [void](Invoke-External 'takeown.exe' ('/F "' + $Path + '"'))
        [void](Invoke-External 'icacls.exe' ('"' + $Path + '" /grant *S-1-5-32-544:F /C /Q'))
    }
    try { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    return (-not (Test-Path -LiteralPath $Path))
}

function Remove-RegKeyForce {
    param([string]$Path)
    try { if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue } } catch { }
}

function Remove-ShortcutsNamed {
    param([string[]]$Names)
    $dirs = @(
        (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'),
        (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'),
        (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Accessories'),
        (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Accessories'),
        (Join-Path $env:PUBLIC 'Desktop'),
        [Environment]::GetFolderPath('Desktop'),
        (Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar')
    ) | Where-Object { $_ } | Select-Object -Unique
    foreach ($d in $dirs) {
        if (-not (Test-Path -LiteralPath $d)) { continue }
        foreach ($n in $Names) {
            foreach ($f in @(Get-ChildItem -LiteralPath $d -Filter $n -Force -ErrorAction SilentlyContinue)) {
                try { Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue } catch { }
            }
        }
    }
}

# ---------------- OneDrive ----------------
function Test-OneDriveInstalled {
    foreach ($p in @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\OneDrive\OneDrive.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft OneDrive\OneDrive.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft OneDrive\OneDrive.exe'),
        (Join-Path $env:SystemRoot 'SysWOW64\OneDriveSetup.exe'),
        (Join-Path $env:SystemRoot 'System32\OneDriveSetup.exe'))) {
        if ($p -and (Test-Path -LiteralPath $p)) { return $true }
    }
    return $false
}

function Remove-OneDriveFull {
    Log 'Removing OneDrive completely...' 'INFO' 'OneDrive'
    Stop-ProcByName 'OneDrive', 'OneDriveSetup', 'FileCoAuth', 'FileSyncHelper'
    # keep it from coming back
    Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive' 'DisableFileSyncNGSC' 1
    # 1) every uninstaller that exists on this PC
    $setups = @()
    foreach ($p in @((Join-Path $env:SystemRoot 'SysWOW64\OneDriveSetup.exe'), (Join-Path $env:SystemRoot 'System32\OneDriveSetup.exe'))) { if (Test-Path -LiteralPath $p) { $setups += $p } }
    foreach ($base in @((Join-Path $env:LOCALAPPDATA 'Microsoft\OneDrive'), (Join-Path $env:ProgramFiles 'Microsoft OneDrive'), (Join-Path ${env:ProgramFiles(x86)} 'Microsoft OneDrive'))) {
        if (Test-Path -LiteralPath $base) { $setups += @(Get-ChildItem -LiteralPath $base -Filter 'OneDriveSetup.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName) }
    }
    foreach ($e in ($setups | Select-Object -Unique)) {
        Log ('  running ' + $e + ' /uninstall') 'INFO' 'OneDrive'
        [void](Invoke-External $e '/uninstall')
        [void](Invoke-External $e '/uninstall /allusers')
    }
    # 2) winget as a second route (per-machine / Store builds)
    $wg = Get-Winget
    if ($wg) { [void](Invoke-External $wg 'uninstall --id Microsoft.OneDrive -e --silent --disable-interactivity --accept-source-agreements') }
    Stop-ProcByName 'OneDrive', 'OneDriveSetup'
    # 3) leftovers: program files, caches, shortcuts, tasks
    foreach ($d in @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\OneDrive'), (Join-Path $env:ProgramData 'Microsoft OneDrive'),
        (Join-Path $env:SystemDrive 'OneDriveTemp'), (Join-Path $env:ProgramFiles 'Microsoft OneDrive'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft OneDrive'))) {
        if (Test-Path -LiteralPath $d) { Log ('  deleting ' + $d) 'INFO' 'OneDrive'; [void](Remove-PathForce $d) }
    }
    Remove-ShortcutsNamed 'OneDrive*.lnk'
    try {
        foreach ($t in @(Get-ScheduledTask -TaskName 'OneDrive*' -ErrorAction SilentlyContinue)) {
            Unregister-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
        }
    } catch { }
    # 4) registry: autostart, Explorer sidebar entry, per-user data
    foreach ($rk in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Run', 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run')) {
        foreach ($vn in 'OneDrive', 'OneDriveSetup') { Remove-ItemProperty -LiteralPath $rk -Name $vn -ErrorAction SilentlyContinue }
    }
    foreach ($k in @('Registry::HKEY_CLASSES_ROOT\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}', 'Registry::HKEY_CLASSES_ROOT\Wow6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}')) {
        if (Test-Path -LiteralPath $k) { Set-Reg $k 'System.IsPinnedToNameSpaceTree' 0 }
    }
    Remove-RegKeyForce 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{018D5C66-4533-4307-9B53-224DE2ED1FE6}'
    Remove-RegKeyForce 'HKCU:\Software\Microsoft\OneDrive'
    # 5) new user accounts: the Default profile starts OneDriveSetup at first logon
    $defHive = Join-Path $env:SystemDrive 'Users\Default\NTUSER.DAT'
    if (Test-Path -LiteralPath $defHive) {
        $code = Invoke-External 'reg.exe' ('load HKU\RockDefaultUser "' + $defHive + '"')
        if ($code -eq 0) {
            [void](Invoke-External 'reg.exe' 'delete "HKU\RockDefaultUser\Software\Microsoft\Windows\CurrentVersion\Run" /v OneDriveSetup /f')
            [GC]::Collect(); [GC]::WaitForPendingFinalizers()
            [void](Invoke-External 'reg.exe' 'unload HKU\RockDefaultUser')
        }
    }
    # 6) the installer itself, so Windows cannot reinstall it for new users
    foreach ($p in @((Join-Path $env:SystemRoot 'SysWOW64\OneDriveSetup.exe'), (Join-Path $env:SystemRoot 'System32\OneDriveSetup.exe'))) {
        if (Test-Path -LiteralPath $p) { Log ('  deleting ' + $p) 'INFO' 'OneDrive'; [void](Remove-PathForce $p) }
    }
    # 7) the user's OneDrive folder holds YOUR files: it is only deleted when it is empty
    $odDirs = @($env:OneDrive, $env:OneDriveConsumer, $env:OneDriveCommercial, (Join-Path $env:USERPROFILE 'OneDrive')) | Where-Object { $_ } | Select-Object -Unique
    $known = @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('MyDocuments'), [Environment]::GetFolderPath('MyPictures'))
    foreach ($d in $odDirs) {
        if (-not (Test-Path -LiteralPath $d)) { continue }
        $left = @(Get-ChildItem -LiteralPath $d -Force -ErrorAction SilentlyContinue)
        if ($left.Count -eq 0) { try { Remove-Item -LiteralPath $d -Force -ErrorAction SilentlyContinue } catch { } }
        else {
            Log ('  kept ' + $d + ' - it still holds your files (move them out, then delete the folder yourself).') 'WARN' 'OneDrive'
            foreach ($kf in $known) { if ($kf -and $kf.StartsWith($d, [StringComparison]::OrdinalIgnoreCase)) { Log ('  note: ' + $kf + ' is stored inside OneDrive - move it back to the profile (right-click folder > Properties > Location).') 'WARN' 'OneDrive' } }
        }
    }
    if (Test-OneDriveInstalled) { Log 'OneDrive is still partly present - restart Windows and run this again.' 'WARN' 'OneDrive' }
    else { Log 'OneDrive was removed completely.' 'INFO' 'OneDrive' }
}

function Restore-OneDrive {
    Remove-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive' 'DisableFileSyncNGSC'
    $wg = Ensure-Winget
    if ($wg) { [void](Invoke-External $wg 'install --id Microsoft.OneDrive -e --silent --accept-source-agreements --accept-package-agreements') }
    else { Log 'winget is not available - install OneDrive from https://www.microsoft.com/microsoft-365/onedrive/download' 'WARN' 'OneDrive' }
}

# ---------------- Microsoft Edge ----------------
$script:EdgeGuids = @('{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}', '{2CD8A007-E189-409D-A2C8-9AF4EF3C72AA}', '{0D50BFEC-CD6A-4F9A-964C-C7416E3ACB10}', '{65C35B14-6C1D-4122-AC46-7148CC9D6497}')

function Remove-EdgeFull {
    Log 'Removing Microsoft Edge completely...' 'INFO' 'Edge'
    $pf86 = ${env:ProgramFiles(x86)}; $pf = $env:ProgramFiles
    $webView = (Test-Path -LiteralPath (Join-Path $pf86 'Microsoft\EdgeWebView')) -or (Test-Path -LiteralPath (Join-Path $pf 'Microsoft\EdgeWebView'))
    if ($webView) { Log '  the WebView2 runtime is kept (many apps need it).' 'INFO' 'Edge' }
    Stop-ProcByName 'msedge', 'MicrosoftEdge', 'MicrosoftEdgeCP', 'MicrosoftEdgeSH', 'msedge_proxy', 'pwahelper', 'cookie_exporter', 'notification_helper'
    # 1) let Edge's own uninstaller do the job
    Set-Reg 'HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev' 'AllowUninstall' 1
    $setups = @()
    foreach ($base in @((Join-Path $pf86 'Microsoft\Edge\Application'), (Join-Path $pf 'Microsoft\Edge\Application'))) {
        if (Test-Path -LiteralPath $base) {
            $setups += @(Get-ChildItem -LiteralPath $base -Filter 'setup.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\Installer\\setup\.exe$' } | Sort-Object FullName -Descending | Select-Object -ExpandProperty FullName)
        }
    }
    foreach ($e in ($setups | Select-Object -First 2)) {
        Log '  running the Edge uninstaller...' 'INFO' 'Edge'
        [void](Invoke-External $e '--uninstall --system-level --verbose-logging --force-uninstall')
        if (-not (Test-EdgeInstalled)) { break }
    }
    $userSetup = @()
    $ub = Join-Path $env:LOCALAPPDATA 'Microsoft\Edge\Application'
    if (Test-Path -LiteralPath $ub) { $userSetup = @(Get-ChildItem -LiteralPath $ub -Filter 'setup.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\Installer\\setup\.exe$' } | Select-Object -First 1 -ExpandProperty FullName) }
    foreach ($e in $userSetup) { [void](Invoke-External $e '--uninstall --force-uninstall') }
    # 2) forced clean-up of everything that is left
    Stop-ProcByName 'msedge', 'MicrosoftEdge'
    $dirs = @((Join-Path $pf86 'Microsoft\Edge'), (Join-Path $pf 'Microsoft\Edge'), (Join-Path $pf86 'Microsoft\EdgeCore'), (Join-Path $pf 'Microsoft\EdgeCore'), (Join-Path $env:LOCALAPPDATA 'Microsoft\Edge'), (Join-Path $env:ProgramData 'Microsoft\Edge'))
    if (-not $webView) { $dirs += @((Join-Path $pf86 'Microsoft\EdgeUpdate'), (Join-Path $pf 'Microsoft\EdgeUpdate'), (Join-Path $env:ProgramData 'Microsoft\EdgeUpdate')) }
    foreach ($d in $dirs) { if (Test-Path -LiteralPath $d) { Log ('  deleting ' + $d) 'INFO' 'Edge'; [void](Remove-PathForce $d) } }
    # services and tasks (the updater is shared with WebView2, so it stays when WebView2 is installed)
    $svcs = @('MicrosoftEdgeElevationService')
    if (-not $webView) { $svcs += @('edgeupdate', 'edgeupdatem') }
    foreach ($sv in $svcs) {
        if (Get-Service -Name $sv -ErrorAction SilentlyContinue) { [void](Invoke-External 'sc.exe' ('stop ' + $sv)); [void](Invoke-External 'sc.exe' ('delete ' + $sv)) }
    }
    if (-not $webView) {
        try {
            foreach ($t in @(Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like 'MicrosoftEdgeUpdate*' })) {
                Unregister-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
            }
        } catch { }
    }
    # registry
    $reg = @('HKLM:\SOFTWARE\Microsoft\Edge', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Edge', 'HKCU:\SOFTWARE\Microsoft\Edge',
             'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge',
             'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe')
    foreach ($g in $script:EdgeGuids) {
        foreach ($root in @('HKLM:\SOFTWARE\Microsoft\EdgeUpdate', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate')) { $reg += ($root + '\Clients\' + $g); $reg += ($root + '\ClientState\' + $g) }
    }
    if (-not $webView) { $reg += @('HKLM:\SOFTWARE\Microsoft\EdgeUpdate', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate') }
    foreach ($k in $reg) { Remove-RegKeyForce $k }
    Remove-ShortcutsNamed 'Microsoft Edge*.lnk'
    # legacy (EdgeHTML) packages on Windows 10
    foreach ($n in 'Microsoft.MicrosoftEdge.Stable', 'Microsoft.MicrosoftEdge', 'Microsoft.MicrosoftEdgeDevToolsClient') {
        foreach ($pk in @(Get-AppxPackage -AllUsers -Name $n -ErrorAction SilentlyContinue)) { try { Remove-AppxPackage -Package $pk.PackageFullName -AllUsers -ErrorAction SilentlyContinue } catch { } }
    }
    # 3) stop Windows Update / the updater from putting Edge back
    $pol = 'HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate'
    foreach ($g in $script:EdgeGuids) { Set-Reg $pol ('Install' + $g) 0; Set-Reg $pol ('Update' + $g) 0 }
    Set-Reg $pol 'DoNotUpdateToEdgeWithChromium' 1
    if (Test-EdgeInstalled) { Log 'Edge is still partly present - restart Windows and run this again.' 'WARN' 'Edge' }
    else { Log 'Microsoft Edge was removed completely.' 'INFO' 'Edge' }
}

function Restore-Edge {
    $pol = 'HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate'
    foreach ($g in $script:EdgeGuids) { Remove-Reg $pol ('Install' + $g); Remove-Reg $pol ('Update' + $g) }
    Remove-Reg $pol 'DoNotUpdateToEdgeWithChromium'
    Remove-Reg 'HKLM:\SOFTWARE\Microsoft\EdgeUpdate' 'DoNotUpdateToEdgeWithChromium'
    Remove-Reg 'HKLM:\SOFTWARE\Microsoft\EdgeUpdateDev' 'AllowUninstall'
    $wg = Ensure-Winget
    if ($wg) { [void](Invoke-External $wg 'install --id Microsoft.Edge -e --silent --accept-source-agreements --accept-package-agreements --force') }
    else { Log 'winget is not available - download Edge from https://www.microsoft.com/edge' 'WARN' 'Edge' }
}

# ---------------- Internet Explorer ----------------
function Get-IEFeatureName {
    switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' { return 'Internet-Explorer-Optional-arm64' } 'x86' { return 'Internet-Explorer-Optional-x86' } default { return 'Internet-Explorer-Optional-amd64' } }
}

function Remove-IEFull {
    Log 'Removing Internet Explorer completely...' 'INFO' 'IE'
    Stop-ProcByName 'iexplore', 'ielowutil'
    $reboot = $false
    # optional feature (Windows 10 and some Windows 11 builds)
    foreach ($f in @((Get-IEFeatureName), 'Internet-Explorer-Optional-x86') | Select-Object -Unique) {
        Log ('  disabling feature ' + $f) 'INFO' 'IE'
        $code = Invoke-External 'dism.exe' ('/Online /Disable-Feature /FeatureName:' + $f + ' /NoRestart /Quiet')
        if ($code -eq 3010) { $reboot = $true }
    }
    # Windows capability (Windows 10 1903+ / Windows 11)
    Log '  removing the Internet Explorer capability' 'INFO' 'IE'
    $code = Invoke-External 'dism.exe' '/Online /Remove-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0 /NoRestart /Quiet'
    if ($code -eq 3010) { $reboot = $true }
    # leftovers
    foreach ($base in @((Join-Path $env:ProgramFiles 'Internet Explorer'), (Join-Path ${env:ProgramFiles(x86)} 'Internet Explorer'))) {
        $exe = Join-Path $base 'iexplore.exe'
        if (Test-Path -LiteralPath $exe) { Log ('  deleting ' + $exe) 'INFO' 'IE'; [void](Remove-PathForce $exe) }
        if ((Test-Path -LiteralPath $base) -and (@(Get-ChildItem -LiteralPath $base -Force -ErrorAction SilentlyContinue).Count -eq 0)) { [void](Remove-PathForce $base) }
    }
    Remove-ShortcutsNamed 'Internet Explorer*.lnk'
    if ($reboot) { Log 'Restart Windows to finish removing Internet Explorer.' 'WARN' 'IE' }
    else { Log 'Internet Explorer was removed (the MSHTML engine is part of Windows and stays, Edge IE-mode needs it).' 'INFO' 'IE' }
}

function Restore-IE {
    $f = Get-IEFeatureName
    [void](Invoke-External 'dism.exe' ('/Online /Enable-Feature /FeatureName:' + $f + ' /All /NoRestart /Quiet'))
    [void](Invoke-External 'dism.exe' '/Online /Add-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0 /NoRestart /Quiet')
    Log 'Internet Explorer was restored - restart Windows to finish.' 'INFO' 'IE'
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
    if ($code -eq 3010 -or $code -eq 1641 -or $code -eq -1978334967 -or $code -eq -1978334966) { return 'installed - restart required' }
    if ($code -eq -1978334963) { return 'already installed' }
    if ($code -eq -1978334964) { return 'installer was cancelled' }
    if ($code -eq -1978334969) { return 'no network connection during install' }
    if ($code -eq -1978334971) { return 'not enough disk space' }
    if ($code -eq -1978334972) { return 'a required dependency is missing' }
    if ($code -eq -1978334975 -or $code -eq -1978334959) { return 'the app is currently running - close it and try again' }
    if ($code -eq -1978334961) { return 'blocked by a policy on this PC' }
    if ($code -eq -1978334957) { return 'not supported on this system' }
    return "finished with exit code $code"
}

# names of everything Windows itself lists as installed (Apps & features): works even when winget cannot match the app to its ID
function Get-InstalledDisplayNames {
    $names = New-Object System.Collections.Generic.List[string]
    foreach ($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*') {
        try {
            foreach ($e in @(Get-ItemProperty -Path $k -ErrorAction SilentlyContinue)) {
                $dn = [string]$e.DisplayName
                if ($dn) { [void]$names.Add($dn) }
            }
        } catch { }
    }
    return @($names)
}
# $Words: names to look for (app display name, last part of the winget ID...)
# Microsoft Edge: check the real msedge.exe (the name "Edge" also matches "Edge WebView2 Runtime" / "Edge Update", which are not the browser)
function Test-EdgeInstalled {
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base -and (Test-Path -LiteralPath (Join-Path $base 'Microsoft\Edge\Application\msedge.exe'))) { return $true }
    }
    return $false
}
function Test-NameInList {
    param([string[]]$List, [string[]]$Words)
    foreach ($w in $Words) {
        $w = ([string]$w).Trim()
        if ($w.Length -lt 3) { continue }
        if ($w -ieq 'Edge') { if (Test-EdgeInstalled) { return $true } else { continue } }
        # whole word only, so "Edge" / "Git" do not match "Edge WebView2" / "GitHub ..."
        $rx = '(?<![A-Za-z0-9])' + [regex]::Escape($w) + '(?![A-Za-z0-9])'
        foreach ($dn in $List) {
            if ($dn -match $rx) {
                if ($dn -match 'WebView' -and $w -notmatch 'WebView') { continue }
                return $true
            }
        }
    }
    return $false
}

# true only if winget itself can see the package in the installed list (exit code 0)
function Test-WingetInstalled {
    param([string]$Wg, [string]$Id)
    Start-Sleep -Milliseconds 1500
    $c = Invoke-External $Wg "list --id $Id -e --accept-source-agreements"
    if ($c -eq 0) { return $true }
    # winget sometimes cannot match an installed app to its ID - fall back to what Windows lists as installed
    return (Test-IdInstalledByName $Id)
}

# looser name check: "Microsoft.VisualStudioCode" must match the entry "Microsoft Visual Studio Code" in Apps & features
function Test-IdInstalledByName {
    param([string]$Id)
    $names = Get-InstalledDisplayNames
    $last = ($Id -split '\.')[-1]
    $cands = @($last, ($last -creplace '([a-z0-9])([A-Z])', '$1 $2'), ($Id -replace '\.', ' '))
    if (Test-NameInList $names $cands) { return $true }
    foreach ($dn in $names) {
        $n = ($dn -replace '[^A-Za-z0-9]', '').ToLower()
        foreach ($c in $cands) {
            $cn = ([string]$c -replace '[^A-Za-z0-9]', '').ToLower()
            if ($cn.Length -ge 4 -and $n.Contains($cn)) { return $true }
        }
    }
    return $false
}

# official installers for apps whose winget / Chocolatey package often fails (used only as the very last fallback)
$script:DirectInstallers = @{
    'google.chrome'   = @{ Url = 'https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi' }
    'mozilla.firefox' = @{ Url = 'https://download.mozilla.org/?product=firefox-msi-latest-ssl&os=win64&lang=en-US' }
}
function Install-Direct {
    param([string]$Id)
    $d = $script:DirectInstallers[$Id.Trim().ToLower()]
    if (-not $d) { return $false }
    $file = Join-Path $env:TEMP ('rock_direct_' + ($Id -replace '[^A-Za-z0-9]', '') + '.msi')
    try {
        try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }
        Log ('  downloading the official installer for ' + $Id + '...') 'INFO' 'Install'
        Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
        Get-RemoteFile $d.Url $file
        if (-not (Test-Path -LiteralPath $file) -or (Get-Item -LiteralPath $file).Length -lt 1MB) { throw 'the downloaded file is too small' }
        $c = Invoke-PkgWindow ('Installing ' + $Id + ' (official installer)') 'msiexec.exe' ('/i "' + $file + '" /qn /norestart')
        if ($c -eq 0 -or $c -eq 3010 -or $c -eq 1641) { Log '  installed with the official installer' 'INFO' 'Install'; return $true }
        Log ('  official installer finished with exit code ' + $c) 'WARN' 'Install'
        return $false
    } catch {
        if ($_.Exception.Message -match 'Cancelled by user') { throw }
        Log ('  direct install failed: ' + $_.Exception.Message) 'WARN' 'Install'
        return $false
    } finally { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
}
# winget failed: Chocolatey (only for apps with a known Chocolatey name, so a wrong package is never installed), then the official installer
function Install-Fallback {
    param([string]$Id)
    try {
        $ch = Get-Choco
        if ($ch -and $script:ChocoMap.ContainsKey($Id.Trim().ToLower())) {
            $cid = ConvertTo-ChocoId $Id
            $ok = @(0, 1641, 3010)
            $c = Invoke-PkgWindow ('choco install ' + $cid + '  [fallback]') $ch ('install ' + $cid + ' -y --no-progress')
            if (-not ($ok -contains $c)) { $c = Invoke-PkgWindow ('choco install ' + $cid + '  [fallback, relaxed]') $ch ('install ' + $cid + ' -y --force --ignore-checksums --no-progress') }
            if (($ok -contains $c) -and (Test-IdInstalledByName $Id)) { Log '  installed with Chocolatey' 'INFO' 'Install'; return $true }
        }
        if (Install-Direct $Id) { return $true }
    } catch { if ($_.Exception.Message -match 'Cancelled by user') { throw } }
    return $false
}

# last few meaningful lines of a captured console output (used to explain a failure)
function Get-OutputTail {
    param([string]$File, [int]$Count = 3)
    if (-not $File -or -not (Test-Path -LiteralPath $File)) { return @() }
    $lines = @(Get-Content -LiteralPath $File -ErrorAction SilentlyContinue | ForEach-Object { ([string]$_).Trim() } |
        Where-Object { $_ -match '[A-Za-z]{4}' -and $_ -notmatch '^[-\\|/\s]+$' -and $_ -notmatch '\d\s*(KB|MB|GB)\s*/' })
    return @($lines | Select-Object -Last $Count)
}

# ---------------- size formatting helper (used by the app cart)
$script:IPInv = [System.Globalization.CultureInfo]::InvariantCulture
function Format-InstSize([double]$b) {
    if ($b -ge 1GB) { return (($b / 1GB).ToString('0.#', $script:IPInv) + 'GB') }
    if ($b -ge 1MB) { return (($b / 1MB).ToString('0.#', $script:IPInv) + 'MB') }
    if ($b -ge 1KB) { return (($b / 1KB).ToString('0.#', $script:IPInv) + 'KB') }
    return (([Math]::Round($b)).ToString('0', $script:IPInv) + 'B')
}

function Invoke-Winget {
    param([string]$Verb, [string[]]$Ids)
    $wg = Ensure-Winget
    if (-not $wg) { Log 'winget is not available and could not be installed automatically. Install "App Installer" from the Microsoft Store, then try again.' 'WARN' 'Winget'; $script:WorkFailed++; return }
    # exit codes that mean "installer finished" (0, restart needed, already installed)
    $okCodes = @(0, 3010, 1641, -1978334967, -1978334966, -1978334963)
    $restartCodes = @(3010, 1641, -1978334967, -1978334966)
    # exit codes where a second attempt is pointless (not found, no installer, hash, network, disk, cancelled, policy...)
    $finalCodes = @(-1978335212, -1978335216, -1978335215, -1978335224, -1978334964, -1978334969, -1978334971, -1978334961, -1978334957)
    # exit codes where Chocolatey / the direct installer would fail the same way (no internet, no disk space, cancelled, blocked by policy)
    $noFallback = @(-1978335224, -1978334964, -1978334969, -1978334971, -1978334961)
    $n = $Ids.Count; $i = 0
    foreach ($id in $Ids) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $script:SegStart = 100.0 * $i / $n
        $script:SegEnd   = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ($Verb + ': ' + $id + '  (' + ($i + 1) + '/' + $n + ')')
        Log "winget $Verb : $id  ($($i + 1)/$n)" 'INFO' 'Winget'
        $base = "$Verb --id $id -e --accept-source-agreements"
        if ($Verb -eq 'install' -or $Verb -eq 'upgrade') { $base += ' --accept-package-agreements' }
        $ttl = 'winget ' + $Verb + ' ' + $id + '  (' + ($i + 1) + '/' + $n + ')'
        $code = Invoke-PkgWindow $ttl $wg ($base + ' --silent')
        if ($Verb -eq 'install') {
            # winget can finish "successfully" without the app really being installed, so always check afterwards
            $done = $false
            if ($restartCodes -contains $code) { $done = $true }
            elseif ($okCodes -contains $code -or $code -eq -1978335189) { $done = Test-WingetInstalled $wg $id }
            if (-not $done -and -not ($finalCodes -contains $code)) {
                Log ('  not installed yet (' + (Get-WingetMessage $code) + ') - retrying with the installer window...') 'WARN' 'Winget'
                $code = Invoke-PkgWindow ($ttl + '  [retry]') $wg ($base + ' --interactive')
                if ($restartCodes -contains $code) { $done = $true }
                elseif ($finalCodes -contains $code) { $done = $false }
                else { $done = Test-WingetInstalled $wg $id }
            }
            # exit code 0 but the app is not visible yet: some installers keep working in the background - wait and look again
            if (-not $done -and $code -eq 0) { Start-Sleep -Seconds 5; $done = Test-WingetInstalled $wg $id }
            # the vendor changed the installer file after the manifest was written (very common with Chrome): allow it and retry once
            if (-not $done -and $code -eq -1978335215) {
                Log '  hash mismatch (the vendor updated the installer) - retrying with the hash check relaxed...' 'WARN' 'Winget'
                try { [void](Invoke-External $wg 'settings --enable InstallerHashOverride') } catch { }
                $code = Invoke-PkgWindow ($ttl + '  [ignore hash]') $wg ($base + ' --silent --ignore-security-hash')
                if ($restartCodes -contains $code) { $done = $true }
                elseif ($okCodes -contains $code) { $done = Test-WingetInstalled $wg $id }
            }
            # last resorts: Chocolatey, then the vendor's own installer
            if (-not $done -and -not ($noFallback -contains $code)) {
                Log '  winget could not finish this install - trying other ways...' 'WARN' 'Winget'
                if (Install-Fallback $id) { $done = $true; $code = 0 }
            }
            if ($done) {
                if ($code -eq 0) { Log '  installed OK' 'INFO' 'Winget' } else { Log ('  ' + (Get-WingetMessage $code)) 'INFO' 'Winget' }
            } else {
                $script:WorkFailed++
                Log ('  FAILED - ' + $id + ' was NOT installed (' + (Get-WingetMessage $code) + ')') 'ERROR' 'Winget'
            }
        } else {
            $msg = Get-WingetMessage $code
            if ($okCodes -contains $code -or $code -eq -1978335189 -or ($Verb -eq 'uninstall' -and $code -eq -1978335212)) { Log ('  ' + $msg) 'INFO' 'Winget' }
            else { $script:WorkFailed++; Log ('  ' + $msg) 'WARN' 'Winget' }
        }
        $i++
    }
    $script:SegStart = -1.0
}

# ---- Chocolatey support (Install tab > Package Manager switch)
function Get-Choco {
    $c = Get-Command choco.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $paths = @()
    if ($env:ChocolateyInstall) { $paths += (Join-Path $env:ChocolateyInstall 'bin\choco.exe') }
    $paths += (Join-Path $env:ProgramData 'chocolatey\bin\choco.exe')
    foreach ($p in $paths) { if (Test-Path -LiteralPath $p) { return $p } }
    return $null
}
function Test-PkgChoco { return ([string]$script:Settings['PkgManager'] -eq 'Chocolatey') }
# winget ID (lower case) -> Chocolatey package ID, for the apps whose Chocolatey name is not simply the last part of the winget ID
$script:ChocoMap = @{
    'google.chrome' = 'googlechrome'
    'microsoft.edge' = 'microsoft-edge'
    'mozilla.firefox' = 'firefox'
    'mozilla.firefox.esr' = 'firefoxesr'
    'torproject.torbrowser' = 'tor-browser'
    'telegram.telegramdesktop' = 'telegram'
    'openwhispersystems.signal' = 'signal'
    'microsoft.visualstudiocode' = 'vscode'
    'microsoft.visualstudiocode.insiders' = 'vscode-insiders'
    'github.githubdesktop' = 'github-desktop'
    'python.python.3.12' = 'python312'
    'python.python.3.13' = 'python313'
    'python.python.3.14' = 'python314'
    'openjs.nodejs.lts' = 'nodejs-lts'
    'openjs.nodejs' = 'nodejs'
    'golang.go' = 'golang'
    'rustlang.rustup' = 'rustup.install'
    'eclipseadoptium.temurin.21.jdk' = 'temurin21'
    'eclipseadoptium.temurin.17.jdk' = 'temurin17'
    'microsoft.dotnet.sdk.8' = 'dotnet-8.0-sdk'
    'microsoft.dotnet.sdk.9' = 'dotnet-9.0-sdk'
    'microsoft.dotnet.desktopruntime.6' = 'dotnet-6.0-desktopruntime'
    'microsoft.dotnet.desktopruntime.8' = 'dotnet-8.0-desktopruntime'
    'microsoft.dotnet.desktopruntime.9' = 'dotnet-9.0-desktopruntime'
    'docker.dockerdesktop' = 'docker-desktop'
    'notepad++.notepad++' = 'notepadplusplus'
    'sublimehq.sublimetext.4' = 'sublimetext4'
    'jetbrains.pycharm.community' = 'pycharm-community'
    'jetbrains.intellijidea.community' = 'intellijidea-community'
    'jetbrains.toolbox' = 'jetbrainstoolbox'
    'microsoft.windowsterminal' = 'microsoft-windows-terminal'
    'microsoft.powershell' = 'powershell-core'
    'adobe.acrobat.reader.64-bit' = 'adobereader'
    'foxit.foxitreader' = 'foxitreader'
    'thedocumentfoundation.libreoffice' = 'libreoffice-fresh'
    'digitalscholar.zotero' = 'zotero'
    'epicgames.epicgameslauncher' = 'epicgameslauncher'
    'gog.galaxy' = 'goggalaxy'
    'ubisoft.connect' = 'ubisoft-connect'
    'electronicarts.eadesktop' = 'ea-app'
    'microsoft.sysinternals.processexplorer' = 'procexp'
    'microsoft.sysinternals.processmonitor' = 'procmon'
    'microsoft.sysinternals.autoruns' = 'autoruns'
    'microsoft.sysinternals.tcpview' = 'tcpview'
    'microsoft.sysinternals.rdcman' = 'rdcman'
    'microsoft.vcredist.2015+.x64' = 'vcredist140'
    'microsoft.vcredist.2015+.x86' = 'vcredist140'
    'microsoft.vcredist.2013.x64' = 'vcredist2013'
    'microsoft.vcredist.2012.x64' = 'vcredist2012'
    'microsoft.vcredist.2010.x64' = 'vcredist2010'
    'microsoft.directx' = 'directx'
    'microsoft.visualstudio.2022.community' = 'visualstudio2022community'
    'videolan.vlc' = 'vlc'
    'clsid2.mpc-hc' = 'mpc-hc-clsid2'
    'peterpawlowski.foobar2000' = 'foobar2000'
    'obsproject.obsstudio' = 'obs-studio'
    'gyan.ffmpeg' = 'ffmpeg'
    'kde.krita' = 'krita'
    'dotpdn.paintdotnet' = 'paint.net'
    'blenderfoundation.blender' = 'blender'
    'kde.kdenlive' = 'kdenlive'
    'meltytech.shotcut' = 'shotcut'
    'duongdieuphap.imageglass' = 'imageglass'
    'cpuid.cpu-z' = 'cpu-z'
    'techpowerup.gpu-z' = 'gpu-z'
    'realix.hwinfo' = 'hwinfo'
    'cpuid.hwmonitor' = 'hwmonitor'
    'crystaldewworld.crystaldiskinfo' = 'crystaldiskinfo'
    'crystaldewworld.crystaldiskmark' = 'crystaldiskmark'
    'guru3d.afterburner' = 'msiafterburner'
    'wagnardsoft.displaydriveruninstaller' = 'ddu'
    'balena.etcher' = 'etcher'
    'wiresharkfoundation.wireshark' = 'wireshark'
    'insecure.nmap' = 'nmap'
    'winsiderss.systeminformer' = 'systeminformer'
    'revouninstaller.revouninstaller' = 'revo-uninstaller'
    'm2team.nanazip' = 'nanazip'
    'rarlab.winrar' = 'winrar'
    'voidtools.everything' = 'everything'
    'keepassxcteam.keepassxc' = 'keepassxc'
    'rocksdanister.livelywallpaper' = 'lively'
    'charlesmilette.translucenttb' = 'translucenttb'
    'ramensoftware.windhawk' = 'windhawk'
    'valinet.explorerpatcher' = 'explorerpatcher'
    'antibodysoftware.wiztree' = 'wiztree'
    'flux.flux' = 'f.lux'
    'hibbiki.chromium' = 'chromium'
    'eloston.ungoogled-chromium' = 'ungoogled-chromium'
    'microsoft.skype' = 'skype'
    'vscodium.vscodium' = 'vscodium'
    'google.androidstudio' = 'androidstudio'
    'kitware.cmake' = 'cmake'
    'llvm.llvm' = 'llvm'
    'github.cli' = 'gh'
    'oracle.mysqlworkbench' = 'mysql.workbench'
    'postgresql.pgadmin' = 'pgadmin4'
    'mongodb.compass.full' = 'mongodb-compass'
    'timkosse.filezilla.client' = 'filezilla'
    'denoland.deno' = 'deno'
    'jandedobbeleer.ohmyposh' = 'oh-my-posh'
    'unity.unityhub' = 'unity-hub'
    'godotengine.godotengine' = 'godot'
    'arduinosa.ide.stable' = 'arduino'
    'raspberrypifoundation.raspberrypiimager' = 'rpi-imager'
    'anaconda.anaconda3' = 'anaconda3'
    'anaconda.miniconda3' = 'miniconda3'
    'rproject.r' = 'r.project'
    'posit.rstudio' = 'r.studio'
    'onlyoffice.desktopeditors' = 'onlyoffice'
    'kingsoft.wpsoffice' = 'wps-office-free'
    'jgraph.draw' = 'drawio'
    'johnmacfarlane.pandoc' = 'pandoc'
    'blizzard.battlenet' = 'battle.net'
    'itchio.itch' = 'itch'
    'mojang.minecraftlauncher' = 'minecraft-launcher'
    'libretro.retroarch' = 'retroarch'
    'dolphinemulator.dolphin' = 'dolphin'
    'pcsx2team.pcsx2' = 'pcsx2'
    'microsoft.azurecli' = 'azure-cli'
    'microsoft.edgewebview2runtime' = 'webview2-runtime'
    'microsoft.sqlservermanagementstudio' = 'sql-server-management-studio'
    'microsoft.azuredatastudio' = 'azure-data-studio'
    'microsoft.powerbi' = 'powerbi'
    'codecguide.k-litecodecpack.mega' = 'k-litecodecpackmega'
    'codecguide.k-litecodecpack.standard' = 'k-litecodecpackstandard'
    'daum.potplayer' = 'potplayer'
    'xbmcfoundation.kodi' = 'kodi'
    'apple.itunes' = 'itunes'
    'cockos.reaper' = 'reaper'
    'nickemanarin.screentogif' = 'screentogif'
    'moritzbunkus.mkvtoolnix' = 'mkvtoolnix'
    'blackmagicdesign.davinciresolve' = 'davinci-resolve'
    'kde.digikam' = 'digikam'
    'xnsoft.xnviewmp' = 'xnviewmp'
    'irfanskiljan.irfanview' = 'irfanview'
    'mediaarea.mediainfo.gui' = 'mediainfo'
    'florianheidenreich.mp3tag' = 'mp3tag'
    'file-new-project.eartrumpet' = 'eartrumpet'
    'vb-audio.voicemeeter' = 'voicemeeter'
    'ocbase.occt.personal' = 'occt'
    'geeks3d.furmark' = 'furmark'
    'alcpu.coretemp' = 'coretemp'
    'piriform.speccy' = 'speccy'
    'piriform.ccleaner' = 'ccleaner'
    'jamsoftware.treesize.free' = 'treesizefree'
    'bitsum.processlasso' = 'plasso'
    'guru3d.rtss' = 'rtss'
    'techpowerup.nvcleanstall' = 'nvcleanstall'
    'techpowerup.throttlestop' = 'throttlestop'
    'famatech.advancedipscanner' = 'advanced-ip-scanner'
    'angryziber.angryipscanner' = 'angryip'
    'telerik.fiddler.classic' = 'fiddler'
    'x64dbg.x64dbg' = 'x64dbg.portable'
    'mhnexus.hxd' = 'hxd'
    'jellyfin.server' = 'jellyfin'
    'zerotier.zerotierone' = 'zerotier-one'
    'cloudflare.warp' = 'warp'
    'openvpntechnologies.openvpn' = 'openvpn'
    'google.googledrive' = 'google-drive-file-stream'
    'mega.megasync' = 'megasync'
    'pcloudag.pclouddrive' = 'pcloud'
    'box.box' = 'box-drive'
    'klocman.bulkcrapuninstaller' = 'bulk-crap-uninstaller'
    'sandboxie.plus' = 'sandboxie-plus'
    'ql-win.quicklook' = 'quicklook'
    'ghisler.totalcommander' = 'totalcommander'
    'alexanderkoblov.doublecommander' = 'doublecmd'
    'open-shell.open-shell-menu' = 'open-shell'
    'kde.kdeconnect' = 'kdeconnect'
    'genymobile.scrcpy' = 'scrcpy'
    'google.platformtools' = 'adb'
    'powersoftware.poweriso' = 'poweriso'
    'bandisoft.bandizip' = 'bandizip'
    'giorgiotani.peazip' = 'peazip'
    'xanderfrangos.twinkletray' = 'twinkle-tray'
    'winaero.winaerotweaker' = 'winaero-tweaker'
    'startisback.startallback' = 'startallback'
    'proton.protonvpn' = 'protonvpn'
    'nordsecurity.nordvpn' = 'nordvpn'
    'mullvadvpn.mullvadvpn' = 'mullvad-app'
    'idrix.veracrypt' = 'veracrypt'
    'dominikreichl.keepass' = 'keepass'
    'securemix.glasswire' = 'glasswire'
    'portswigger.burpsuite.community' = 'burp-suite-free-edition'
    'gnupg.gpg4win' = 'gpg4win'
    'nvidia.geforceexperience' = 'geforce-experience'
    'logitech.ghub' = 'lghub'
    'elgato.streamdeck' = 'streamdeck'
    'akeo.ie.zadig' = 'zadig'
    'hashicorp.vagrant' = 'vagrant'
    'schniz.fnm' = 'fnm'
    'jesseduffield.lazygit' = 'lazygit'
    'hugo.hugo.extended' = 'hugo-extended'
    'oracle.virtualbox' = 'virtualbox'
    'microsoft.onedrive' = 'onedrive'
    'microsoft.nuget' = 'nuget.commandline'
    'gerardog.gsudo' = 'gsudo'
    'henry++.simplewall' = 'simplewall'
    'agilebits.1password' = '1password'
    'tonec.internetdownloadmanager' = 'internet-download-manager'
    'marticliment.unigetui' = 'wingetui'
    'glavsoft.tightvnc' = 'tightvnc'
    'microsoft.wsl' = 'wsl2'
    'canonical.ubuntu.2204' = 'wsl-ubuntu-2204'
    'apachefriends.xampp.8.2' = 'xampp-82'
    'microsoft.office' = 'office365business'
    'rubyinstallerteam.rubywithdevkit.3.4' = 'ruby'
    'rubyinstallerteam.rubywithdevkit.3.2' = 'ruby'
    'amazon.corretto.21.jdk' = 'corretto21jdk'
    'amazon.corretto.8.jdk' = 'corretto8jdk'
    'rizonesoft.notepad3' = 'notepad3'
    'mumble.mumble.client' = 'mumble'
    'cisco.webex' = 'webex'
    'jitsi.meet' = 'jitsi-meet'
    'element.element' = 'element-desktop'
    'teamspeaksystems.teamspeakclient' = 'teamspeak'
    'plex.plexmediaserver' = 'plexmediaserver'
    'lizardbyte.sunshine' = 'sunshine'
    'nextcloud.nextclouddesktop' = 'nextcloud-client'
    'tailscale.tailscale' = 'tailscale'
}
$script:ChocoValues = @{}
foreach ($v in $script:ChocoMap.Values) { $script:ChocoValues[[string]$v] = $true }
# winget IDs look like Vendor.App while choco IDs are the bare app name (Valve.Steam -> steam)
function ConvertTo-ChocoId {
    param([string]$Id)
    $key = $Id.Trim().ToLower()
    if ($script:ChocoMap.ContainsKey($key)) { return [string]$script:ChocoMap[$key] }
    if ($script:ChocoValues.ContainsKey($key)) { return $key }   # already a Chocolatey ID
    if ($key -like 'python.python.*') { return 'python' }
    $s = $Id
    if ($s -match '\.') { $s = ($s -split '\.')[-1] }
    return ($s -replace '[^A-Za-z0-9._-]', '').ToLower()
}
function Invoke-Choco {
    param([string]$Verb, [string[]]$Ids, [switch]$Native)
    $ch = Ensure-Choco
    if (-not $ch) { Log 'Chocolatey (choco) is not available and could not be installed automatically. Install it from chocolatey.org, or switch back to WinGet.' 'WARN' 'Pkg'; return }
    $n = $Ids.Count; $i = 0
    foreach ($id in $Ids) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $script:SegStart = 100.0 * $i / $n
        $script:SegEnd   = 100.0 * ($i + 1) / $n
        if ($Native) { $cid = $id.Trim().ToLower() } else { $cid = ConvertTo-ChocoId $id }
        Set-Progress $script:SegStart ($Verb + ': ' + $cid + '  (' + ($i + 1) + '/' + $n + ')')
        Log "choco $Verb : $cid  ($($i + 1)/$n)" 'INFO' 'Pkg'
        $code = Invoke-PkgWindow ('choco ' + $Verb + ' ' + $cid + '  (' + ($i + 1) + '/' + $n + ')') $ch "$Verb $cid -y"
        $okc = ($code -eq 0 -or $code -eq 1641 -or $code -eq 3010)
        if ($okc -and $Verb -eq 'install') {
            # make sure the package really landed in the chocolatey lib folder
            $libDir = Join-Path (Split-Path -Parent (Split-Path -Parent $ch)) ('lib\' + $cid)
            if (-not (Test-Path -LiteralPath $libDir)) { $libDir = Join-Path $env:ProgramData ('chocolatey\lib\' + $cid) }
            if (-not (Test-Path -LiteralPath $libDir)) { $okc = $false; $code = 'not found after install' }
        }
        if (-not $okc -and $Verb -eq 'install') {
            Log '  choco could not finish this install - retrying with checksums relaxed...' 'WARN' 'Pkg'
            $c2 = Invoke-PkgWindow ('choco install ' + $cid + '  [retry]') $ch ('install ' + $cid + ' -y --force --ignore-checksums')
            if ($c2 -eq 0 -or $c2 -eq 1641 -or $c2 -eq 3010) { $okc = $true; $code = $c2 }
            if (-not $okc -and -not $Native) {
                $wgf = Get-Winget
                if ($wgf) {
                    Log '  trying winget instead...' 'WARN' 'Pkg'
                    $c3 = Invoke-PkgWindow ('winget install ' + $id + '  [fallback]') $wgf ('install --id ' + $id + ' -e --accept-source-agreements --accept-package-agreements --silent')
                    if (@(0, 3010, 1641, -1978334967, -1978334966, -1978334963) -contains $c3) { $okc = $true; $code = $c3 }
                }
            }
            if (-not $okc -and -not $Native -and (Install-Direct $id)) { $okc = $true; $code = 0 }
        }
        if ($okc) { Log '  OK' 'INFO' 'Pkg' }
        else { $script:WorkFailed++; Log ("  FAILED - $cid was not installed / finished with: $code") 'ERROR' 'Pkg' }
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

# ---- Tweaks that use scriptblocks (services, DISM, restore point, Edge / OneDrive / IE removal ...) run in a BACKGROUND runspace.
# They used to run on the window's own thread: anything slower than a few seconds froze the UI (white window, "Not responding", crash).
$script:TweakBgHelpers = @('Set-Reg', 'Remove-Reg', 'Get-Winget', 'Stop-ProcByName', 'Remove-PathForce', 'Remove-RegKeyForce', 'Remove-ShortcutsNamed',
    'Test-OneDriveInstalled', 'Remove-OneDriveFull', 'Restore-OneDrive', 'Test-EdgeInstalled', 'Remove-EdgeFull', 'Restore-Edge',
    'Get-IEFeatureName', 'Remove-IEFull', 'Restore-IE')
$script:TweakBgTail = @'
function Log {
    param([string]$msg, [string]$level = 'INFO', [string]$tag = 'UI')
    $pre = ''
    if ($level -eq 'ERROR' -or $level -eq 'WARN') { $pre = $level + ': ' }
    [void]$global:RockSt.Lines.Add($pre + $msg)
}
function Invoke-External {
    param([string]$File, [string]$ArgString, [string]$OutFile)
    $sp = @{ FilePath = $File; WindowStyle = 'Hidden'; PassThru = $true }
    if ($ArgString) { $sp['ArgumentList'] = $ArgString }
    if ($OutFile) { $sp['RedirectStandardOutput'] = $OutFile }
    $p = Start-Process @sp
    $null = $p.Handle
    if (-not $p.WaitForExit(1800000)) { try { $p.Kill() } catch { } ; return -1 }
    return $p.ExitCode
}
function Ensure-Winget { return (Get-Winget) }
& ([scriptblock]::Create($global:RockCode))
'@
$script:TweakBgText = $null
function Get-TweakBgScript {
    if ($script:TweakBgText) { return $script:TweakBgText }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('param($st, $code)')
    [void]$sb.AppendLine('$ErrorActionPreference = ''Continue''')
    [void]$sb.AppendLine('$global:RockSt = $st; $global:RockCode = $code')
    $guids = @($script:EdgeGuids | ForEach-Object { "'" + $_ + "'" }) -join ', '
    [void]$sb.AppendLine('$script:EdgeGuids = @(' + $guids + ')')
    foreach ($n in $script:TweakBgHelpers) {
        $c = Get-Command $n -CommandType Function -ErrorAction SilentlyContinue
        if ($c) { [void]$sb.AppendLine('function ' + $n + ' {'); [void]$sb.AppendLine([string]$c.Definition); [void]$sb.AppendLine('}') }
    }
    [void]$sb.AppendLine($script:TweakBgTail)
    $script:TweakBgText = $sb.ToString()
    return $script:TweakBgText
}

function Invoke-TweakBg {
    param([string]$Code)
    $st = New-WUState
    $ps = [powershell]::Create()
    [void]$ps.AddScript((Get-TweakBgScript)).AddArgument($st).AddArgument($Code)
    $async = $ps.BeginInvoke()
    $printed = 0
    $t0 = Get-Date
    $last = [DateTime]::MinValue
    while (-not $async.IsCompleted) {
        if ($script:CancelRequested) {
            try { [void]$ps.BeginStop($null, $null) } catch { }
            throw 'Cancelled by user'
        }
        if (([DateTime]::Now - $last).TotalMilliseconds -ge 200) {
            $last = [DateTime]::Now
            while ($printed -lt $st.Lines.Count) { Log ([string]$st.Lines[$printed]) 'INFO' 'Tweaks'; $printed++ }
            if ($script:SegStart -ge 0) {
                $el = ((Get-Date) - $t0).TotalSeconds
                $frac = 1 - [Math]::Exp(-$el / 25.0)
                Set-Progress ($script:SegStart + ($script:SegEnd - $script:SegStart) * $frac * 0.92)
            }
        }
        DoEvents
        Start-Sleep -Milliseconds 16
    }
    while ($printed -lt $st.Lines.Count) { Log ([string]$st.Lines[$printed]) 'INFO' 'Tweaks'; $printed++ }
    try {
        try { [void]$ps.EndInvoke($async) }
        catch {
            $e = $_.Exception
            while ($e.InnerException) { $e = $e.InnerException }
            throw $e.Message
        }
        # non-terminating errors only count as warnings (same as before, when they just printed to the console)
        $k = 0
        foreach ($er in @($ps.Streams.Error)) { if ($k -ge 5) { break }; Log ('  warning: ' + [string]$er) 'WARN' 'Tweaks'; $k++ }
    } finally { try { $ps.Dispose() } catch { } }
}

function Invoke-TweakSafe {
    param($T, [string]$Mode)
    if ($T.Ops) { Invoke-TweakAction $T $Mode; return }
    $blk = $null
    if ($Mode -eq 'Apply') { $blk = $T.Apply } else { $blk = $T.Undo }
    if (-not $blk) { return }
    Invoke-TweakBg ([string]$blk.ToString())
}


