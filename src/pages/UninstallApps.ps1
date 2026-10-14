# Module: pages/UninstallApps - Uninstall Apps page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Uninstall Apps page
# ------------------------------------------------------------------
$script:UnLoaded = $false
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
    $txtUnCount.Text = "$(L 'Selected'): $n"
}
function Update-UnView {
    $q = $txtUnQuery.Text.Trim()
    $t = [string]$cmbUnType.SelectedItem
    $view = New-Object 'System.Collections.Generic.List[RockInst]'
    foreach ($a in $script:UnAll) {
        if ($t -eq 'Desktop apps' -and $a.Source -ne 'Desktop') { continue }
        if ($t -eq 'Store apps' -and $a.Source -ne 'Store') { continue }
        if ($q -and $a.Name -notlike "*$q*" -and $a.Publisher -notlike "*$q*") { continue }
        $view.Add($a)
    }
    $lvUnApps.ItemsSource = $view
    Update-UnCount
    $txtUnInfo.Text = "$($view.Count) $(L 'of') $($script:UnAll.Count) " + (L 'apps shown. Tick what you want to remove, then press Uninstall selected.')
}

function Scan-UnApps {
    Start-Work {
        Log 'Listing installed apps...' 'INFO' 'Uninstall'
        $st = New-WUState
        $st.AbortNow = $true
        $st.Msg = 'Reading installed apps...'
        $rows = Invoke-BgJob $script:AppsScanScript @() $st
        $script:UnAll.Clear()
        foreach ($r in @($rows | Sort-Object { $_.Name })) {
            if (-not $r) { continue }
            $o = New-Object RockInst
            $o.Name = [string]$r.Name; $o.Version = [string]$r.Version; $o.Publisher = [string]$r.Publisher
            $o.Source = [string]$r.Source; $o.Size = [string]$r.Size; $o.Key = [string]$r.Key
            $o.Cmd = [string]$r.Cmd; $o.Quiet = [string]$r.Quiet
            $script:UnAll.Add($o)
        }
        $script:UnLoaded = $true
        Update-UnView
        Log "$($script:UnAll.Count) apps found." 'INFO' 'Uninstall'
    } 'Reading installed apps...'
}

function Uninstall-UnApps {
    $sel = @($script:UnAll | Where-Object { $_.Selected })
    if ($sel.Count -eq 0) { Log 'Tick the apps you want to remove first.' 'WARN' 'Uninstall'; return }
    $names = ($sel | Select-Object -First 8 | ForEach-Object { '- ' + $_.Name }) -join "`n"
    if ($sel.Count -gt 8) { $names += "`n" + ((L '... and {0} more') -f ($sel.Count - 8)) }
    $r = [System.Windows.MessageBox]::Show(((L 'Uninstall {0} app(s)?') -f $sel.Count) + "`n`n$names", 'Rock Toolbox', 'YesNo', 'Warning')
    if ([string]$r -ne 'Yes') { return }
    $items = @($sel | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Source = $_.Source; Key = $_.Key; Cmd = $_.Cmd; Quiet = $_.Quiet } })
    Start-Work {
        $st = New-WUState
        $st.Pct = 0.0
        $st.Msg = 'Uninstalling apps...'
        [void](Invoke-BgJob $script:AppsUninstallScript @(, $items) $st)
        Log ("Finished: $($st.Ok) removed, $($st.Fail) failed.") 'INFO' 'Uninstall'
    } 'Uninstalling apps...'
    Scan-UnApps
}

foreach ($t in 'All apps', 'Desktop apps', 'Store apps') { [void]$cmbUnType.Items.Add($t) }
$cmbUnType.SelectedIndex = 0
$txtUnQuery.Add_TextChanged({ Update-UnView })
$cmbUnType.Add_SelectionChanged({ Update-UnView })
$btnUnRefresh.Add_Click({ Scan-UnApps })
$btnUnSelNone.Add_Click({ foreach ($a in $script:UnAll) { $a.Selected = $false }; Update-UnCount })
$btnUnGo.Add_Click({ Uninstall-UnApps })
$lvUnApps.AddHandler([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent, [System.Windows.RoutedEventHandler]{ Update-UnCount })
$lvUnApps.Add_MouseDoubleClick({
    $it = $lvUnApps.SelectedItem
    if ($it) { $it.Selected = -not $it.Selected; Update-UnCount }
})

