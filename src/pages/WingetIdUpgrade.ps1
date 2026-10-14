# Module: pages/WingetIdUpgrade - Winget ID + Winget Upgrade pages
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Winget ID list (ready list of every app in Rock Toolbox) + Winget Upgrade page
# ------------------------------------------------------------------
Add-Type -TypeDefinition @'
using System.ComponentModel;
public class RockPkg {
    public string Name { get; set; }
    public string Id { get; set; }
    public string Category { get; set; }
}
public class RockGod {
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public int Index { get; set; }
}
public class RockUpg : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Name { get; set; }
    public string Id { get; set; }
    public string Version { get; set; }
    public string Available { get; set; }
    public string Source { get; set; }
}
public class RockWU : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Title { get; set; }
    public string KB { get; set; }
    public string Size { get; set; }
    public string Type { get; set; }
    public string Category { get; set; }
    public string Id { get; set; }
}
public class RockInst : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Publisher { get; set; }
    public string Source { get; set; }
    public string Size { get; set; }
    public string Key { get; set; }
    public string Cmd { get; set; }
    public string Quiet { get; set; }
}
public class RockDrv : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Title { get; set; }
    public string DrvClass { get; set; }
    public string Kind { get; set; }
    public string Provider { get; set; }
    public string Version { get; set; }
    public string DrvDate { get; set; }
    public string Size { get; set; }
    public string Id { get; set; }
    public string FilePath { get; set; }
}
'@

# ---------------- Winget ID: ready list ----------------
$script:AllPkgs = New-Object 'System.Collections.Generic.List[RockPkg]'
foreach ($a in @($Apps | Sort-Object { $_.Cat }, { $_.Name })) {
    $p = New-Object RockPkg
    $p.Name = $a.Name; $p.Id = $a.Id; $p.Category = $a.Cat
    $script:AllPkgs.Add($p)
}
[void]$cmbFindSource.Items.Add('All categories')
foreach ($c in @($Apps | ForEach-Object { $_.Cat } | Select-Object -Unique)) { [void]$cmbFindSource.Items.Add($c) }
$cmbFindSource.SelectedIndex = 0

function Update-FindList {
    $q = $txtFindQuery.Text.Trim()
    $cat = [string]$cmbFindSource.SelectedItem
    $view = New-Object 'System.Collections.Generic.List[RockPkg]'
    foreach ($p in $script:AllPkgs) {
        if ($cat -and $cat -ne 'All categories' -and $p.Category -ne $cat) { continue }
        if ($q -and $p.Name -notlike "*$q*" -and $p.Id -notlike "*$q*") { continue }
        $view.Add($p)
    }
    $lvFind.ItemsSource = $view
    $txtFindInfo.Text = "$($view.Count) $(L 'of') $($script:AllPkgs.Count) " + (L 'apps. Select a row, then Copy ID / Use in Install tab / Install selected (double-click copies the ID).')
}
Update-FindList
$txtFindQuery.Add_TextChanged({ Update-FindList })
$cmbFindSource.Add_SelectionChanged({ Update-FindList })

$btnFindCopy.Add_Click({
    $it = $lvFind.SelectedItem
    if (-not $it) { Log 'Select an app first.' 'WARN' 'Find'; return }
    try { [System.Windows.Clipboard]::SetText($it.Id); Log ('Copied ID: ' + $it.Id) 'INFO' 'Find' }
    catch { Log ('Could not copy: ' + $_.Exception.Message) 'ERROR' 'Find' }
})
$lvFind.Add_MouseDoubleClick({
    $it = $lvFind.SelectedItem
    if ($it) {
        try { [System.Windows.Clipboard]::SetText($it.Id); Log ('Copied ID: ' + $it.Id) 'INFO' 'Find' } catch { }
    }
})
$btnFindSend.Add_Click({
    $it = $lvFind.SelectedItem
    if (-not $it) { Log 'Select an app first.' 'WARN' 'Find'; return }
    $txtCustomId.Text = $it.Id
    Show-Page 'Install'
    Log ('ID sent to the Install tab (Custom winget ID): ' + $it.Id) 'INFO' 'Find'
})
$btnFindInstall.Add_Click({
    $it = $lvFind.SelectedItem
    if (-not $it) { Log 'Select an app first.' 'WARN' 'Find'; return }
    $pid2 = $it.Id
    Start-Work { Invoke-Winget 'install' @($pid2) } 'Installing...'
})

# ---------------- Winget Upgrade: apps that have updates, tick the ones you want ----------------
$script:upgLoaded = $false

function Read-WingetUpgrades {
    param([string]$File)
    $items = New-Object 'System.Collections.Generic.List[RockUpg]'
    if (-not (Test-Path $File)) { return ,$items }
    $lines = @(Get-Content -LiteralPath $File -Encoding UTF8 -ErrorAction SilentlyContinue)
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '-{8,}\s*$') { $start = $i + 1; break }
    }
    if ($start -lt 0) { return ,$items }
    for ($i = $start; $i -lt $lines.Count; $i++) {
        $ln = [string]$lines[$i]
        if (-not $ln.Trim()) { if ($items.Count -gt 0) { break } else { continue } }
        if ($ln -match 'upgrades? available') { break }
        $parts = @($ln -split '\s{2,}' | Where-Object { $_ -ne '' })
        if ($parts.Count -lt 4) { continue }
        $id = $parts[1].Trim()
        if ($id -notmatch '^[\w\.\-\+]+$') { continue }
        $u = New-Object RockUpg
        $u.Name = $parts[0].Trim(); $u.Id = $id
        $u.Version = $parts[2].Trim(); $u.Available = $parts[3].Trim()
        if ($parts.Count -ge 5) { $u.Source = $parts[4].Trim() }
        $items.Add($u)
    }
    return ,$items
}

function Get-UpgItems {
    $s = $lvUpg.ItemsSource
    if ($null -eq $s) { return @() }
    return @($s)
}

function Update-UpgCount {
    $all = @(Get-UpgItems)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtUpgCount.Text = "$(L 'Selected'): $n $(L 'of') $($all.Count)"
}

function Scan-Upgrades {
    Start-Work {
        if (-not (Get-Winget)) { Log 'winget not found. Install "App Installer" from the Microsoft Store.' 'WARN' 'Upgrade'; return }
        Log 'Checking which apps have updates (winget upgrade)...' 'INFO' 'Upgrade'
        $tmp = Join-Path $env:TEMP 'rock_winget_upgrade.txt'
        [void](Invoke-WingetTable 'upgrade --accept-source-agreements' $tmp)
        $items = Read-WingetUpgrades $tmp
        $lvUpg.ItemsSource = $items
        $script:upgLoaded = $true
        Update-UpgCount
        if ($items.Count -eq 0) {
            $txtUpgInfo.Text = L 'Everything is up to date (or winget could not read the list - press Check for updates again).'
            Log 'No app updates found.' 'INFO' 'Upgrade'
        } else {
            $txtUpgInfo.Text = "$($items.Count) " + (L 'app(s) can be updated. Tick the ones you want, then press Update selected.')
            Log "$($items.Count) app(s) have updates available." 'INFO' 'Upgrade'
        }
    } 'Checking for app updates...'
}

function Invoke-UpgradeIds {
    param([string[]]$Ids)
    if ($Ids.Count -eq 0) { Log 'Tick the apps you want to update first.' 'WARN' 'Upgrade'; return }
    Start-Work {
        Invoke-Winget 'upgrade' $Ids
        Log 'Update queue finished. Re-checking the list...' 'INFO' 'Upgrade'
    } 'Updating apps...'
    Scan-Upgrades
}

$btnUpgScan.Add_Click({ Scan-Upgrades })
$btnUpgSelAll.Add_Click({ foreach ($u in @(Get-UpgItems)) { $u.Selected = $true }; Update-UpgCount })
$btnUpgSelNone.Add_Click({ foreach ($u in @(Get-UpgItems)) { $u.Selected = $false }; Update-UpgCount })
$btnUpgRun.Add_Click({
    $ids = @(@(Get-UpgItems) | Where-Object { $_.Selected } | ForEach-Object { $_.Id })
    Invoke-UpgradeIds $ids
})
$btnUpgAll.Add_Click({
    $ids = @(@(Get-UpgItems) | ForEach-Object { $_.Id })
    Invoke-UpgradeIds $ids
})
$lvUpg.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{ Update-UpgCount })
$lvUpg.Add_MouseDoubleClick({
    $it = $lvUpg.SelectedItem
    if ($it) { $it.Selected = -not $it.Selected; Update-UpgCount }
})

