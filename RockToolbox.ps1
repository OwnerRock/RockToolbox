<#
    Rock Toolbox - Windows utility (Install | Tweaks | Debloat | Config | Updates | Wallpapers | Windows Update | Uninstall Apps | Drivers | God Mode | Settings)

    Run with Start-RockToolbox.bat  (or: powershell -ExecutionPolicy Bypass -File .\RockToolbox.ps1)
    It re-launches itself as Administrator in Windows PowerShell 5.1.
#>
param([switch]$Relaunched)

$ErrorActionPreference = 'Stop'
trap {
    Write-Host ''
    Write-Host "Rock Toolbox error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    try {
        $d = Join-Path $env:LOCALAPPDATA 'RockToolbox'
        New-Item -ItemType Directory -Force -Path $d | Out-Null
        ($_ | Out-String) | Set-Content (Join-Path $d 'error.log')
    } catch {}
    Read-Host 'Press Enter to close'
    exit 1
}

# ------------------------------------------------------------------
# Elevation (and force Windows PowerShell 5.1 so the Appx cmdlets work)
# ------------------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -or $PSVersionTable.PSEdition -eq 'Core') {
    if (-not $PSCommandPath) { Write-Host 'Save this as a .ps1 file and run it from the file.'; exit }
    $argList = "-NoProfile -ExecutionPolicy Bypass -STA -File `"$PSCommandPath`" -Relaunched"
    if ($isAdmin) { Start-Process powershell.exe -ArgumentList $argList }
    else          { Start-Process powershell.exe -ArgumentList $argList -Verb RunAs }
    exit
}


# ------------------------------------------------------------------
# Console banner
# ------------------------------------------------------------------
function Show-Banner {
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

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

$AppDir = Join-Path $env:LOCALAPPDATA 'RockToolbox'
$WpDir  = Join-Path $AppDir 'Wallpapers'
New-Item -ItemType Directory -Force -Path $WpDir | Out-Null

Add-Type -TypeDefinition @"
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
"@

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


$env:ROCK_TOOLBOX_MASTER = $PSCommandPath
try {
    $partFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name -match '^RockT[0-9]{3}\.ps1$' } | Sort-Object Name)
    if ($partFiles.Count -ne 250) { throw ('Expected 250 RockT001..RockT250 .ps1 files next to RockToolbox.ps1, found ' + $partFiles.Count + '.') }
    foreach ($pf in $partFiles) { . $pf.FullName }
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    try { ($_ | Out-String) | Set-Content (Join-Path $AppDir 'error.log') } catch {}
    [System.Windows.MessageBox]::Show("Rock Toolbox failed to start:`n`n$($_.Exception.Message)`n`n$($_.ScriptStackTrace)", 'Rock Toolbox') | Out-Null
    Read-Host 'Press Enter to close'
}
