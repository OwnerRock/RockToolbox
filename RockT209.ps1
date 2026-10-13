function Invoke-TaskbarVerb {
    param([string]$File, [string]$Action)
    $store = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell\Windows.taskbar' + $Action
    $handler = [string](Get-ItemProperty -LiteralPath $store -ErrorAction Stop).ExplorerCommandHandler
    if (-not $handler) { throw 'This Windows build has no taskbar pin handler.' }
    $sub  = 'Software\Classes\*\shell\RockTaskbar' + $Action
    $verb = 'RockTaskbar' + $Action
    $k = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($sub)
    try {
        $k.SetValue('ExplorerCommandHandler', $handler)
        $shell = New-Object -ComObject Shell.Application
        $ns = $shell.Namespace((Split-Path -Parent $File))
        $item = $ns.ParseName((Split-Path -Leaf $File))
        $item.InvokeVerb($verb)
        Pump-Wait 900
    } finally {
        $k.Close()
        try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($sub, $false) } catch { }
    }
}

function Pin-RockToTaskbar {
    if (-not $RockScriptPath -or -not (Test-Path -LiteralPath $RockScriptPath)) { throw 'Run Rock Toolbox from the saved .ps1 file to pin it.' }
    if (@(Get-RockPinnedLinks).Count -gt 0) { Log 'Rock Toolbox is already pinned.' 'INFO' 'Taskbar'; return }
    $ico = Join-Path $AppDir 'RockToolbox.ico'
    Save-RockIcon $ico
    $lnkPath = New-RockShortcut -Folder $AppDir -Icon $ico
    try { Set-LnkAppId $lnkPath 'Rock.Toolbox.Utility' }
    catch { Log ('  could not tag the shortcut (pinning still works): ' + $_.Exception.Message) 'WARN' 'Taskbar' }
    try { Invoke-TaskbarVerb $lnkPath 'pin' }
    catch { Log ('  pin handler failed: ' + $_.Exception.Message) 'WARN' 'Taskbar' }
    Pump-Wait 700
    if (@(Get-RockPinnedLinks).Count -gt 0) {
        Log 'Rock Toolbox pinned to the taskbar.' 'INFO' 'Taskbar'
    } else {
        Log 'Windows blocked automatic pinning. Right-click the selected shortcut and choose Pin to taskbar.' 'WARN' 'Taskbar'
        try { Start-Process explorer.exe ('/select,"' + $lnkPath + '"') } catch { }
    }
}

function Unpin-RockFromTaskbar {
    $links = @(Get-RockPinnedLinks)
    if ($links.Count -eq 0) { Log 'Rock Toolbox is not pinned.' 'INFO' 'Taskbar'; return }
    foreach ($l in $links) {
        try { Invoke-TaskbarVerb $l 'unpin' }
        catch { Log ('  unpin handler failed: ' + $_.Exception.Message) 'WARN' 'Taskbar' }
    }
    Pump-Wait 700
    $left = @(Get-RockPinnedLinks)
    foreach ($l in $left) { Remove-Item -LiteralPath $l -Force -ErrorAction SilentlyContinue }
    if ($left.Count -gt 0) { Log 'Pinned shortcut removed. If the icon is still visible, restart Explorer (Tweaks tab).' 'WARN' 'Taskbar' }
    else { Log 'Rock Toolbox removed from the taskbar.' 'INFO' 'Taskbar' }
}

$chkTaskbar.Add_Click({
    $want = [bool]$chkTaskbar.IsChecked
    $ttl = 'Removing from taskbar...'
    if ($want) { $ttl = 'Pinning to taskbar...' }
    Start-Work { if ($want) { Pin-RockToTaskbar } else { Unpin-RockFromTaskbar } } $ttl
    Update-TaskbarCheck
})

# ---- smooth scrolling (mouse wheel / touchpad) for every scroll area
