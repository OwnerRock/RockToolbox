$script:GodCatMap = @{
    1='Appearance and Personalization'; 2='Hardware and Sound'; 3='Network and Internet'
    4='Sounds, Speech and Audio Devices'; 5='System and Security'; 6='Clock, Language and Region'
    7='Ease of Access'; 8='Programs'; 9='User Accounts'; 10='Security Center'; 11='Mobile PC'
}

function Load-GodMode {
    $script:GodItems.Clear(); $script:GodAll.Clear()
    try {
        $shell  = New-Object -ComObject Shell.Application
        $folder = $shell.NameSpace('shell:::' + $script:GodGuid)
        if (-not $folder) { $folder = $shell.NameSpace('::' + $script:GodGuid) }
        if (-not $folder) { throw 'Windows could not open the GodMode shell folder.' }

        # find the Category / Comments columns by header name (works on English Windows)
        $catCol = -1; $descCol = -1
        for ($c = 0; $c -lt 40; $c++) {
            $h = [string]$folder.GetDetailsOf($null, $c)
            if (-not $h) { continue }
            if ($catCol  -lt 0 -and $h -match '^Category$') { $catCol = $c }
            if ($descCol -lt 0 -and $h -match '^(Comments|Description)$') { $descCol = $c }
        }

        $rows = New-Object System.Collections.ArrayList
        foreach ($it in @($folder.Items())) {
            $name = [string]$it.Name
            if (-not $name) { continue }
            $cat = ''
            if ($catCol -ge 0) { try { $cat = [string]$folder.GetDetailsOf($it, $catCol) } catch { } }
            if (-not $cat) {
                try {
                    $v = $it.ExtendedProperty('System.ControlPanel.Category')
                    $n = @($v)[0]
                    if ($null -ne $n -and $script:GodCatMap.ContainsKey([int]$n)) { $cat = $script:GodCatMap[[int]$n] }
                } catch { }
            }
            if (-not $cat) { $cat = 'Other' }
            $desc = ''
            if ($descCol -ge 0) { try { $desc = [string]$folder.GetDetailsOf($it, $descCol) } catch { } }
            [void]$rows.Add([pscustomobject]@{ Name = $name; Category = $cat; Description = $desc; Item = $it })
        }
        $idx = 0
        foreach ($r in @($rows | Sort-Object Category, Name)) {
            $g = New-Object RockGod
            $g.Name = $r.Name; $g.Category = $r.Category; $g.Description = $r.Description; $g.Index = $idx
            [void]$script:GodItems.Add($r.Item)
            $script:GodAll.Add($g)
            $idx++
        }
        $script:GodLoaded = $true
    } catch {
        Log ('God Mode: ' + $_.Exception.Message) 'ERROR' 'GodMode'
    }

    $sel = [string]$cmbGodCat.SelectedItem
    $cmbGodCat.Items.Clear()
    [void]$cmbGodCat.Items.Add('All categories')
    foreach ($c in @($script:GodAll | ForEach-Object { $_.Category } | Select-Object -Unique)) { [void]$cmbGodCat.Items.Add($c) }
    if ($sel -and $cmbGodCat.Items.Contains($sel)) { $cmbGodCat.SelectedItem = $sel } else { $cmbGodCat.SelectedIndex = 0 }
    Update-GodList
}

function Update-GodList {
    $q   = $txtGodQuery.Text.Trim()
    $cat = [string]$cmbGodCat.SelectedItem
    $view = New-Object 'System.Collections.Generic.List[RockGod]'
    foreach ($g in $script:GodAll) {
        if ($cat -and $cat -ne 'All categories' -and $g.Category -ne $cat) { continue }
        if ($q -and $g.Name -notlike "*$q*" -and $g.Description -notlike "*$q*") { continue }
        $view.Add($g)
    }
    $lvGod.ItemsSource = $view
    $txtGodInfo.Text = "$($view.Count) of $($script:GodAll.Count) settings. Select a row and press Open selected (or double-click it)."
}

function Open-GodItem {
    $g = $lvGod.SelectedItem
    if (-not $g) { Log 'Select a setting first.' 'WARN' 'GodMode'; return }
    try {
        $script:GodItems[$g.Index].InvokeVerb()
        Log ('Opened: ' + $g.Name) 'INFO' 'GodMode'
    } catch {
        Log ('Could not open "' + $g.Name + '": ' + $_.Exception.Message) 'ERROR' 'GodMode'
    }
}

$txtGodQuery.Add_TextChanged({ Update-GodList })
