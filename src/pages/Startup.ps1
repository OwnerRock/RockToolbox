# Module: pages/Startup - Startup Manager page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (13) Startup Manager ----
$script:StartupLoaded = $false
$script:StartupAll = $null
$script:StartupScript = {
    param($st)
    $ErrorActionPreference = 'SilentlyContinue'
    $rows = New-Object System.Collections.ArrayList
    $apprBase = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved'
    function Test-Enabled([string]$hive, [string]$sub, [string]$name) {
        $b = (Get-ItemProperty -LiteralPath ("{0}:\{1}\{2}" -f $hive, $apprBase, $sub) -Name $name -ErrorAction SilentlyContinue).$name
        if ($b -is [byte[]] -and $b.Length -gt 0) { return (($b[0] -band 1) -eq 0) }
        return $true
    }
    function Add-Item($name, $kind, $group, $cmd, $src, $ref, [bool]$en) {
        $s = 'Disabled'; if ($en) { $s = 'Enabled' }
        [void]$rows.Add([pscustomobject]@{ Name = [string]$name; Kind = [string]$kind; Group = [string]$group; Command = [string]$cmd; Source = [string]$src; Ref = [string]$ref; Status = $s })
    }
    $st.Msg = 'Reading startup programs...'
    $runKeys = @(
        @{ H = 'HKCU'; P = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; S = 'Run'; L = 'Registry (current user)' },
        @{ H = 'HKLM'; P = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'; S = 'Run'; L = 'Registry (all users)' },
        @{ H = 'HKLM'; P = 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run'; S = 'Run32'; L = 'Registry (all users, 32-bit)' }
    )
    foreach ($rk in $runKeys) {
        $k = Get-Item -LiteralPath $rk.P
        if (-not $k) { continue }
        foreach ($vn in @($k.GetValueNames())) {
            if (-not $vn) { continue }
            Add-Item $vn 'Startup app' 'apps' ([string]$k.GetValue($vn)) $rk.L ("reg|{0}|{1}|{2}" -f $rk.H, $rk.S, $vn) (Test-Enabled $rk.H $rk.S $vn)
        }
    }
    $wsh = New-Object -ComObject WScript.Shell
    $folders = @(
        @{ H = 'HKCU'; P = [Environment]::GetFolderPath('Startup'); L = 'Startup folder (current user)' },
        @{ H = 'HKLM'; P = [Environment]::GetFolderPath('CommonStartup'); L = 'Startup folder (all users)' }
    )
    foreach ($f in $folders) {
        foreach ($file in @(Get-ChildItem -LiteralPath $f.P -File -Force | Where-Object { $_.Name -ne 'desktop.ini' })) {
            $cmd = $file.FullName
            if ($file.Extension -eq '.lnk') { try { $lk = $wsh.CreateShortcut($file.FullName); $cmd = ("$($lk.TargetPath) $($lk.Arguments)").Trim() } catch { } }
            Add-Item $file.BaseName 'Startup app' 'apps' $cmd $f.L ("folder|{0}|StartupFolder|{1}" -f $f.H, $file.Name) (Test-Enabled $f.H 'StartupFolder' $file.Name)
        }
    }
    $st.Msg = 'Reading scheduled tasks...'
    foreach ($t in @(Get-ScheduledTask)) {
        if ($t.TaskPath -like '\Microsoft\*') { continue }
        $logon = $false
        foreach ($tr in @($t.Triggers)) { $cn = [string]$tr.CimClass.CimClassName; if ($cn -eq 'MSFT_TaskLogonTrigger' -or $cn -eq 'MSFT_TaskBootTrigger') { $logon = $true } }
        if (-not $logon) { continue }
        $a = @($t.Actions)[0]
        $cmd = ("$($a.Execute) $($a.Arguments)").Trim()
        Add-Item $t.TaskName 'Scheduled task' 'tasks' $cmd ('Task: ' + $t.TaskPath) ("task|{0}|{1}" -f $t.TaskPath, $t.TaskName) ($t.State -ne 'Disabled')
    }
    $st.Msg = 'Reading services...'
    foreach ($s in @(Get-CimInstance Win32_Service)) {
        if ($s.StartMode -ne 'Auto' -and $s.StartMode -ne 'Disabled') { continue }
        $pn = [string]$s.PathName
        if (-not $pn) { continue }
        if ($pn -match '(?i)\\Windows\\|\\Windows Defender\\|\\Microsoft Shared\\|WindowsApps|\\Microsoft\.NET\\') { continue }
        Add-Item $s.DisplayName 'Service' 'services' $pn ('Service: ' + $s.Name) ("svc|{0}" -f $s.Name) ($s.StartMode -ne 'Disabled')
    }
    return $rows
}.ToString()

function Get-StartupItems {
    if ($null -eq $script:StartupAll) { return @() }
    return @($script:StartupAll)
}
function Update-StartupCount {
    $src = @()
    if ($lvStartup.ItemsSource) { $src = @($lvStartup.ItemsSource) }
    $n = @($src | Where-Object { $_.Selected }).Count
    $txtStCount.Text = "$(L 'Selected'): $n $(L 'of') $($src.Count)"
}
function Apply-StartupFilter {
    if ($null -eq $script:StartupAll) { return }
    $kind = [string]$cmbStKind.SelectedItem
    $q = ([string]$txtStSearch.Text).Trim()
    $list = New-Object 'System.Collections.Generic.List[RockStartup]'
    foreach ($it in $script:StartupAll) {
        if ($kind -eq 'Startup apps' -and $it.Group -ne 'apps') { continue }
        if ($kind -eq 'Scheduled tasks' -and $it.Group -ne 'tasks') { continue }
        if ($kind -eq 'Services' -and $it.Group -ne 'services') { continue }
        if ($q -and ($it.Name -notlike "*$q*") -and ($it.Command -notlike "*$q*")) { continue }
        $list.Add($it)
    }
    $lvStartup.ItemsSource = $list
    Update-StartupCount
}

function Scan-Startup {
    Start-Work {
        Log 'Reading startup items (programs, tasks, services)...' 'INFO' 'Startup'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Reading startup items...'
        $rows = @(Invoke-BgJob $script:StartupScript @() $st)
        $all = New-Object 'System.Collections.Generic.List[RockStartup]'
        foreach ($r in @($rows | Where-Object { $_ -and $_.Name } | Sort-Object { $_.Group }, { $_.Status -eq 'Disabled' }, { $_.Name })) {
            $o = New-Object RockStartup
            $o.Name = [string]$r.Name; $o.Kind = [string]$r.Kind; $o.Group = [string]$r.Group
            $o.Command = [string]$r.Command; $o.Source = [string]$r.Source; $o.Ref = [string]$r.Ref; $o.Status = [string]$r.Status
            $all.Add($o)
        }
        $script:StartupAll = $all
        $script:StartupLoaded = $true
        Apply-StartupFilter
        Log "$($all.Count) startup items found." 'INFO' 'Startup'
    } 'Reading startup items...'
}

function Set-StartupItem {
    param($Item, [bool]$Enable)
    $p = ([string]$Item.Ref) -split '\|', 4
    switch ($p[0]) {
        { $_ -eq 'reg' -or $_ -eq 'folder' } {
            $key = "{0}:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\{1}" -f $p[1], $p[2]
            if (-not (Test-Path -LiteralPath $key)) { New-Item -Path $key -Force | Out-Null }
            if ($Enable) { [byte[]]$bytes = 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            else { [byte[]]$bytes = [byte[]](@(3, 0, 0, 0) + [BitConverter]::GetBytes([DateTime]::UtcNow.ToFileTimeUtc())) }
            Set-ItemProperty -LiteralPath $key -Name $p[3] -Value $bytes -Type Binary -Force
        }
        'task' {
            $tn = ([string]$Item.Ref) -split '\|', 3
            if ($Enable) { Enable-ScheduledTask -TaskPath $tn[1] -TaskName $tn[2] -ErrorAction Stop | Out-Null }
            else { Disable-ScheduledTask -TaskPath $tn[1] -TaskName $tn[2] -ErrorAction Stop | Out-Null }
        }
        'svc' {
            if ($Enable) { Set-Service -Name $p[1] -StartupType Automatic -ErrorAction Stop }
            else { Set-Service -Name $p[1] -StartupType Disabled -ErrorAction Stop }
        }
    }
    if ($Enable) { $Item.Status = 'Enabled' } else { $Item.Status = 'Disabled' }
}

function Invoke-StartupToggle {
    param([bool]$Enable)
    $sel = @(@($lvStartup.ItemsSource) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the items you want to change first.' 'WARN' 'Startup'; return }
    if (-not $Enable) {
        $svc = @($sel | Where-Object { $_.Group -eq 'services' }).Count
        if ($svc -gt 0) {
            $r = [System.Windows.MessageBox]::Show("$svc " + (L 'service(s) are selected. Disabling a service can break the program that needs it.') + "`n`n" + (L 'Disable the selected items?'), 'Rock Toolbox', 'YesNo', 'Warning')
            if ([string]$r -ne 'Yes') { return }
        }
    }
    $ok = 0; $bad = 0
    foreach ($it in $sel) {
        try { Set-StartupItem $it $Enable; $ok++ }
        catch { $bad++; Log "  failed: $($it.Name) - $($_.Exception.Message)" 'WARN' 'Startup' }
        $it.Selected = $false
    }
    $w = 'Disabled'; if ($Enable) { $w = 'Enabled' }
    $failTxt = ''
    if ($bad -gt 0) { $failTxt = ", $bad failed" }
    Log "$w $ok item(s)$failTxt. Changes apply at the next sign-in or restart." 'INFO' 'Startup'
    Update-StartupCount
}

foreach ($t in 'All', 'Startup apps', 'Scheduled tasks', 'Services') { [void]$cmbStKind.Items.Add($t) }
$cmbStKind.SelectedIndex = 0
$cmbStKind.Add_SelectionChanged({ Apply-StartupFilter })
$txtStSearch.Add_TextChanged({ Apply-StartupFilter })
$btnStRefresh.Add_Click({ Scan-Startup })
$btnStNone.Add_Click({ foreach ($x in @(Get-StartupItems)) { $x.Selected = $false }; Update-StartupCount })
$btnStDisable.Add_Click({ Invoke-StartupToggle $false })
$btnStEnable.Add_Click({ Invoke-StartupToggle $true })
$lvStartup.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{ Update-StartupCount })
$lvStartup.Add_MouseDoubleClick({ $it = $lvStartup.SelectedItem; if ($it) { $it.Selected = -not $it.Selected; Update-StartupCount } })
$btnStOpen.Add_Click({
    $it = $lvStartup.SelectedItem
    if (-not $it) { Log 'Click a row first, then press Open location.' 'WARN' 'Startup'; return }
    $cmd = [string]$it.Command
    $exe = $null
    if ($cmd -match '^\s*"([^"]+)"') { $exe = $Matches[1] }
    elseif ($cmd -match '^\s*(.+?\.(exe|lnk|bat|cmd|vbs|ps1))(\s|$)') { $exe = $Matches[1] }
    if ($exe) { $exe = [Environment]::ExpandEnvironmentVariables($exe) }
    try {
        if ($exe -and (Test-Path -LiteralPath $exe)) { Start-Process explorer.exe -ArgumentList "/select,`"$exe`"" }
        elseif ($exe -and (Test-Path -LiteralPath (Split-Path -Parent $exe))) { Start-Process explorer.exe -ArgumentList "`"$(Split-Path -Parent $exe)`"" }
        else { Log 'Could not find the file of this item.' 'WARN' 'Startup' }
    } catch { Log "Could not open the location: $($_.Exception.Message)" 'WARN' 'Startup' }
})

