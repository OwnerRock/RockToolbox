function Invoke-WingetTable {
    param([string]$WingetArgs, [string]$OutFile)
    Remove-Item $OutFile -Force -ErrorAction SilentlyContinue
    $cmd = '/d /c "mode con: cols=400 lines=9999 >nul 2>&1 & winget.exe ' + $WingetArgs + ' > "' + $OutFile + '""'
    return (Invoke-External 'cmd.exe' $cmd)
}

function Get-WingetMessage([int]$code) {
    if ($code -eq 0) { return 'OK' }
    if ($code -eq -1978335189) { return 'already installed (no newer version available)' }
    if ($code -eq -1978335212) { return 'package not found in the winget source' }
    if ($code -eq -1978335216) { return 'no applicable installer for this PC' }
    if ($code -eq -1978335215) { return 'installer hash mismatch' }
    if ($code -eq -1978335224) { return 'download failed (check your internet)' }
    if ($code -eq 3010 -or $code -eq 1641) { return 'installed - restart required' }
    return "finished with exit code $code"
}

function Invoke-Winget {
    param([string]$Verb, [string[]]$Ids)
    $wg = Get-Winget
    if (-not $wg) { Log 'winget not found. Install "App Installer" from the Microsoft Store, then try again.' 'WARN' 'Winget'; return }
    $n = $Ids.Count; $i = 0
    foreach ($id in $Ids) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $script:SegStart = 100.0 * $i / $n
        $script:SegEnd   = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ($Verb + ': ' + $id + '  (' + ($i + 1) + '/' + $n + ')')
        Log "winget $Verb : $id  ($($i + 1)/$n)" 'INFO' 'Winget'
        $wgArgs = "$Verb --id $id -e --silent --accept-source-agreements"
        if ($Verb -eq 'install' -or $Verb -eq 'upgrade') { $wgArgs += ' --accept-package-agreements' }
        $code = Invoke-External $wg $wgArgs
        $msg = Get-WingetMessage $code
        if ($code -eq 0 -or $code -eq -1978335189 -or $code -eq 3010) { Log ('  ' + $msg) 'INFO' 'Winget' } else { Log ('  ' + $msg) 'WARN' 'Winget' }
        $i++
    }
    $script:SegStart = -1.0
}

function New-Label([string]$title, [string]$desc) {
    $sp = New-Object System.Windows.Controls.StackPanel
    $t1 = New-Object System.Windows.Controls.TextBlock
    $t1.Text = $title; $t1.FontWeight = 'SemiBold'; Set-Res $t1 'Foreground' '#F2F4FF'
    [void]$sp.Children.Add($t1)
    if ($desc) {
        $t2 = New-Object System.Windows.Controls.TextBlock
        $t2.Text = $desc; $t2.FontSize = 11.5; Set-Res $t2 'Foreground' '#8D93AD'; $t2.TextWrapping = 'Wrap'; $t2.MaxWidth = 760
        [void]$sp.Children.Add($t2)
    }
    return $sp
}

function Reset-WindowsUpdate {
    Log 'Resetting Windows Update components...' 'INFO' 'Update'
    $svcs = 'wuauserv', 'bits', 'cryptsvc', 'msiserver'
    foreach ($s in $svcs) { Stop-Service $s -Force -ErrorAction SilentlyContinue }
    $sd = "$env:windir\SoftwareDistribution"; $cr = "$env:windir\System32\catroot2"
    Remove-Item "$sd.bak" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "$cr.bak" -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $sd) { Rename-Item $sd "$sd.bak" -ErrorAction SilentlyContinue }
    if (Test-Path $cr) { Rename-Item $cr "$cr.bak" -ErrorAction SilentlyContinue }
    foreach ($s in $svcs) { Start-Service $s -ErrorAction SilentlyContinue }
    Log 'Update components reset.' 'INFO' 'Update'
}

# tweak runner: a tweak is either a list of registry Ops (V = apply value, U = undo value, Rm = remove on undo) or Apply/Undo scriptblocks
