<#
    Rock Toolbox - Windows utility (Install | Tweaks | Debloat | Config | Updates | Windows Update | Uninstall Apps | Drivers | System Info | Cleanup | Startup | Repair | Wallpapers | God Mode | Settings)

    Run with Start-RockToolbox.bat  (or: powershell -ExecutionPolicy Bypass -File .\RockToolbox.ps1)
    It re-launches itself as Administrator in Windows PowerShell 5.1.

    Layout (keep these folders next to this file):
      src\console   banner + console icon          ui\MainWindow.xaml   window shell
      src\core      settings, window, helpers,     ui\Styles.xaml       all styles / templates
                    navigation                     ui\pages\*.xaml      one file per tab
      src\data      apps, tweaks, debloat, ...
      src\pages     one file per tab (logic)
      src\features  shortcut, cursors, taskbar, safety, profile, ...
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
# Remote launch support:  irm https://raw.githubusercontent.com/OwnerRock/RockToolbox/main/RockToolbox.ps1 | iex
# When run from memory there is no file path, so download the repo and run from disk.
# ------------------------------------------------------------------
if (-not $PSCommandPath) {
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $root = Join-Path $env:LOCALAPPDATA 'RockToolbox\app'
    $zip  = Join-Path $env:TEMP 'RockToolbox-main.zip'
    $tmp  = Join-Path $env:TEMP 'RockToolbox-extract'
    Write-Host 'Downloading Rock Toolbox...' -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/OwnerRock/RockToolbox/archive/refs/heads/main.zip' -OutFile $zip
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path $root) | Out-Null
    Move-Item -LiteralPath (Join-Path $tmp 'RockToolbox-main') -Destination $root
    Remove-Item $zip, $tmp -Recurse -Force -ErrorAction SilentlyContinue
    $main = Join-Path $root 'RockToolbox.ps1'
    $args2 = "-NoProfile -ExecutionPolicy Bypass -STA -File `"$main`""
    Start-Process powershell.exe -ArgumentList $args2 -Verb RunAs
    return
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
# Paths: the tool is split into modules (src\*) and UI files (ui\*) that sit next to this script
# ------------------------------------------------------------------
$script:RockMain = $PSCommandPath
$script:RockRoot = Split-Path -Parent $PSCommandPath

# console banner + icon (runs before the UI, exactly like the single-file version did)
. (Join-Path $script:RockRoot 'src\console\Console.ps1')

# every module is dot-sourced in this order into the same script scope (order matters: later files use earlier ones)
$__rockMods = @(
        'src\core\Settings.ps1',
        'src\core\Language.ps1',
        'src\core\Window.ps1',
        'src\core\Helpers.ps1',
        'src\data\Apps.ps1',
        'src\data\Tweaks.ps1',
        'src\data\Debloat.ps1',
        'src\data\Customize.ps1',
        'src\data\Config.ps1',
        'src\pages\Install.ps1',
        'src\pages\Tweaks.ps1',
        'src\data\CursorPack.ps1',
        'src\features\Cursors.ps1',
        'src\features\RockShortcut.ps1',
        'src\pages\Config.ps1',
        'src\pages\Updates.ps1',
        'src\pages\Debloat.ps1',
        'src\pages\Wallpapers.ps1',
        'src\pages\WingetIdUpgrade.ps1',
        'src\pages\GodMode.ps1',
        'src\pages\WindowsUpdate.ps1',
        'src\pages\UninstallApps.ps1',
        'src\pages\Drivers.ps1',
        'src\pages\Settings.ps1',
        'src\features\TaskbarPin.ps1',
        'src\features\SmoothScroll.ps1',
        'src\features\Background.ps1',
        'src\features\Common.ps1',
        'src\features\Safety.ps1',
        'src\features\StopAndLiveTool.ps1',
        'src\features\Profile.ps1',
        'src\pages\SystemInfo.ps1',
        'src\pages\SystemInfoLive.ps1',
        'src\pages\Cleanup.ps1',
        'src\pages\Startup.ps1',
        'src\pages\Repair.ps1',
        'src\features\PauseUpdates.ps1',
        'src\core\Navigation.ps1'
)

try {
    foreach ($__rockMod in $__rockMods) {
        $__rockPath = Join-Path $script:RockRoot $__rockMod
        if (-not (Test-Path -LiteralPath $__rockPath)) { throw "Missing file: $__rockMod (keep the src and ui folders next to RockToolbox.ps1)" }
        . $__rockPath
    }
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    try { ($_ | Out-String) | Set-Content (Join-Path $AppDir 'error.log') } catch {}
    [System.Windows.MessageBox]::Show("Rock Toolbox failed to start:`n`n$($_.Exception.Message)`n`n$($_.ScriptStackTrace)", 'Rock Toolbox') | Out-Null
    Read-Host 'Press Enter to close'
}
