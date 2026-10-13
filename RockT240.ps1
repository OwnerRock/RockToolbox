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
}

foreach ($k in $pages.Keys) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $k; $b.Tag = $k
    $b.FontWeight = 'SemiBold'; $b.FontSize = 13
    $b.Padding = '10,7'; $b.Margin = '0,2,5,2'
    $b.Add_Click({ param($s, $e) Show-Page ([string]$s.Tag) })
    [void]$tabBar.Children.Add($b)
    $tabButtons[$k] = $b
}
