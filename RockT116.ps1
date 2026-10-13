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
    $txtUpgCount.Text = "Selected: $n of $($all.Count)"
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
            $txtUpgInfo.Text = 'Everything is up to date (or winget could not read the list - press Check for updates again).'
            Log 'No app updates found.' 'INFO' 'Upgrade'
        } else {
            $txtUpgInfo.Text = "$($items.Count) app(s) can be updated. Tick the ones you want, then press Update selected."
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
