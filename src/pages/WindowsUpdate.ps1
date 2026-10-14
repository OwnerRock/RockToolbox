# Module: pages/WindowsUpdate - Windows Update page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Windows Update page (Windows Update Agent API, worked in a background runspace
# so the window stays responsive while Windows searches / downloads / installs)
# ------------------------------------------------------------------
$script:WULoaded = $false
$script:WUReboot = $false

$script:WUScanScript = {
    param($st)
    $ErrorActionPreference = 'Stop'
    $session  = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $found    = $searcher.Search('IsInstalled=0 and IsHidden=0')
    foreach ($u in $found.Updates) {
        $kb = ''
        try { if ($u.KBArticleIDs.Count -gt 0) { $kb = 'KB' + [string]$u.KBArticleIDs.Item(0) } } catch { }
        $cats = ''
        try { $n = @(); foreach ($c in $u.Categories) { $n += [string]$c.Name }; $cats = ($n -join ', ') } catch { }
        $mb = 0.0
        try { $mb = [double]$u.MaxDownloadSize / 1MB } catch { }
        $size = '-'
        if ($mb -ge 1024) { $size = ('{0:0.0} GB' -f ($mb / 1024)) }
        elseif ($mb -ge 1) { $size = ('{0:0} MB' -f $mb) }
        elseif ($mb -gt 0) { $size = '< 1 MB' }
        $type = 'Software'
        if ([int]$u.Type -eq 2) { $type = 'Driver' }
        [pscustomobject]@{ Title = [string]$u.Title; KB = $kb; Size = $size; Type = $type; Category = $cats; Id = [string]$u.Identity.UpdateID }
    }
}.ToString()

$script:WUInstallScript = {
    param($st, $ids)
    $ErrorActionPreference = 'Stop'
    $session  = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $st.Msg = 'Preparing the selected updates...'
    $found = $searcher.Search('IsInstalled=0 and IsHidden=0')
    $todo = @()
    foreach ($u in $found.Updates) { if ($ids -contains [string]$u.Identity.UpdateID) { $todo += $u } }
    $total = $todo.Count
    if ($total -eq 0) { [void]$st.Lines.Add('None of the selected updates are available any more.'); return }
    $span = 100.0 / $total
    $i = 0
    foreach ($u in $todo) {
        if ($st.Cancel) { [void]$st.Lines.Add('Stopped - the remaining updates were skipped.'); break }
        $i++
        $title = [string]$u.Title
        $short = $title
        if ($short.Length -gt 46) { $short = $short.Substring(0, 43) + '...' }
        $base = ($i - 1) * $span
        $st.Pct = $base
        try {
            if (-not $u.EulaAccepted) { $u.AcceptEula() }
            if (-not $u.IsDownloaded) {
                [void]$st.Lines.Add("Downloading ($i/$total): $title")
                $st.Msg = "Downloading ($i/$total): $short"
                $c = New-Object -ComObject Microsoft.Update.UpdateColl
                [void]$c.Add($u)
                $d = $session.CreateUpdateDownloader()
                $d.Updates = $c
                $job = $null
                try { $job = $d.BeginDownload($null, $null, $null) } catch { $job = $null }
                if ($job) {
                    $lastB = 0.0; $lastT = [DateTime]::Now; $speed = 0.0
                    while (-not $job.IsCompleted) {
                        if ($st.Cancel) { try { $job.RequestAbort() } catch { }; break }
                        $pr  = $job.GetProgress()
                        $got = [double]$pr.CurrentUpdateBytesDownloaded
                        $tot = [double]$pr.CurrentUpdateBytesToDownload
                        $pc  = [double]$pr.PercentComplete
                        $now = [DateTime]::Now
                        $dt  = ($now - $lastT).TotalSeconds
                        if ($dt -ge 1.0) { $speed = (($got - $lastB) / 1MB) / $dt; $lastB = $got; $lastT = $now }
                        $st.Pct = $base + $span * 0.7 * ($pc / 100.0)
                        $st.Msg = ('Downloading ({0}/{1}): {2:0}% - {3:0} of {4:0} MB - {5:0.0} MB/s - {6}' -f $i, $total, $pc, ($got / 1MB), ($tot / 1MB), $speed, $short)
                        Start-Sleep -Milliseconds 400
                    }
                    $dr = $d.EndDownload($job)
                } else {
                    $dr = $d.Download()
                }
                if ($st.Cancel) { [void]$st.Lines.Add('  download cancelled'); break }
                if ($dr.ResultCode -ne 2 -and $dr.ResultCode -ne 3) { throw ('download failed (code ' + $dr.ResultCode + ')') }
            }
            $st.Pct = $base + $span * 0.7
            $st.Msg = "Installing ($i/$total): $short"
            [void]$st.Lines.Add("Installing ($i/$total): $title")
            $c2 = New-Object -ComObject Microsoft.Update.UpdateColl
            [void]$c2.Add($u)
            $in = $session.CreateUpdateInstaller()
            $in.Updates = $c2
            $ij = $null
            try { $ij = $in.BeginInstall($null, $null, $null) } catch { $ij = $null }
            if ($ij) {
                while (-not $ij.IsCompleted) {
                    $pr = $ij.GetProgress()
                    $pc = [double]$pr.PercentComplete
                    $st.Pct = $base + $span * (0.7 + 0.3 * ($pc / 100.0))
                    $st.Msg = ('Installing ({0}/{1}): {2:0}% - {3}' -f $i, $total, $pc, $short)
                    Start-Sleep -Milliseconds 500
                }
                $ir = $in.EndInstall($ij)
            } else {
                $ir = $in.Install()
            }
            if ($ir.ResultCode -eq 2 -or $ir.ResultCode -eq 3) {
                $st.Ok = $st.Ok + 1
                if ($ir.RebootRequired) { $st.Reboot = $true }
                [void]$st.Lines.Add('  installed')
            } else {
                $st.Fail = $st.Fail + 1
                $hr = ('{0:X8}' -f $ir.HResult)
                [void]$st.Lines.Add('  failed (result ' + $ir.ResultCode + ', 0x' + $hr + ')')
            }
        } catch {
            $st.Fail = $st.Fail + 1
            [void]$st.Lines.Add('  failed: ' + $_.Exception.Message)
        }
        $st.Pct = ($i * 100.0) / $total
    }
}.ToString()

function New-WUState {
    return [hashtable]::Synchronized(@{
        Cancel = $false; AbortNow = $false; Msg = ''; Pct = -1.0; Printed = 0
        Ok = 0; Fail = 0; Reboot = $false
        Lines = [System.Collections.ArrayList]::Synchronized((New-Object System.Collections.ArrayList))
    })
}

function Wait-WUJob {
    param($Ps, $Async, $State)
    $last = [DateTime]::MinValue
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not $Async.IsCompleted) {
        $State.Cancel = [bool]$script:CancelRequested
        if ($State.Cancel -and $State.AbortNow) {
            try { [void]$Ps.BeginStop($null, $null) } catch { }
            throw 'Cancelled by user'
        }
        if (([DateTime]::Now - $last).TotalMilliseconds -ge 200) {
            $last = [DateTime]::Now
            while ($State.Printed -lt $State.Lines.Count) { Log ([string]$State.Lines[$State.Printed]) 'INFO' 'WinUpdate'; $State.Printed = $State.Printed + 1 }
            if ([double]$State.Pct -ge 0) { Set-Progress ([double]$State.Pct) ([string]$State.Msg) }
            elseif ($State.Msg) { Set-Progress -1 ([string]$State.Msg + '  (' + [int]$sw.Elapsed.TotalSeconds + ' s)') }
        }
        DoEvents
        Start-Sleep -Milliseconds 16
    }
    while ($State.Printed -lt $State.Lines.Count) { Log ([string]$State.Lines[$State.Printed]) 'INFO' 'WinUpdate'; $State.Printed = $State.Printed + 1 }
    $out = $null
    try { $out = $Ps.EndInvoke($Async) }
    catch {
        $e = $_.Exception
        while ($e.InnerException) { $e = $e.InnerException }
        $Ps.Dispose()
        throw $e.Message
    }
    if ($Ps.HadErrors -and $Ps.Streams.Error.Count -gt 0) {
        $msg = [string]$Ps.Streams.Error[0]
        $Ps.Dispose()
        throw $msg
    }
    $Ps.Dispose()
    return @($out)
}

function Get-WUItems {
    $src = $lvWU.ItemsSource
    if ($null -eq $src) { return @() }
    return @($src)
}
function Update-WUCount {
    $all = @(Get-WUItems)
    $n = @($all | Where-Object { $_.Selected }).Count
    $txtWUCount.Text = "$(L 'Selected'): $n $(L 'of') $($all.Count)"
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
            $txtWUInfo.Text = (L 'Could not reach Windows Update: ') + $_.Exception.Message + '  ' + (L '(If Windows Update was disabled, turn it back on from the Updates tab.)')
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
            $txtWUInfo.Text = L 'Windows is up to date - no pending updates.'
            Log 'No Windows updates available.' 'INFO' 'WinUpdate'
        } else {
            $txtWUInfo.Text = "$($list.Count) " + (L 'update(s) available. Tick the ones you want, then press Install selected.')
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
$script:AUKey = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU'
function Get-WUAutoState {
    try { $v = (Get-ItemProperty -LiteralPath $script:AUKey -Name NoAutoUpdate -ErrorAction Stop).NoAutoUpdate; return ([int]$v -ne 1) }
    catch { return $true }
}
function Update-WUAutoText {
    $on = Get-WUAutoState
    if ($on) { $txtWUAuto.Text = L 'Automatic updates: ON'; Set-Res $txtWUAuto 'Foreground' '#3DDC84' }
    else     { $txtWUAuto.Text = L 'Automatic updates: OFF'; Set-Res $txtWUAuto 'Foreground' '#FF6B6B' }
    $btnWUAutoOff.IsEnabled = $on
    $btnWUAutoOn.IsEnabled = (-not $on)
}
function Set-WUAuto([bool]$Enable) {
    try {
        if (-not (Test-Path -LiteralPath $script:AUKey)) { New-Item -Path $script:AUKey -Force | Out-Null }
        if ($Enable) {
            New-ItemProperty -LiteralPath $script:AUKey -Name NoAutoUpdate -Value 0 -PropertyType DWord -Force | Out-Null
            Remove-ItemProperty -LiteralPath $script:AUKey -Name AUOptions -ErrorAction SilentlyContinue
            Log 'Automatic Windows updates enabled.' 'INFO' 'WinUpdate'
        } else {
            New-ItemProperty -LiteralPath $script:AUKey -Name NoAutoUpdate -Value 1 -PropertyType DWord -Force | Out-Null
            New-ItemProperty -LiteralPath $script:AUKey -Name AUOptions -Value 2 -PropertyType DWord -Force | Out-Null
            Log 'Automatic Windows updates disabled. You can still install updates from this page.' 'INFO' 'WinUpdate'
        }
    } catch { Log ('Could not change the setting: ' + $_.Exception.Message) 'ERROR' 'WinUpdate' }
    Update-WUAutoText
}
$btnWUAutoOff.Add_Click({ Set-WUAuto $false })
$btnWUAutoOn.Add_Click({ Set-WUAuto $true })

$btnWUScan.Add_Click({ Scan-WinUpdates })
$btnWUInstall.Add_Click({ Install-WinUpdates })
$btnWUSelAll.Add_Click({ foreach ($u in @(Get-WUItems)) { $u.Selected = $true }; Update-WUCount })
$btnWUSelNone.Add_Click({ foreach ($u in @(Get-WUItems)) { $u.Selected = $false }; Update-WUCount })
$lvWU.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{ Update-WUCount })
$lvWU.Add_MouseDoubleClick({
    $it = $lvWU.SelectedItem
    if ($it) { $it.Selected = -not $it.Selected; Update-WUCount }
})

