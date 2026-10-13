function Update-WUCount {
    $all = @(Get-WUItems)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtWUCount.Text = "Selected: $n of $($all.Count)"
}

function Scan-WinUpdates {
    Start-Work {
        Log 'Searching Windows Update (this can take a minute)...' 'INFO' 'WinUpdate'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Searching Windows Update...'
        $ps = [powershell]::Create()
        [void]$ps.AddScript($script:WUScanScript).AddArgument($st)
        $async = $ps.BeginInvoke()
        try { $rows = Wait-WUJob $ps $async $st }
        catch {
            if ($_.Exception.Message -match 'Cancelled by user') { throw }
            $txtWUInfo.Text = 'Could not reach Windows Update: ' + $_.Exception.Message + '  (If Windows Update was disabled, turn it back on from the Updates tab.)'
            throw
        }
        $list = New-Object 'System.Collections.Generic.List[RockWU]'
        foreach ($r in @($rows | Sort-Object { $_.Type }, { $_.Title })) {
            if (-not $r) { continue }
            $o = New-Object RockWU
            $o.Title = [string]$r.Title; $o.KB = [string]$r.KB; $o.Size = [string]$r.Size
            $o.Type = [string]$r.Type; $o.Category = [string]$r.Category; $o.Id = [string]$r.Id
            $list.Add($o)
        }
        $lvWU.ItemsSource = $list
        $script:WULoaded = $true
        Update-WUCount
        if ($list.Count -eq 0) {
            $txtWUInfo.Text = 'Windows is up to date - no pending updates.'
            Log 'No Windows updates available.' 'INFO' 'WinUpdate'
        } else {
            $txtWUInfo.Text = "$($list.Count) update(s) available. Tick the ones you want, then press Install selected."
            Log "$($list.Count) Windows update(s) available." 'INFO' 'WinUpdate'
        }
    } 'Searching Windows Update...'
}

function Invoke-BgJob {
    param([string]$ScriptText, [object[]]$ArgList, $State)
    $ps = [powershell]::Create()
    [void]$ps.AddScript($ScriptText).AddArgument($State)
    foreach ($a in $ArgList) { [void]$ps.AddArgument($a) }
    $async = $ps.BeginInvoke()
    return @(Wait-WUJob $ps $async $State)
}

function Invoke-WUInstall {
    param($Ids, [bool]$Restart, [scriptblock]$Rescan)
    $script:WUReboot = $false
    Start-Work {
        $st = New-WUState
        $st.Pct = 0.0
        $st.Msg = 'Installing...'
        $ps = [powershell]::Create()
        [void]$ps.AddScript($script:WUInstallScript).AddArgument($st).AddArgument($Ids)
        $async = $ps.BeginInvoke()
        [void](Wait-WUJob $ps $async $st)
        $script:WUReboot = [bool]$st.Reboot
        Log ("Finished: $($st.Ok) installed, $($st.Fail) failed.") 'INFO' 'WinUpdate'
    } 'Installing updates...'
    if ($Rescan) { & $Rescan }
    if ($script:WUReboot) {
        if ($Restart) {
            Log 'A restart is needed - restarting in 30 seconds (run "shutdown /a" to cancel).' 'WARN' 'WinUpdate'
            try { Start-Process shutdown.exe -ArgumentList '/r', '/t', '30', '/c', '"Rock Toolbox: restarting to finish Windows updates"' -WindowStyle Hidden } catch { }
        } else {
            Log 'Restart your PC to finish installing.' 'WARN' 'WinUpdate'
        }
    }
}

function Install-WinUpdates {
    $sel = @(@(Get-WUItems) | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the updates you want to install first.' 'WARN' 'WinUpdate'; return }
    $ids = @($sel | ForEach-Object { $_.Id })
    Invoke-WUInstall -Ids $ids -Restart ([bool]$chkWUReboot.IsChecked) -Rescan { Scan-WinUpdates }
}

# ---- automatic updates on / off (Windows policy)
