# Module: features/Profile - Export / import of selections
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (4) export / import of the user's selections ----
function Export-RockProfile {
    $dlg = New-Object Microsoft.Win32.SaveFileDialog
    $dlg.Filter = 'Rock Toolbox profile (*.json)|*.json'
    $dlg.FileName = 'RockToolbox-profile.json'
    $dlg.InitialDirectory = [Environment]::GetFolderPath('MyDocuments')
    if (-not $dlg.ShowDialog()) { return }
    $tw = @($tweakBoxes | Where-Object { $_.IsChecked } | ForEach-Object { [string]$_.Tag.Name })
    $db = @($bloatBoxes | Where-Object { $_.IsChecked } | ForEach-Object { [string]$_.Tag.Name })
    $apps = @($script:Selected.Keys | ForEach-Object { [string]$_ })
    $tg = [ordered]@{}; $ch = [ordered]@{}
    foreach ($c in @($customizePanel.Children)) {
        if ($c -is [System.Windows.Controls.CheckBox] -and $c.Tag) { $tg[[string]$c.Tag.Name] = [bool]$c.IsChecked }
        elseif ($c -is [System.Windows.Controls.StackPanel]) {
            foreach ($x in @($c.Children)) {
                if ($x -is [System.Windows.Controls.ComboBox] -and $x.Tag -and $x.SelectedItem) { $ch[[string]$x.Tag.Name] = [string]$x.SelectedItem }
            }
        }
    }
    $obj = [ordered]@{
        Tool = 'RockToolbox'; Version = 1; Created = (Get-Date).ToString('s')
        Tweaks = $tw; Debloat = $db; Apps = $apps
        Customize = [ordered]@{ Toggles = $tg; Choices = $ch }
    }
    try {
        ($obj | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $dlg.FileName -Encoding UTF8
        Log "Profile saved: $($dlg.FileName)  ($($tw.Count) tweaks, $($db.Count) debloat, $($apps.Count) apps)" 'INFO' 'Profile'
    } catch { Log "Could not save the profile: $($_.Exception.Message)" 'ERROR' 'Profile' }
}

function Import-RockProfile {
    $dlg = New-Object Microsoft.Win32.OpenFileDialog
    $dlg.Filter = 'Rock Toolbox profile (*.json)|*.json'
    $dlg.InitialDirectory = [Environment]::GetFolderPath('MyDocuments')
    if (-not $dlg.ShowDialog()) { return }
    $j = $null
    try { $j = Get-Content -LiteralPath $dlg.FileName -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
    if (-not $j -or $j.Tool -ne 'RockToolbox') { Log 'That file is not a Rock Toolbox profile.' 'WARN' 'Profile'; return }
    $tw = @($j.Tweaks); $db = @($j.Debloat); $apps = @($j.Apps)
    foreach ($cb in $tweakBoxes) { $cb.IsChecked = ($tw -contains [string]$cb.Tag.Name) }
    foreach ($cb in $bloatBoxes) { $cb.IsChecked = ($db -contains [string]$cb.Tag.Name) }
    $script:Selected.Clear()
    foreach ($it in $script:AppCards) {
        $id = [string]$it.Card.Tag.Id
        if ($apps -contains $id) { $script:Selected[$id] = $true; Set-CardLook $it.Card $true } else { Set-CardLook $it.Card $false }
    }
    Update-SelCount
    Log "Profile loaded: $($tw.Count) tweaks, $($db.Count) debloat items and $($apps.Count) apps are now ticked. Review them in each tab, then run." 'INFO' 'Profile'
    $nT = 0; $nC = 0
    if ($j.Customize) {
        if ($j.Customize.Toggles) { $nT = @($j.Customize.Toggles.PSObject.Properties).Count }
        if ($j.Customize.Choices) { $nC = @($j.Customize.Choices.PSObject.Properties).Count }
    }
    if (($nT + $nC) -gt 0) {
        $r = [System.Windows.MessageBox]::Show(((L 'The profile also holds {0} Customize preferences (Config switches, dropdowns).') -f ($nT + $nC)) + "`n`n" + (L 'Apply them now? They change Windows settings immediately.'), 'Rock Toolbox', 'YesNo', 'Question')
        if ([string]$r -eq 'Yes') {
            $done = 0
            foreach ($c in @($customizePanel.Children)) {
                if ($c -is [System.Windows.Controls.CheckBox] -and $c.Tag -and $j.Customize.Toggles) {
                    $p = $j.Customize.Toggles.PSObject.Properties[[string]$c.Tag.Name]
                    if ($p) {
                        $want = [bool]$p.Value
                        if ([bool]$c.IsChecked -ne $want) {
                            try { Set-ToggleState $c.Tag $want; $c.IsChecked = $want; $done++ } catch { Log "  failed: $($c.Tag.Name)" 'WARN' 'Profile' }
                        }
                    }
                } elseif ($c -is [System.Windows.Controls.StackPanel] -and $j.Customize.Choices) {
                    foreach ($x in @($c.Children)) {
                        if ($x -is [System.Windows.Controls.ComboBox] -and $x.Tag) {
                            $p = $j.Customize.Choices.PSObject.Properties[[string]$x.Tag.Name]
                            if ($p -and $x.Items.Contains([string]$p.Value) -and [string]$x.SelectedItem -ne [string]$p.Value) { $x.SelectedItem = [string]$p.Value; $done++ }
                        }
                    }
                }
            }
            Log "Applied $done Customize preference(s). Restart Explorer or sign out for some of them." 'INFO' 'Profile'
        }
    }
}
$btnProfExport.Add_Click({ Export-RockProfile })
$btnProfImport.Add_Click({ Import-RockProfile })

