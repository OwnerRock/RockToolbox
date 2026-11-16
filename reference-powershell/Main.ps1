<#
    Rock Toolbox - entry script (loaded by RockToolbox.exe)

    RockToolbox.exe hosts the Windows PowerShell 5.1 engine in-process, runs this file, and the UI is
    built from ui\*.xaml. All the logic lives in the modules under src\ and is loaded below in a fixed order.

    For development you can still run it directly:
        powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\src\Main.ps1
#>
param([switch]$Relaunched, [switch]$NoConsole)

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
    if ($NoConsole) {
        try { Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show("Rock Toolbox error: $($_.Exception.Message)`n`nDetails were saved to: $env:LOCALAPPDATA\RockToolbox\error.log", 'Rock Toolbox') | Out-Null } catch { }
    } else {
        Read-Host 'Press Enter to close'
    }
    exit 1
}

# ------------------------------------------------------------------
# Paths. RockRoot = install folder (parent of src\). RockMain = what "restart / shortcut" should launch:
# RockToolbox.exe when we were started by it, otherwise this script.
# ------------------------------------------------------------------
$script:RockRoot = Split-Path -Parent $PSScriptRoot
$script:RockMain = $PSCommandPath
if ($env:ROCK_LAUNCHER -and (Test-Path -LiteralPath $env:ROCK_LAUNCHER)) { $script:RockMain = $env:ROCK_LAUNCHER }

# ------------------------------------------------------------------
# Elevation - only needed when this script is run by hand (the EXE already requires Administrator)
# ------------------------------------------------------------------
if ($env:ROCK_HOSTED -ne '1') {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin -or $PSVersionTable.PSEdition -eq 'Core') {
        $argList = "-NoProfile -ExecutionPolicy Bypass -STA -File `"$PSCommandPath`" -Relaunched"
        if ($NoConsole) { $argList += ' -NoConsole' }
        if ($isAdmin) { Start-Process powershell.exe -ArgumentList $argList }
        else          { Start-Process powershell.exe -ArgumentList $argList -Verb RunAs }
        exit
    }
}

# ------------------------------------------------------------------
# Modules - dot-sourced in this exact order so they share one scope (same behaviour as the old single file)
# ------------------------------------------------------------------
$RockModules = @(
    'console/Console.ps1',
    'core/Settings.ps1',
    'core/Language.ps1',
    'core/Window.ps1',
    'core/Helpers.ps1',
    'data/Apps.ps1',
    'data/Tweaks.ps1',
    'data/Debloat.ps1',
    'data/Customize.ps1',
    'data/Config.ps1',
    'pages/Install.ps1',
    'pages/Tweaks.ps1',
    'data/CursorPack.ps1',
    'features/Cursors.ps1',
    'features/RockShortcut.ps1',
    'pages/Config.ps1',
    'pages/Updates.ps1',
    'pages/Debloat.ps1',
    'pages/Wallpapers.ps1',
    'pages/WingetIdUpgrade.ps1',
    'pages/GodMode.ps1',
    'pages/WindowsUpdate.ps1',
    'pages/UninstallApps.ps1',
    'pages/Drivers.ps1',
    'pages/Settings.ps1',
    'features/TaskbarPin.ps1',
    'features/SmoothScroll.ps1',
    'features/Background.ps1',
    'features/Common.ps1',
    'features/Safety.ps1',
    'features/StopAndLiveTool.ps1',
    'features/Profile.ps1',
    'pages/SystemInfo.ps1',
    'pages/SystemInfoLive.ps1',
    'pages/Cleanup.ps1',
    'pages/Startup.ps1',
    'pages/Repair.ps1',
    'features/PauseUpdates.ps1',
    'pages/Downloads.ps1',
    'core/Navigation.ps1',
    'pages/Welcome.ps1'
)
foreach ($RockModule in $RockModules) {
    $RockModulePath = Join-Path $PSScriptRoot $RockModule
    if (-not (Test-Path -LiteralPath $RockModulePath)) { throw "Missing module: $RockModulePath" }
    . $RockModulePath
}
