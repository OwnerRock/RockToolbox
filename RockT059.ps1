$btnSsh.Add_Click({ Start-Work {
    Log 'Installing OpenSSH Server (may take a few minutes)...' 'INFO' 'SSH'
    [void](Invoke-External 'powershell.exe' '-NoProfile -Command "Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0"')
    Set-Service sshd -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service sshd -ErrorAction SilentlyContinue
    if (-not (Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
    }
    Log 'OpenSSH Server enabled and listening on port 22.' 'INFO' 'SSH'
} })

$btnCurApply.Add_Click({ Start-Work {
    Log "Applying cursor scheme: $CursorSchemeName" 'INFO' 'Cursor'
    $dest = Join-Path $env:SystemRoot "Cursors\$CursorDirName"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null

    # extract the embedded pack straight from the script (no external files)
    Add-Type -AssemblyName System.IO.Compression
    $bytes = [Convert]::FromBase64String(($CursorPackB64 -replace '\s', ''))
    $ms  = New-Object System.IO.MemoryStream (, $bytes)
    $zip = New-Object System.IO.Compression.ZipArchive($ms, [System.IO.Compression.ZipArchiveMode]::Read)
    $cnt = $zip.Entries.Count
    try {
        foreach ($e in $zip.Entries) {
            $target = Join-Path $dest $e.Name
            if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Length -eq $e.Length)) { continue }
            $in = $e.Open(); $fs = [System.IO.File]::Create($target)
            try { $in.CopyTo($fs) } finally { $fs.Dispose(); $in.Dispose() }
        }
    } finally { $zip.Dispose(); $ms.Dispose() }
    Log "  $cnt cursor files written to $dest" 'INFO' 'Cursor'

    $vals = [ordered]@{}
    foreach ($n in $CursorMap.Keys) { $vals[$n] = "%SystemRoot%\Cursors\$CursorDirName\" + $CursorMap[$n] }
    $list = ($CursorSchemeOrder | ForEach-Object { "%SystemRoot%\Cursors\$CursorDirName\$_" }) -join ','
    Set-CursorRegistry -Values $vals -Scheme $CursorSchemeName -Source 1 -SchemeList $list
    Update-SystemCursors
    Log 'Cursor scheme applied. Open Mouse Properties > Pointers to see it.' 'INFO' 'Cursor'
} 'Applying cursors...' })

$btnCurReset.Add_Click({ Start-Work {
    Log 'Restoring default Windows cursors...' 'INFO' 'Cursor'
    $def = [ordered]@{
        Arrow='aero_arrow.cur'; Help='aero_helpsel.cur'; AppStarting='aero_working.ani'; Wait='aero_busy.ani'
        crosshair=''; precisionhair=''; IBeam=''; NWPen='aero_pen.cur'; No='aero_unavail.cur'
        SizeNS='aero_ns.cur'; SizeWE='aero_ew.cur'; SizeNWSE='aero_nwse.cur'; SizeNESW='aero_nesw.cur'
        SizeAll='aero_move.cur'; UpArrow='aero_up.cur'; Hand='aero_link.cur'; Person='aero_person.cur'; Pin='aero_pin.cur'
    }
    $vals = [ordered]@{}
    foreach ($n in $def.Keys) { $vals[$n] = if ($def[$n]) { '%SystemRoot%\cursors\' + $def[$n] } else { '' } }
    Set-CursorRegistry -Values $vals -Scheme 'Windows Default' -Source 2 -SchemeList $null
    Update-SystemCursors
    Log 'Default Windows cursors restored.' 'INFO' 'Cursor'
} 'Restoring cursors...' })

$btnShortcut.Add_Click({ Start-Work {
    if (-not $RockScriptPath -or -not (Test-Path -LiteralPath $RockScriptPath)) {
        throw 'Run Rock Toolbox from the saved .ps1 file to create a shortcut.'
    }
    $ico = Join-Path $AppDir 'RockToolbox.ico'
    Save-RockIcon $ico
    Log "Rock icon saved: $ico" 'INFO' 'Shortcut'
    $desktop = [Environment]::GetFolderPath('Desktop')
    $made = @()
    foreach ($f in @($desktop, (Split-Path -Parent $RockScriptPath)) | Select-Object -Unique) {
        try { $made += (New-RockShortcut -Folder $f -Icon $ico); Log "  shortcut created: $($made[-1])" 'INFO' 'Shortcut' }
        catch { Log "  could not create shortcut in $f : $($_.Exception.Message)" 'WARN' 'Shortcut' }
    }
    if ($made.Count -eq 0) { throw 'No shortcut could be created.' }
    Log 'Done. If the icon looks old, press F5 on the desktop or restart Explorer (Tweaks tab).' 'INFO' 'Shortcut'
} 'Creating shortcut...' })

# ------------------------------------------------------------------
# Windows Update profiles
