# ==============================================================================
# MODULE: src\core\Navigation.ps1
# ==============================================================================
# Module: core/Navigation - Tabs, page switching, start-up
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Navigation (top tabs)
# ------------------------------------------------------------------
$pages = [ordered]@{
    'Install'    = $pnlInstall
    'Tweaks'     = $pnlTweaks
    'Debloat'    = $pnlDebloat
    'Config'     = $pnlConfig
    'Updates'    = $pnlUpdates
    'Windows Update' = $pnlWU
    'Uninstall Apps' = $pnlUnApps
    'Drivers'    = $pnlDrv
    'System Info' = $pnlSysInfo
    'Cleanup'    = $pnlClean
    'Startup'    = $pnlStartup
    'Repair'     = $pnlRepair
    'Downloads'  = $pnlDownloads
    'Wallpapers' = $pnlWall
    'God Mode'   = $pnlGod
    'Settings'   = $pnlSettings
}
$tabButtons = @{}
$script:CurPage = ''

function Show-Page {
    param([string]$Name)
    foreach ($k in $pages.Keys) {
        if ($k -eq $Name) { $pages[$k].Visibility = 'Visible' }
        else              { $pages[$k].Visibility = 'Collapsed' }
    }
    $changed = ($Name -ne $script:CurPage)
    $script:CurPage = $Name
    Update-TabColors
    if ($changed -and $pages.Contains($Name)) { Start-PageTransition $pages[$Name]; Start-PageStagger $Name }
    $script:Settings['LastTab'] = $Name
    if ([bool]$script:Settings['RememberTab']) { Save-Soon }
    if ($Name -eq 'Wallpapers' -and -not $script:wpLoaded) { Start-WpSearch 'feed' '' '' $false }
    if ($Name -eq 'Windows Update') { Update-WUAutoText }
    if ($Name -eq 'Windows Update' -and -not $script:WULoaded) { Scan-WinUpdates }
    if ($Name -eq 'Uninstall Apps' -and -not $script:UnLoaded) { Scan-UnApps }
    if ($Name -eq 'God Mode' -and -not $script:GodLoaded) { Load-GodMode }
    if ($Name -eq 'Tweaks') { Initialize-TweaksPage }
    if ($Name -eq 'Settings') { Update-TaskbarCheck }
    if ($Name -eq 'Config') { Update-FeatureStates }
    if ($Name -eq 'System Info') { Start-SysLive } else { Stop-SysLive }
    if ($Name -eq 'System Info' -and -not $script:SysInfoLoaded) { Load-SysInfo }
    if ($Name -eq 'Cleanup' -and -not $script:CleanLoaded) { Scan-Cleanup }
    if ($Name -eq 'Startup' -and -not $script:StartupLoaded) { Scan-Startup }
    if ($Name -eq 'Updates') { Update-PauseState }
}

function Add-NavButton([string]$k, [bool]$Side) {
    $b = New-Object System.Windows.Controls.Button
    $b.Tag = $k
    $b.FontWeight = 'SemiBold'
    $b.BorderThickness = '0'
    if ($Side) {
        $b.FontSize = 13
        $b.HorizontalAlignment = 'Stretch'; $b.HorizontalContentAlignment = 'Left'
        $b.Padding = '12,8'; $b.Margin = '0,0,0,2'
        $sp = New-Object System.Windows.Controls.StackPanel; $sp.Orientation = 'Horizontal'
        $dot = New-Object System.Windows.Controls.TextBlock
        $dot.Text = [string][char]0x25CF; $dot.FontSize = 8; $dot.Opacity = 0.75; $dot.Margin = '0,0,10,0'; $dot.VerticalAlignment = 'Center'
        $lbl = New-Object System.Windows.Controls.TextBlock; $lbl.Text = L $k; $lbl.VerticalAlignment = 'Center'
        [void]$sp.Children.Add($dot); [void]$sp.Children.Add($lbl)
        $b.Content = $sp
    } else {
        $b.Content = L $k
        $b.FontSize = 12.5
        $b.HorizontalAlignment = 'Left'; $b.HorizontalContentAlignment = 'Center'
        $b.Padding = '9,5'; $b.Margin = '0,0,4,4'
    }
    $b.Add_Click({ param($s, $e) Show-Page ([string]$s.Tag) })
    [void]$tabBar.Children.Add($b)
    $tabButtons[$k] = $b
}
if (Test-Modern) {
    $navGroups = [ordered]@{
        'Apps'        = @('Install', 'Updates', 'Uninstall Apps', 'Downloads')
        'System'      = @('Tweaks', 'Debloat', 'Config', 'Windows Update', 'Drivers', 'Startup')
        'Maintenance' = @('Cleanup', 'Repair', 'System Info')
        'More'        = @('Wallpapers', 'God Mode')
    }
    $first = $true
    foreach ($g in $navGroups.Keys) {
        $h = New-Object System.Windows.Controls.TextBlock
        $h.Text = (L $g); $h.FontSize = 10.5; $h.FontWeight = 'SemiBold'
        $h.Margin = $(if ($first) { '10,2,0,5' } else { '10,12,0,5' }); $first = $false
        Set-Res $h 'Foreground' '#9AA0B8'
        [void]$tabBar.Children.Add($h)
        foreach ($k in $navGroups[$g]) { if ($pages.Contains($k)) { Add-NavButton $k $true } }
    }
    foreach ($k in $pages.Keys) { if ($k -ne 'Settings' -and -not $tabButtons.ContainsKey($k)) { Add-NavButton $k $true } }
} else {
    foreach ($k in $pages.Keys) {
        if ($k -eq 'Settings') { continue }   # opened from the gear button, not the tab bar
        Add-NavButton $k $false
    }
}
$startPage = 'Install'
if ([bool]$script:Settings['RememberTab'] -and $pages.Contains([string]$script:Settings['LastTab'])) { $startPage = [string]$script:Settings['LastTab'] }
elseif ($pages.Contains([string]$script:Settings['StartTab'])) { $startPage = [string]$script:Settings['StartTab'] }
Show-Page $startPage

Log 'Starting the interface thread.' 'INFO' 'UI'
if ([bool]$script:Settings['AutoRestore']) { Log 'Rock Toolbox ready. A restore point is created automatically before risky actions (Settings > Behavior).' 'INFO' 'UI' }
else { Log 'Rock Toolbox ready. Tip: create a restore point first (Tweaks > Restore Point - Create).' 'INFO' 'UI' }
if (-not (Get-Winget)) { Log 'winget was not found - it will be installed automatically the first time it is needed (Install / Upgrade).' 'WARN' 'UI' }

$script:IconTimer.Start()
# open at a compact size that fits THIS screen: 75% x 72% of the work area (1440x750 on a 1920x1080 screen), centered in the work area
$wa = [System.Windows.SystemParameters]::WorkArea
$tw = [Math]::Round($wa.Width * 0.75)
$th = [Math]::Round($wa.Height * 0.72)
$tw = [Math]::Max(1000, [Math]::Min(1800, $tw))
$th = [Math]::Max(600, [Math]::Min(1000, $th))
$window.Width  = [Math]::Min($tw, $wa.Width  - 20)
$window.Height = [Math]::Min($th, $wa.Height - 20)
$window.WindowStartupLocation = 'Manual'
$window.Left = $wa.Left + ($wa.Width  - $window.Width)  / 2
$window.Top  = $wa.Top  + ($wa.Height - $window.Height) / 2
$window.Add_Loaded({
    Start-PageAnim $rootGrid 22; Start-CardsIntro; Update-UpdateBadges
    # the app cards are built right AFTER the opening animation, not during it
    $script:BuildBoot = New-Object System.Windows.Threading.DispatcherTimer
    $script:BuildBoot.Interval = [TimeSpan]::FromMilliseconds(500)
    $script:BuildBoot.Add_Tick({ $script:BuildBoot.Stop(); Start-AppBuild })
    $script:BuildBoot.Start()
})
$script:UpdDoneFlag = Join-Path $env:LOCALAPPDATA 'RockToolbox\update_done.flag'
if (Test-Path -LiteralPath $script:UpdDoneFlag) {
    $window.Add_Loaded({
        $script:UpdDoneTimer = New-Object System.Windows.Threading.DispatcherTimer
        $script:UpdDoneTimer.Interval = [TimeSpan]::FromMilliseconds(1800)
        $script:UpdDoneTimer.Add_Tick({
            $script:UpdDoneTimer.Stop()
            $ver = ''
            try { $ver = ([string](Get-Content -LiteralPath $script:UpdDoneFlag -ErrorAction Stop | Select-Object -First 1)).Trim() } catch { }
            Remove-Item -LiteralPath $script:UpdDoneFlag -Force -ErrorAction SilentlyContinue
            if (-not $ver) { $ver = [string]$script:RockVersion }
            [void](Show-RockMessage -Text ("Update is successfully completed`nRock Toolbox is now on version " + $ver + '.') -Caption 'Rock Toolbox' -Buttons 'OK' -Icon 'Information')
        })
        $script:UpdDoneTimer.Start()
    })
}
$script:PrereqBoot = New-Object System.Windows.Threading.DispatcherTimer
$script:PrereqBoot.Interval = [TimeSpan]::FromSeconds(7)
$script:PrereqBoot.Add_Tick({ $script:PrereqBoot.Stop(); Start-PrereqCheck })
$script:PrereqBoot.Start()
if (Test-UpdateCheckDue) {
    $script:UpdBoot = New-Object System.Windows.Threading.DispatcherTimer
    $script:UpdBoot.Interval = [TimeSpan]::FromSeconds(4)
    $script:UpdBoot.Add_Tick({ $script:UpdBoot.Stop(); Start-SilentUpdateCheck })
    $script:UpdBoot.Start()
}
if ([bool]$script:Settings['StartMaximized']) { $window.WindowState = 'Maximized' }
# a WPF/XAML error while drawing used to close the whole tool (the top-level trap exits). Now it is written to
# %LOCALAPPDATA%\RockToolbox\error.log and the tool keeps running.
$window.Dispatcher.Add_UnhandledException({
    param($sender, $e)
    try {
        $e.Handled = $true
        $msg = $e.Exception.ToString()
        try {
            $dir = Join-Path $env:LOCALAPPDATA 'RockToolbox'
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
            Add-Content -LiteralPath (Join-Path $dir 'error.log') -Value ((Get-Date).ToString('s') + "  UI error: " + $msg + "`r`n") -Encoding UTF8
        } catch { }
        try { Log ('UI error (see error.log): ' + $e.Exception.Message) 'WARN' 'UI' } catch { }
    } catch { }
})
