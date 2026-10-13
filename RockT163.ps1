$script:DrvUnLoaded = $false

$script:DrvScanScript = {
    param($st)
    $ErrorActionPreference = 'Stop'
    $session  = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $found    = $searcher.Search("IsInstalled=0 and Type='Driver' and IsHidden=0")
    foreach ($u in $found.Updates) {
        $cls = ''; $prov = ''; $date = ''
        try { $cls = [string]$u.DriverClass } catch { }
        try { $prov = [string]$u.DriverProvider } catch { }
        try { $mf = [string]$u.DriverManufacturer; if ($mf) { $prov = $mf } } catch { }
        try { $date = ([datetime]$u.DriverVerDate).ToString('yyyy-MM-dd') } catch { }
        $mb = 0.0
        try { $mb = [double]$u.MaxDownloadSize / 1MB } catch { }
        $size = '-'
        if ($mb -ge 1024) { $size = ('{0:0.0} GB' -f ($mb / 1024)) } elseif ($mb -ge 1) { $size = ('{0:0} MB' -f $mb) } elseif ($mb -gt 0) { $size = '< 1 MB' }
        [pscustomobject]@{ Kind = 'Update'; Title = [string]$u.Title; Class = $cls; Provider = $prov; Date = $date; Size = $size; Id = [string]$u.Identity.UpdateID }
    }
    try {
        foreach ($d in @(Get-CimInstance Win32_PnPEntity -Filter 'ConfigManagerErrorCode <> 0')) {
            $nm = [string]$d.Name
            if (-not $nm) { $nm = [string]$d.DeviceID }
            [pscustomobject]@{ Kind = 'Device'; Title = $nm; Class = [string]$d.PNPClass; Provider = ''; Date = ''; Size = ''; Id = [string]$d.ConfigManagerErrorCode }
        }
    } catch { }
}.ToString()

$script:DrvListScript = {
    param($st)
    $ErrorActionPreference = 'Stop'
    foreach ($d in @(Get-WindowsDriver -Online)) {
        if ($d.BootCritical) { continue }
        $leaf = ''
        try { $leaf = Split-Path -Leaf ([string]$d.OriginalFileName) } catch { }
        if (-not $leaf) { $leaf = [string]$d.Driver }
        $date = ''
        try { $date = ([datetime]$d.Date).ToString('yyyy-MM-dd') } catch { }
        [pscustomobject]@{ Title = $leaf; Provider = [string]$d.ProviderName; Class = [string]$d.ClassName; Version = [string]$d.Version; Date = $date; Inf = [string]$d.Driver }
    }
}.ToString()

$script:DrvUninstallScript = {
    param($st, $infs)
    $ErrorActionPreference = 'Continue'
    $n = @($infs).Count
    $i = 0
    foreach ($inf in @($infs)) {
        if ($st.Cancel) { [void]$st.Lines.Add('Stopped - the remaining drivers were skipped.'); break }
        $i++
        $st.Pct = (($i - 1) * 100.0) / $n
        $st.Msg = "Removing driver ($i/$n): $inf"
        [void]$st.Lines.Add("Removing driver ($i/$n): $inf")
        try {
            $out = & pnputil.exe /delete-driver $inf /uninstall /force 2>&1
            $code = $LASTEXITCODE
            foreach ($l in @($out)) { $t = ([string]$l).Trim(); if ($t) { [void]$st.Lines.Add('  ' + $t) } }
            if ($code -eq 0 -or $code -eq 3010) { $st.Ok = $st.Ok + 1 } else { $st.Fail = $st.Fail + 1 }
        } catch {
            $st.Fail = $st.Fail + 1
            [void]$st.Lines.Add('  failed: ' + $_.Exception.Message)
        }
        $st.Pct = ($i * 100.0) / $n
    }
}.ToString()

function Get-DrvItems([object]$lv) {
    $src = $lv.ItemsSource
    if ($null -eq $src) { return @() }
    return @($src)
}
