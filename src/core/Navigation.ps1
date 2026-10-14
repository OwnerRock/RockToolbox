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
    'Wallpapers' = $pnlWall
    'Winget ID'  = $pnlFind
    'Winget Upgrade' = $pnlUpg
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
    if ($changed -and $pages.Contains($Name)) { Start-PageTransition $pages[$Name] }
    $script:Settings['LastTab'] = $Name
    if ([bool]$script:Settings['RememberTab']) { Save-Settings }
    if ($Name -eq 'Wallpapers' -and -not $script:wpLoaded) { Start-WpSearch 'feed' '' '' $false }
    if ($Name -eq 'Winget Upgrade' -and -not $script:upgLoaded) { Scan-Upgrades }
    if ($Name -eq 'Windows Update') { Update-WUAutoText }
    if ($Name -eq 'Windows Update' -and -not $script:WULoaded) { Scan-WinUpdates }
    if ($Name -eq 'Uninstall Apps' -and -not $script:UnLoaded) { Scan-UnApps }
    if ($Name -eq 'God Mode' -and -not $script:GodLoaded) { Load-GodMode }
    if ($Name -eq 'Settings') { Update-TaskbarCheck }
    if ($Name -eq 'System Info') { Start-SysLive } else { Stop-SysLive }
    if ($Name -eq 'System Info' -and -not $script:SysInfoLoaded) { Load-SysInfo }
    if ($Name -eq 'Cleanup' -and -not $script:CleanLoaded) { Scan-Cleanup }
    if ($Name -eq 'Startup' -and -not $script:StartupLoaded) { Scan-Startup }
    if ($Name -eq 'Updates') { Update-PauseState }
}

foreach ($k in $pages.Keys) {
    if ($k -eq 'Settings') { continue }   # opened from the gear button, not the tab bar
    $b = New-Object System.Windows.Controls.Button
    $b.Content = L $k; $b.Tag = $k
    $b.FontWeight = 'SemiBold'; $b.FontSize = 13
    $b.HorizontalAlignment = 'Left'
    $b.HorizontalContentAlignment = 'Center'
    $b.Padding = '16,8'; $b.Margin = '0,0,6,6'
    $b.BorderThickness = '0'
    $b.Add_Click({ param($s, $e) Show-Page ([string]$s.Tag) })
    [void]$tabBar.Children.Add($b)
    $tabButtons[$k] = $b
}
$startPage = 'Install'
if ([bool]$script:Settings['RememberTab'] -and $pages.Contains([string]$script:Settings['LastTab'])) { $startPage = [string]$script:Settings['LastTab'] }
elseif ($pages.Contains([string]$script:Settings['StartTab'])) { $startPage = [string]$script:Settings['StartTab'] }
Show-Page $startPage

Log 'Starting the interface thread.' 'INFO' 'UI'
if ([bool]$script:Settings['AutoRestore']) { Log 'Rock Toolbox ready. A restore point is created automatically before risky actions (Settings > Behavior).' 'INFO' 'UI' }
else { Log 'Rock Toolbox ready. Tip: create a restore point first (Tweaks > Restore Point - Create).' 'INFO' 'UI' }
if (-not (Get-Winget)) { Log 'winget was not found. Install "App Installer" from the Microsoft Store to use the Install tab.' 'WARN' 'UI' }

$script:IconTimer.Start()
$wa = [System.Windows.SystemParameters]::WorkArea
if ($window.Width  -gt $wa.Width)  { $window.Width  = [Math]::Max(900, $wa.Width  - 20) }
if ($window.Height -gt $wa.Height) { $window.Height = [Math]::Max(600, $wa.Height - 20) }
$window.Add_Loaded({ Start-PageAnim $rootGrid 22; Start-CardsIntro })
if ([bool]$script:Settings['StartMaximized']) { $window.WindowState = 'Maximized' }
[void]$window.ShowDialog()

