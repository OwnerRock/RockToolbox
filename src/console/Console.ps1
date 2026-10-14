# Module: console - banner, native helpers, console / taskbar icon (dot-sourced by RockToolbox.ps1, runs before the UI starts)

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


