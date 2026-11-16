<<<<<<< HEAD
﻿# ==============================================================================
# MODULE: src\console\Console.ps1
# ==============================================================================
# Module: console - banner, native helpers, console / taskbar icon (dot-sourced by RockToolbox.ps1, runs before the UI starts)

# ------------------------------------------------------------------
# Console banner
# ------------------------------------------------------------------
function Show-Banner {
    if ($NoConsole) { return }
    $art = @'
 ____    ___    ____  _  __
|  _ \  / _ \  / ___|| |/ /
| |_) || | | || |    | ' / 
|  _ < | |_| || |___ | . \ 
|_| \_\ \___/  \____||_|\_\
'@
    $lines  = $art -split "`r?`n"
    $colors = 'Cyan', 'Cyan', 'DarkCyan', 'Magenta', 'DarkMagenta'
    Write-Host ''
    for ($i = 0; $i -lt $lines.Count; $i++) {
        Write-Host ('   ' + $lines[$i]) -ForegroundColor $colors[$i]
    }
    Write-Host ''
    Write-Host '   ====== Rock Tech ======' -ForegroundColor White
    Write-Host '   ===== Windows Toolbox =====' -ForegroundColor White
    try { $os = (Get-CimInstance Win32_OperatingSystem).Caption } catch { $os = 'Windows' }
    Write-Host ("   {0}  |  PowerShell {1}  |  Administrator" -f $os, $PSVersionTable.PSVersion) -ForegroundColor DarkGray
    Write-Host ''
}
Show-Banner

# ---- Add-Type with an on-disk cache: the C# compiler runs only the first time, later launches just load the saved DLL
function Add-Type {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)][string]$TypeDefinition,
        [string[]]$ReferencedAssemblies, [string]$Path, [string[]]$AssemblyName, [string]$MemberDefinition,
        [string]$Name, [string]$Namespace, [string[]]$UsingNamespace, [string]$Language,
        [string[]]$CompilerOptions, [switch]$PassThru, [switch]$IgnoreWarnings
    )
    if ($TypeDefinition -and -not $Path -and -not $MemberDefinition -and -not $AssemblyName -and -not $PassThru) {
        try {
            $dir = Join-Path $env:LOCALAPPDATA 'RockToolbox\TypeCache'
            $key = $TypeDefinition + '|' + (($ReferencedAssemblies | Sort-Object) -join ',') + '|' + $PSVersionTable.PSVersion.ToString() + '|' + [string]$Language
            $sha = [System.Security.Cryptography.SHA1]::Create()
            $hash = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key)) | ForEach-Object { $_.ToString('x2') })
            $dll = Join-Path $dir ($hash + '.dll')
            if (-not (Test-Path -LiteralPath $dll)) {
                New-Item -ItemType Directory -Force -Path $dir | Out-Null
                $tmp = $dll + '.' + $PID + '.tmp'
                $args2 = @{ TypeDefinition = $TypeDefinition; OutputAssembly = $tmp; OutputType = 'Library'; ErrorAction = 'Stop' }
                if ($ReferencedAssemblies) { $args2.ReferencedAssemblies = $ReferencedAssemblies }
                if ($Language) { $args2.Language = $Language }
                Microsoft.PowerShell.Utility\Add-Type @args2
                Move-Item -LiteralPath $tmp -Destination $dll -Force
            }
            $first = [regex]::Match($TypeDefinition, '\bpublic\s+(?:static\s+|sealed\s+|abstract\s+)*(?:class|struct|enum|interface)\s+(\w+)')
            if (-not ($first.Success -and ($first.Groups[1].Value -as [type]))) {
                Microsoft.PowerShell.Utility\Add-Type -Path $dll -ErrorAction Stop
            }
            return
        } catch { }
    }
    Microsoft.PowerShell.Utility\Add-Type @PSBoundParameters
}

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

# Precompiled helper types (RockNative.dll, built at release time): loading it once replaces
# several slow Add-Type C# compilations. If the DLL is missing we compile inline instead.
try {
    $rockNative = Join-Path $script:RockRoot 'RockNative.dll'
    if (Test-Path -LiteralPath $rockNative) { Add-Type -Path $rockNative }
} catch { }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

$AppDir = Join-Path $env:LOCALAPPDATA 'RockToolbox'
$WpDir  = Join-Path $AppDir 'Wallpapers'
New-Item -ItemType Directory -Force -Path $WpDir | Out-Null

if (-not ('WinToolboxNative' -as [type])) { Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WinToolboxNative {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@ }

# ------------------------------------------------------------------
# Rock logo as a real icon: console window + taskbar (replaces the blue PowerShell icon)
# ------------------------------------------------------------------
function New-RockBitmap {
    param([int]$Size)
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $k = $Size / 40.0
    $facets = @(
        @('#8FEBFF', 20, 2, 4, 12, 20, 20),
        @('#4FD1FF', 20, 2, 36, 12, 20, 20),
        @('#2F7BFF', 4, 12, 4, 28, 20, 20),
        @('#7B5CFF', 36, 12, 36, 28, 20, 20),
        @('#4B35C9', 4, 28, 20, 38, 20, 20),
        @('#9A6BFF', 36, 28, 20, 38, 20, 20)
    )
    foreach ($f in $facets) {
        $pts = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF([single]($f[1] * $k), [single]($f[2] * $k))),
            (New-Object System.Drawing.PointF([single]($f[3] * $k), [single]($f[4] * $k))),
            (New-Object System.Drawing.PointF([single]($f[5] * $k), [single]($f[6] * $k)))
        )
        $br = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($f[0]))
        $g.FillPolygon($br, $pts)
        $br.Dispose()
    }
    $g.Dispose()
    return $bmp
}
function New-RockHIcon {
    param([int]$Size)
    $bmp = New-RockBitmap $Size
    return $bmp.GetHicon()
}
try {
    [void][WinToolboxNative]::SetCurrentProcessExplicitAppUserModelID('Rock.Toolbox.Utility')
    $Host.UI.RawUI.WindowTitle = 'Rock Toolbox'
    $hwnd = [WinToolboxNative]::GetConsoleWindow()
    if ($hwnd -ne [IntPtr]::Zero) {
        $hBig   = New-RockHIcon 64
        $hSmall = New-RockHIcon 32
        [void][WinToolboxNative]::SendMessage($hwnd, 0x80, [IntPtr]1, $hBig)
        [void][WinToolboxNative]::SendMessage($hwnd, 0x80, [IntPtr]0, $hSmall)
    }
} catch { }




# ==============================================================================
# UI files: every ui\*.xaml file is read from the install folder (ui\MainWindow.xaml + ui\Styles.xaml + ui\pages\*.xaml)
# ==============================================================================

function Get-XamlSource {
    param([string]$Relative)
    $file = Join-Path $script:RockRoot ($Relative -replace '/', '\')
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing UI file: $file" }
    $txt = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8).TrimEnd()
    $eval = [System.Text.RegularExpressions.MatchEvaluator]{ param($m) Get-XamlSource $m.Groups[1].Value }
    return [regex]::Replace($txt, '[ \t]*<!--\s*@include\s+(\S+)\s*-->', $eval)
}
=======
﻿# ==============================================================================
# MODULE: src\console\Console.ps1
# ==============================================================================
# Module: console - banner, native helpers, console / taskbar icon (dot-sourced by RockToolbox.ps1, runs before the UI starts)

# ------------------------------------------------------------------
# Console banner
# ------------------------------------------------------------------
function Show-Banner {
    if ($NoConsole) { return }
    $art = @'
 ____    ___    ____  _  __
|  _ \  / _ \  / ___|| |/ /
| |_) || | | || |    | ' / 
|  _ < | |_| || |___ | . \ 
|_| \_\ \___/  \____||_|\_\
'@
    $lines  = $art -split "`r?`n"
    $colors = 'Cyan', 'Cyan', 'DarkCyan', 'Magenta', 'DarkMagenta'
    Write-Host ''
    for ($i = 0; $i -lt $lines.Count; $i++) {
        Write-Host ('   ' + $lines[$i]) -ForegroundColor $colors[$i]
    }
    Write-Host ''
    Write-Host '   ====== Rock Tech ======' -ForegroundColor White
    Write-Host '   ===== Windows Toolbox =====' -ForegroundColor White
    try { $os = (Get-CimInstance Win32_OperatingSystem).Caption } catch { $os = 'Windows' }
    Write-Host ("   {0}  |  PowerShell {1}  |  Administrator" -f $os, $PSVersionTable.PSVersion) -ForegroundColor DarkGray
    Write-Host ''
}
Show-Banner

# ---- Add-Type with an on-disk cache: the C# compiler runs only the first time, later launches just load the saved DLL
function Add-Type {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)][string]$TypeDefinition,
        [string[]]$ReferencedAssemblies, [string]$Path, [string[]]$AssemblyName, [string]$MemberDefinition,
        [string]$Name, [string]$Namespace, [string[]]$UsingNamespace, [string]$Language,
        [string[]]$CompilerOptions, [switch]$PassThru, [switch]$IgnoreWarnings
    )
    if ($TypeDefinition -and -not $Path -and -not $MemberDefinition -and -not $AssemblyName -and -not $PassThru) {
        try {
            $dir = Join-Path $env:LOCALAPPDATA 'RockToolbox\TypeCache'
            $key = $TypeDefinition + '|' + (($ReferencedAssemblies | Sort-Object) -join ',') + '|' + $PSVersionTable.PSVersion.ToString() + '|' + [string]$Language
            $sha = [System.Security.Cryptography.SHA1]::Create()
            $hash = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key)) | ForEach-Object { $_.ToString('x2') })
            $dll = Join-Path $dir ($hash + '.dll')
            if (-not (Test-Path -LiteralPath $dll)) {
                New-Item -ItemType Directory -Force -Path $dir | Out-Null
                $tmp = $dll + '.' + $PID + '.tmp'
                $args2 = @{ TypeDefinition = $TypeDefinition; OutputAssembly = $tmp; OutputType = 'Library'; ErrorAction = 'Stop' }
                if ($ReferencedAssemblies) { $args2.ReferencedAssemblies = $ReferencedAssemblies }
                if ($Language) { $args2.Language = $Language }
                Microsoft.PowerShell.Utility\Add-Type @args2
                Move-Item -LiteralPath $tmp -Destination $dll -Force
            }
            $first = [regex]::Match($TypeDefinition, '\bpublic\s+(?:static\s+|sealed\s+|abstract\s+)*(?:class|struct|enum|interface)\s+(\w+)')
            if (-not ($first.Success -and ($first.Groups[1].Value -as [type]))) {
                Microsoft.PowerShell.Utility\Add-Type -Path $dll -ErrorAction Stop
            }
            return
        } catch { }
    }
    Microsoft.PowerShell.Utility\Add-Type @PSBoundParameters
}

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

# Precompiled helper types (RockNative.dll, built at release time): loading it once replaces
# several slow Add-Type C# compilations. If the DLL is missing we compile inline instead.
try {
    $rockNative = Join-Path $script:RockRoot 'RockNative.dll'
    if (Test-Path -LiteralPath $rockNative) { Add-Type -Path $rockNative }
} catch { }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

$AppDir = Join-Path $env:LOCALAPPDATA 'RockToolbox'
$WpDir  = Join-Path $AppDir 'Wallpapers'
New-Item -ItemType Directory -Force -Path $WpDir | Out-Null

if (-not ('WinToolboxNative' -as [type])) { Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WinToolboxNative {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@ }

# ------------------------------------------------------------------
# Rock logo as a real icon: console window + taskbar (replaces the blue PowerShell icon)
# ------------------------------------------------------------------
function New-RockBitmap {
    param([int]$Size)
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $k = $Size / 40.0
    $facets = @(
        @('#8FEBFF', 20, 2, 4, 12, 20, 20),
        @('#4FD1FF', 20, 2, 36, 12, 20, 20),
        @('#2F7BFF', 4, 12, 4, 28, 20, 20),
        @('#7B5CFF', 36, 12, 36, 28, 20, 20),
        @('#4B35C9', 4, 28, 20, 38, 20, 20),
        @('#9A6BFF', 36, 28, 20, 38, 20, 20)
    )
    foreach ($f in $facets) {
        $pts = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF([single]($f[1] * $k), [single]($f[2] * $k))),
            (New-Object System.Drawing.PointF([single]($f[3] * $k), [single]($f[4] * $k))),
            (New-Object System.Drawing.PointF([single]($f[5] * $k), [single]($f[6] * $k)))
        )
        $br = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($f[0]))
        $g.FillPolygon($br, $pts)
        $br.Dispose()
    }
    $g.Dispose()
    return $bmp
}
function New-RockHIcon {
    param([int]$Size)
    $bmp = New-RockBitmap $Size
    return $bmp.GetHicon()
}
try {
    [void][WinToolboxNative]::SetCurrentProcessExplicitAppUserModelID('Rock.Toolbox.Utility')
    $Host.UI.RawUI.WindowTitle = 'Rock Toolbox'
    $hwnd = [WinToolboxNative]::GetConsoleWindow()
    if ($hwnd -ne [IntPtr]::Zero) {
        $hBig   = New-RockHIcon 64
        $hSmall = New-RockHIcon 32
        [void][WinToolboxNative]::SendMessage($hwnd, 0x80, [IntPtr]1, $hBig)
        [void][WinToolboxNative]::SendMessage($hwnd, 0x80, [IntPtr]0, $hSmall)
    }
} catch { }




# ==============================================================================
# UI files: every ui\*.xaml file is read from the install folder (ui\MainWindow.xaml + ui\Styles.xaml + ui\pages\*.xaml)
# ==============================================================================

function Get-XamlSource {
    param([string]$Relative)
    $file = Join-Path $script:RockRoot ($Relative -replace '/', '\')
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing UI file: $file" }
    $txt = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8).TrimEnd()
    $eval = [System.Text.RegularExpressions.MatchEvaluator]{ param($m) Get-XamlSource $m.Groups[1].Value }
    return [regex]::Replace($txt, '[ \t]*<!--\s*@include\s+(\S+)\s*-->', $eval)
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
