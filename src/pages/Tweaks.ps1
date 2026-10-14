# Module: pages/Tweaks - Tweaks + Customize page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Build UI: Tweaks + Customize
# ------------------------------------------------------------------
$tweakBoxes = New-Object System.Collections.ArrayList
foreach ($grp in 'Essential', 'Advanced') {
    $h = New-Object System.Windows.Controls.TextBlock
    $h.Style = $window.FindResource('H')
    if ($grp -eq 'Essential') { $h.Text = L 'Essential Tweaks' }
    else { $h.Text = L 'Advanced Tweaks - CAUTION'; Set-Res $h 'Foreground' '#FFB454' }
    [void]$tweaksPanel.Children.Add($h)
    foreach ($t in @($Tweaks | Where-Object { $_.Group -eq $grp })) {
        $cb = New-Object System.Windows.Controls.CheckBox
        $cb.Content = $t.Name
        $cb.ToolTip = $t.Desc
        $cb.Tag = $t
        [void]$tweaksPanel.Children.Add($cb)
        [void]$tweakBoxes.Add($cb)
    }
}

$btnTweakApply.Add_Click({ Start-Work {
    $sel = @($tweakBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'Nothing selected.' 'WARN' 'Tweaks'; return }
    if (@($sel | Where-Object { $_.Tag.Name -like 'Restore Point*' }).Count -eq 0) {
        if (-not (New-SafetyRestorePoint 'running tweaks')) { throw 'Cancelled by user' }
    }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $t = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Applying: ' + $t.Name + '  (' + ($i + 1) + '/' + $n + ')')
        Log "Applying: $($t.Name)  ($($i + 1)/$n)" 'INFO' 'Tweaks'
        try { Invoke-TweakAction $t 'Apply'; Log '  done' 'INFO' 'Tweaks' } catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; Log "  failed: $($_.Exception.Message)" 'ERROR' 'Tweaks' }
        $i++
    }
    $script:SegStart = -1.0
    Log "Tweaks finished ($n). A restart may be needed for some of them." 'INFO' 'Tweaks'
} 'Applying tweaks...' })
$btnTweakUndo.Add_Click({ Start-Work {
    $sel = @($tweakBoxes | Where-Object { $_.IsChecked })
    if ($sel.Count -eq 0) { Log 'Nothing selected.' 'WARN' 'Tweaks'; return }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $t = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Undoing: ' + $t.Name + '  (' + ($i + 1) + '/' + $n + ')')
        if (-not $t.Ops -and -not $t.Undo) { Log "No undo available for: $($t.Name)" 'WARN' 'Tweaks'; $i++; continue }
        Log "Undoing: $($t.Name)" 'INFO' 'Tweaks'
        try { Invoke-TweakAction $t 'Undo'; Log '  done' 'INFO' 'Tweaks' } catch { if ($_.Exception.Message -match 'Cancelled by user') { throw }; Log "  failed: $($_.Exception.Message)" 'ERROR' 'Tweaks' }
        $i++
    }
    $script:SegStart = -1.0
    Log 'Undo finished.' 'INFO' 'Tweaks'
} 'Undoing tweaks...' })
$btnTweakRec.Add_Click({ foreach ($cb in $tweakBoxes) { $cb.IsChecked = [bool]$cb.Tag.Rec } })
$btnTweakClear.Add_Click({ foreach ($cb in $tweakBoxes) { $cb.IsChecked = $false } })

$lastGroup = ''
foreach ($t in $Toggles) {
    if ($t.Group -and $t.Group -ne $lastGroup) {
        $gh = New-Object System.Windows.Controls.TextBlock
        $gh.Style = $window.FindResource('H'); $gh.Text = $t.Group
        [void]$customizePanel.Children.Add($gh)
        $lastGroup = $t.Group
    }
    if ($t.Choice) {
        $row = New-Object System.Windows.Controls.StackPanel
        $row.Orientation = 'Horizontal'; $row.Margin = '4,6,4,6'
        $lbl = New-Object System.Windows.Controls.TextBlock
        $lbl.Text = $t.Name; $lbl.VerticalAlignment = 'Center'; $lbl.Margin = '0,0,16,0'
        Set-Res $lbl 'Foreground' '#E6E8F2'
        $cmb = New-Object System.Windows.Controls.ComboBox
        $cmb.Width = 220; $cmb.Height = 28; $cmb.Foreground = Get-Brush '#000000'
        $cmb.VerticalContentAlignment = 'Center'; $cmb.ToolTip = $t.Desc
        foreach ($k in $t.Choice.Options.Keys) { [void]$cmb.Items.Add($k) }
        $cur = $null
        try { $cur = (Get-ItemProperty -LiteralPath $t.Choice.P -Name $t.Choice.N -ErrorAction Stop).($t.Choice.N) } catch { $cur = $null }
        if ($null -eq $cur) { $cur = $t.Choice.Def }
        $pick = $null
        foreach ($k in $t.Choice.Options.Keys) { if ("$($t.Choice.Options[$k])" -eq "$cur") { $pick = $k } }
        if ($pick) { $cmb.SelectedItem = $pick } else { $cmb.SelectedIndex = 0 }
        $cmb.Tag = $t
        $cmb.Add_SelectionChanged({
            param($s, $e)
            if (-not $s.SelectedItem) { return }
            $tg = $s.Tag
            try {
                Set-Reg $tg.Choice.P $tg.Choice.N ([int]$tg.Choice.Options[[string]$s.SelectedItem])
                Log ($tg.Name + ': ' + [string]$s.SelectedItem) 'INFO' 'Customize'
            } catch { Log "Failed: $($_.Exception.Message)" 'ERROR' 'Customize' }
        })
        [void]$row.Children.Add($lbl); [void]$row.Children.Add($cmb)
        [void]$customizePanel.Children.Add($row)
        continue
    }
    $cb = New-Object System.Windows.Controls.CheckBox
    $cb.Style = $window.FindResource('Switch')
    $cb.Content = $t.Name
    $cb.ToolTip = $t.Desc
    $cb.Tag = $t
    $cb.IsChecked = (Get-ToggleState $t)
    $cb.Add_Click({
        param($s, $e)
        $tg = $s.Tag
        try {
            Set-ToggleState $tg ([bool]$s.IsChecked)
            $st = 'OFF'; if ($s.IsChecked) { $st = 'ON' }
            Log ($tg.Name + ': ' + $st) 'INFO' 'Customize'
        } catch {
            Log "Failed: $($_.Exception.Message)" 'ERROR' 'Customize'
            $s.IsChecked = (Get-ToggleState $tg)
        }
    })
    [void]$customizePanel.Children.Add($cb)
}
$btnExplorer.Add_Click({
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Log 'Explorer restarted.' 'INFO' 'Customize'
})

$guidRx = '[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}'
$btnUltOn.Add_Click({ Start-Work {
    $o = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 | Out-String
    $m = [regex]::Match($o, $guidRx)
    if ($m.Success) { powercfg -setactive $m.Value | Out-Null; Log 'Ultimate Performance plan enabled.' 'INFO' 'Power' }
    else { Log 'Could not create the Ultimate Performance plan on this edition.' 'WARN' 'Power' }
} })
$btnUltOff.Add_Click({ Start-Work {
    powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e | Out-Null
    $list = powercfg -list | Out-String
    foreach ($line in ($list -split "`r?`n")) {
        if ($line -match 'Ultimate Performance') {
            $g = [regex]::Match($line, $guidRx).Value
            if ($g) { powercfg -delete $g | Out-Null }
        }
    }
    Log 'Back to Balanced; Ultimate Performance plan removed.' 'INFO' 'Power'
} })

$DnsMap = [ordered]@{
    'Cloudflare (1.1.1.1)' = @('1.1.1.1', '1.0.0.1')
    'Google (8.8.8.8)'     = @('8.8.8.8', '8.8.4.4')
    'Quad9 (9.9.9.9)'      = @('9.9.9.9', '149.112.112.112')
    'AdGuard (blocks ads)' = @('94.140.14.14', '94.140.15.15')
    'Automatic (DHCP)'     = $null
}
foreach ($k in $DnsMap.Keys) { [void]$cmbDns.Items.Add($k) }
$cmbDns.SelectedIndex = 0
$btnDns.Add_Click({ Start-Work {
    $name = [string]$cmbDns.SelectedItem
    $addr = $DnsMap[$name]
    Log "Setting DNS: $name" 'INFO' 'DNS'
    foreach ($a in @(Get-NetAdapter | Where-Object { $_.Status -eq 'Up' })) {
        if ($addr) { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses $addr }
        else       { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses }
        Log "  adapter: $($a.Name)" 'INFO' 'DNS'
    }
    Clear-DnsClientCache
    Log 'DNS updated and cache flushed.' 'INFO' 'DNS'
} })

