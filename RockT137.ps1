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
