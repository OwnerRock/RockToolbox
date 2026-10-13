$script:UnAll = New-Object 'System.Collections.Generic.List[RockInst]'

$script:AppsScanScript = {
    param($st)
    $ErrorActionPreference = 'SilentlyContinue'
    $roots = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
             'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
             'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    $seen = @{}
    foreach ($p in @(Get-ItemProperty -Path $roots)) {
        $n = [string]$p.DisplayName
        if (-not $n) { continue }
        if ($p.SystemComponent -eq 1) { continue }
        if ($p.ParentKeyName) { continue }
        if ($n -match '\(KB\d+\)') { continue }
        $cmd = [string]$p.UninstallString
        $q = [string]$p.QuietUninstallString
        if (-not $cmd -and -not $q) { continue }
        $id = $n + '|' + [string]$p.DisplayVersion
        if ($seen.ContainsKey($id)) { continue }
        $seen[$id] = $true
        $size = ''
        if ($p.EstimatedSize) { $mb = [double]$p.EstimatedSize / 1024.0; if ($mb -ge 1024) { $size = ('{0:0.0} GB' -f ($mb / 1024)) } elseif ($mb -ge 1) { $size = ('{0:0} MB' -f $mb) } else { $size = '< 1 MB' } }
        [pscustomobject]@{ Name = $n; Version = [string]$p.DisplayVersion; Publisher = [string]$p.Publisher; Source = 'Desktop'; Size = $size; Key = [string]$p.PSChildName; Cmd = $cmd; Quiet = $q }
    }
    foreach ($a in @(Get-AppxPackage)) {
        if ($a.IsFramework -or $a.NonRemovable -or ([string]$a.SignatureKind -eq 'System')) { continue }
        $pub = [string]$a.Publisher
        if ($pub -match 'CN=([^,]+)') { $pub = $Matches[1] }
        [pscustomobject]@{ Name = [string]$a.Name; Version = [string]$a.Version; Publisher = $pub; Source = 'Store'; Size = ''; Key = [string]$a.PackageFullName; Cmd = ''; Quiet = '' }
    }
}.ToString()

$script:AppsUninstallScript = {
    param($st, $items)
    $ErrorActionPreference = 'Stop'
    $n = @($items).Count
    $i = 0
    foreach ($it in @($items)) {
        if ($st.Cancel) { [void]$st.Lines.Add('Stopped - the remaining apps were skipped.'); break }
        $i++
        $st.Pct = (($i - 1) * 100.0) / $n
        $st.Msg = "Uninstalling ($i/$n): " + $it.Name
        [void]$st.Lines.Add("Uninstalling ($i/$n): " + $it.Name)
        try {
            if ($it.Source -eq 'Store') {
                Remove-AppxPackage -Package $it.Key -AllUsers -ErrorAction Stop
                $st.Ok = $st.Ok + 1
                [void]$st.Lines.Add('  removed')
            } else {
                $cmd = [string]$it.Quiet
                $hidden = $true
                if (-not $cmd) { $cmd = [string]$it.Cmd; $hidden = $false }
                if ($cmd -match 'msiexec(\.exe)?\W*\s+.*?/[IiXx]\s*(\{[0-9A-Fa-f-]{36}\})') {
                    $cmd = 'msiexec.exe /x ' + $Matches[2] + ' /qn /norestart'
                    $hidden = $true
                }
                $wst = 'Normal'
                if ($hidden) { $wst = 'Hidden' }
                $p = Start-Process -FilePath $env:ComSpec -ArgumentList ('/c "' + $cmd + '"') -WindowStyle $wst -PassThru
                if (-not $p.WaitForExit(600000)) {
                    try { $p.Kill() } catch { }
                    throw 'the uninstaller did not finish in 10 minutes'
                }
                $code = $p.ExitCode
                if ($code -eq 0 -or $code -eq 3010 -or $code -eq 1641) {
                    $st.Ok = $st.Ok + 1
                    [void]$st.Lines.Add('  done')
                } else {
                    $st.Fail = $st.Fail + 1
                    [void]$st.Lines.Add("  uninstaller ended with code $code (it may have been cancelled)")
                }
            }
        } catch {
            $st.Fail = $st.Fail + 1
            [void]$st.Lines.Add('  failed: ' + $_.Exception.Message)
        }
        $st.Pct = ($i * 100.0) / $n
    }
}.ToString()

function Update-UnCount {
    $n = @($script:UnAll | Where-Object { $_.Selected }).Count
    $txtUnCount.Text = "Selected: $n"
}
