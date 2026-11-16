<<<<<<< HEAD
﻿# ==============================================================================
# MODULE: src\pages\Config.ps1
# ==============================================================================
# Module: pages/Config - Config page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Build UI: Config
# ------------------------------------------------------------------
$featureBoxes = New-Object System.Collections.ArrayList
$script:FeatDone = @{}        # features that are already on (cannot be ticked again)
$script:FeatPending = @{}     # enabled by this tool in this session, waiting for a restart
foreach ($f in $Features) {
    $cb = New-Object System.Windows.Controls.CheckBox
    $cb.Content = $f.Name; $cb.Tag = $f
    [void]$featuresPanel.Children.Add($cb)
    [void]$featureBoxes.Add($cb)
}

# reads the real state of every optional feature (runs in the background so the page never freezes)
$script:FeatJob = {
    $r = @{}
    try { foreach ($x in @(Get-CimInstance -ClassName Win32_OptionalFeature -ErrorAction Stop)) { $r[[string]$x.Name] = [int]$x.InstallState } } catch { }
    if ($r.Count -eq 0) {
        try { foreach ($x in @(Get-WindowsOptionalFeature -Online -ErrorAction Stop)) { $st = 2; if ([string]$x.State -match '^Enabled') { $st = 1 }; $r[[string]$x.FeatureName] = $st } } catch { }
    }
    try { $bc = (& bcdedit.exe /enum '{current}' 2>$null | Out-String); $r['__f8legacy'] = [int]($bc -match 'bootmenupolicy\s+Legacy') } catch { }
    $r
}
function Set-FeatureBoxStates {
    param($Map)
    foreach ($cb in $featureBoxes) {
        $f = $cb.Tag
        $on = $false
        if ($f.Cmd) {
            if ($Map.ContainsKey('__f8legacy')) {
                $leg = ([int]$Map['__f8legacy'] -eq 1)
                if ($f.Name -match '- Enable$') { $on = $leg } else { $on = (-not $leg) }
            }
        } else {
            $found = @($f.Features | Where-Object { $Map.ContainsKey([string]$_) })
            if ($found.Count -gt 0) { $on = (@($found | Where-Object { [int]$Map[[string]$_] -ne 1 }).Count -eq 0) }
        }
        $pend = ((-not $on) -and $script:FeatPending.ContainsKey($f.Name))
        if ($on -or $pend) {
            $script:FeatDone[$f.Name] = $true
            $cb.IsChecked = $true; $cb.IsHitTestVisible = $false; $cb.Focusable = $false
            if ($pend) { $cb.Content = $f.Name + '   - enabled, restart Windows to finish'; $cb.Foreground = Get-Brush '#FFB454' }
            elseif ($f.Cmd) { $cb.Content = $f.Name + '   - current setting'; $cb.Foreground = Get-Brush '#3DDC84' }
            else { $cb.Content = $f.Name + '   - already enabled'; $cb.Foreground = Get-Brush '#3DDC84' }
        } else {
            $script:FeatDone.Remove($f.Name)
            $cb.IsHitTestVisible = $true; $cb.Focusable = $true
            $cb.Content = $f.Name
            $cb.ClearValue([System.Windows.Controls.Control]::ForegroundProperty)
        }
    }
}
function Update-FeatureStates {
    if ($script:FeatPs) { return }
    try {
        $script:FeatPs = [powershell]::Create()
        [void]$script:FeatPs.AddScript($script:FeatJob.ToString())
        $script:FeatHandle = $script:FeatPs.BeginInvoke()
    } catch { $script:FeatPs = $null; return }
    $script:FeatTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:FeatTimer.Interval = [TimeSpan]::FromMilliseconds(400)
    $script:FeatTimer.Add_Tick({
        if (-not $script:FeatHandle -or -not $script:FeatHandle.IsCompleted) { return }
        $script:FeatTimer.Stop()
        $map = $null
        try { $map = @($script:FeatPs.EndInvoke($script:FeatHandle))[0] } catch { }
        try { $script:FeatPs.Dispose() } catch { }
        $script:FeatPs = $null
        if ($map -is [hashtable]) { Set-FeatureBoxStates $map }
    })
    $script:FeatTimer.Start()
}

$btnFeatures.Add_Click({ Start-Work {
    $sel = @($featureBoxes | Where-Object { $_.IsChecked -and -not $script:FeatDone.ContainsKey([string]$_.Tag.Name) })
    if ($sel.Count -eq 0) { Log 'No features selected (the ticked ones are already enabled).' 'WARN' 'Features'; return }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $f = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Enabling: ' + $f.Name + '  (' + ($i + 1) + '/' + $n + ')')
        Log "Enabling: $($f.Name)" 'INFO' 'Features'
        if ($f.Cmd) {
            $code = Invoke-External $f.Cmd.File $f.Cmd.Arg
            Log "  exit code $code" 'INFO' 'Features'
        } else {
            foreach ($nm in $f.Features) {
                if ($script:CancelRequested) { throw 'Cancelled by user' }
                $code = Invoke-External 'dism.exe' "/Online /Enable-Feature /FeatureName:$nm /All /NoRestart /Quiet"
                Log "  $nm -> exit code $code (3010 = restart required)" 'INFO' 'Features'
                if ($code -eq 3010) { $script:FeatPending[$f.Name] = $true }
            }
        }
        $i++
    }
    $script:SegStart = -1.0
    Log 'Done. Restart Windows to finish enabling features.' 'INFO' 'Features'
    Update-FeatureStates
} 'Enabling features...' })

foreach ($f in $Fixes) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $f.Name; $b.Tag = $f
    $b.Add_Click({
        param($s, $e)
        $act = $s.Tag.Action
        Start-Work { & $act }
    })
    [void]$fixesPanel.Children.Add($b)
}
foreach ($p in $LegacyPanels) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $p.Name; $b.Tag = $p
    $b.Add_Click({
        param($s, $e)
        $pp = $s.Tag
        try {
            if ($pp.Arg) { Start-Process $pp.File -ArgumentList $pp.Arg } else { Start-Process $pp.File }
        } catch { Log "Could not open $($pp.Name): $($_.Exception.Message)" 'ERROR' 'Config' }
    })
    [void]$panelsPanel.Children.Add($b)
}
# ------------------------------------------------------------------
# Windows activation check (status, edition, product key)
# ------------------------------------------------------------------
$script:ActJob = {
    $o = @{ Ok = $false; Edition = ''; Status = -1; Name = ''; Desc = ''; Channel = ''; Partial = ''; Grace = 0; BiosKey = ''; RegKey = ''; Err = '' }
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
        $dv = [string]$cv.DisplayVersion
        if (-not $dv) { $dv = [string]$cv.ReleaseId }
        $extra = ''
        if ($dv) { $extra = $dv + ', ' }
        $o.Edition = ([string]$os.Caption).Trim() + '   (' + $extra + 'build ' + [string]$os.BuildNumber + ')'
    } catch { }
    try {
        $list = @(Get-CimInstance -ClassName SoftwareLicensingProduct -Filter "ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL" -ErrorAction Stop)
        $p = $list | Where-Object { [string]$_.Name -notmatch 'ESU|add-on' } | Select-Object -First 1
        if (-not $p -and $list.Count -gt 0) { $p = $list[0] }
        if ($p) {
            $o.Ok = $true
            $o.Status = [int]$p.LicenseStatus; $o.Name = [string]$p.Name; $o.Desc = [string]$p.Description
            $o.Channel = [string]$p.ProductKeyChannel; $o.Partial = [string]$p.PartialProductKey; $o.Grace = [int]$p.GracePeriodRemaining
        } else { $o.Err = 'No Windows license was found on this PC.' }
    } catch { $o.Err = $_.Exception.Message }
    try { $o.BiosKey = ([string](Get-CimInstance -ClassName SoftwareLicensingService -ErrorAction Stop).OA3xOriginalProductKey).Trim() } catch { }
    try {
        $dpid = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop).DigitalProductId
        if ($dpid -and $dpid.Count -ge 67) {
            $map = 'BCDFGHJKMPQRTVWXY2346789'
            $v = [byte[]]$dpid.Clone()
            $isWin8 = [int][math]::Floor($v[66] / 6) -band 1
            $v[66] = [byte](($v[66] -band 0xF7) -bor (($isWin8 -band 2) * 4))
            $key = ''; $last = 0
            for ($i = 24; $i -ge 0; $i--) {
                $cur = 0
                for ($j = 14; $j -ge 0; $j--) {
                    $cur = $cur * 256 + $v[$j + 52]
                    $v[$j + 52] = [byte][math]::Floor($cur / 24)
                    $cur = $cur % 24
                }
                $key = [string]$map[$cur] + $key; $last = $cur
            }
            if ($isWin8 -eq 1) { $r = $key.Substring(1); $key = $r.Substring(0, $last) + 'N' + $r.Substring($last) }
            $o.RegKey = ($key -replace '(.{5})(?=.)', '$1-')
        }
    } catch { }
    $o
}

function Start-ActivationJob {
    $ui = $script:ActUi
    $ui.Head.Text = 'Checking Windows activation...'
    $ui.Sub.Text = ''
    $ui.Copy.IsEnabled = $false; $ui.Again.IsEnabled = $false
    foreach ($t in $ui.Vals) { $t.Text = '...' }
    $ui.Note.Visibility = 'Collapsed'
    try {
        $script:ActPs = [powershell]::Create()
        [void]$script:ActPs.AddScript($script:ActJob.ToString())
        $script:ActHandle = $script:ActPs.BeginInvoke()
    } catch { $ui.Head.Text = 'Could not start the check'; $ui.Sub.Text = $_.Exception.Message; $ui.Again.IsEnabled = $true; return }
    if ($script:ActTimer) { $script:ActTimer.Stop() }
    $script:ActTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:ActTimer.Interval = [TimeSpan]::FromMilliseconds(300)
    $script:ActTimer.Add_Tick({
        if (-not $script:ActHandle -or -not $script:ActHandle.IsCompleted) { return }
        $script:ActTimer.Stop()
        $res = $null
        try { $res = @($script:ActPs.EndInvoke($script:ActHandle))[0] } catch { }
        try { $script:ActPs.Dispose() } catch { }
        Set-ActivationUi $res
    })
    $script:ActTimer.Start()
}

function Set-ActivationUi {
    param($r)
    $ui = $script:ActUi
    if (-not $ui) { return }
    $ui.Again.IsEnabled = $true
    $green = '<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="46" Height="46"><Ellipse Fill="#3DDC84"/><Path Data="M13,24 L20,31 L33,16" Stroke="#1F2329" StrokeThickness="3.4" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Grid>'
    $icon = $green
    if (-not $r -or -not ($r -is [hashtable])) {
        $ui.Head.Text = 'Could not read the license status'; $ui.Sub.Text = 'Try again in a moment.'
        $icon = Get-MsgIconXaml 'Warning'
    } else {
        $stNames = @{ 0 = 'Unlicensed'; 1 = 'Licensed'; 2 = 'Initial grace period'; 3 = 'Additional grace period'; 4 = 'Non-genuine grace period'; 5 = 'Notification mode'; 6 = 'Extended grace period' }
        $report = New-Object System.Collections.Generic.List[string]
        if ($r.Ok -and [int]$r.Status -eq 1) {
            $ui.Head.Text = 'Windows is activated'
            $ui.Sub.Text = 'License status: Licensed'
            if (([string]$r.Channel + [string]$r.Desc) -match 'Volume|KMS|GVLK' -and [int]$r.Grace -gt 0) {
                $ui.Sub.Text = 'Volume (KMS) activation - about ' + [int]([int]$r.Grace / 1440) + ' day(s) left until the next automatic renewal.'
            }
        } elseif ($r.Ok) {
            $ui.Head.Text = 'Windows is NOT activated'
            $nm = [string]$stNames[[int]$r.Status]; if (-not $nm) { $nm = 'Unknown' }
            $ui.Sub.Text = 'License status: ' + $nm
            $icon = Get-MsgIconXaml 'Error'
        } else {
            $ui.Head.Text = 'Could not read the license status'
            $ui.Sub.Text = [string]$r.Err
            $icon = Get-MsgIconXaml 'Warning'
        }
        $type = [string]$r.Channel; if (-not $type) { $type = [string]$r.Desc }
        $last5 = ''
        if ($r.Partial) { $last5 = 'XXXXX-XXXXX-XXXXX-XXXXX-' + [string]$r.Partial }
        $bios = [string]$r.BiosKey; if (-not $bios) { $bios = 'None stored in the BIOS / UEFI' }
        $reg = [string]$r.RegKey; $regMismatch = $false
        if (-not $reg) { $reg = 'Not available' }
        elseif ($r.Partial -and -not $reg.EndsWith([string]$r.Partial)) { $reg = $reg + '   (generic - not the active key)'; $regMismatch = $true }
        $vals = @([string]$r.Edition, [string]$r.Name, $type, $last5, $bios, $reg)
        for ($i = 0; $i -lt $ui.Vals.Count; $i++) { if (-not $vals[$i]) { $vals[$i] = '-' }; $ui.Vals[$i].Text = $vals[$i] }
        $script:ActReport = "Windows activation`r`n" + $ui.Head.Text + "`r`n" + $ui.Sub.Text + "`r`n"
        for ($i = 0; $i -lt $ui.Labels.Count; $i++) { $script:ActReport += $ui.Labels[$i] + ': ' + $vals[$i] + "`r`n" }
        $ui.Copy.IsEnabled = $true
        if ($r.Ok -and (-not [string]$r.BiosKey) -and ($regMismatch -or -not [string]$r.RegKey)) {
            $ui.Note.Text = 'Windows 10 / 11 usually uses a digital license linked to this PC or your Microsoft account, so the full key often cannot be read back. The last 5 characters above identify the active key.'
            $ui.Note.Visibility = 'Visible'
        }
    }
    try {
        $ui.Icon.Children.Clear()
        [void]$ui.Icon.Children.Add([Windows.Markup.XamlReader]::Parse((Convert-XamlTheme -Text $icon -Literal)))
    } catch { }
    Log ($ui.Head.Text) 'INFO' 'Activation'
}

function Show-ActivationCheck {
    $xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Rock Toolbox" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" FontFamily="Segoe UI" FontSize="13" MinWidth="600" MaxWidth="760">
  <Window.Resources>
__STYLES__
  </Window.Resources>
  <Border BorderBrush="#2B3038" BorderThickness="1" Padding="24,22">
    <StackPanel>
      <DockPanel LastChildFill="True">
        <Grid x:Name="actIcon" DockPanel.Dock="Left" Width="46" Height="46" VerticalAlignment="Top" Margin="0,0,16,0"/>
        <StackPanel VerticalAlignment="Center">
          <TextBlock x:Name="actHead" FontSize="17" FontWeight="Bold" Foreground="White" TextWrapping="Wrap"/>
          <TextBlock x:Name="actSub" Foreground="#9AA0B8" TextWrapping="Wrap" Margin="0,2,0,0"/>
        </StackPanel>
      </DockPanel>
      <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Margin="0,16,0,0" Padding="14,10">
        <Grid x:Name="actGrid"/>
      </Border>
      <TextBlock x:Name="actNote" Foreground="#8D93AD" FontSize="11.5" TextWrapping="Wrap" MaxWidth="640" Margin="2,10,0,0" Visibility="Collapsed"/>
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,18,0,0">
        <Button x:Name="actCopy" Style="{StaticResource DiaBtn2}" Content="Copy details" Cursor="Hand"/>
        <Button x:Name="actAgain" Style="{StaticResource DiaBtn2}" Content="Check again" Margin="10,0,0,0" Cursor="Hand"/>
        <Button x:Name="actClose" Style="{StaticResource DiaBtn}" Content="Close" IsDefault="True" IsCancel="True" Margin="10,0,0,0" Cursor="Hand"/>
      </StackPanel>
    </StackPanel>
  </Border>
</Window>
'@
    $xaml = $xaml.Replace('__STYLES__', $script:DiaStylesXaml)
    $xaml = Add-RockDialogChrome -Text $xaml -Title 'Windows Activation'
    $xaml = Convert-XamlTheme -Text $xaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($xaml)
    Initialize-RockDialogClose $w
    $script:ActWin = $w
    $grid = $w.FindName('actGrid')
    $c1 = New-Object System.Windows.Controls.ColumnDefinition; $c1.Width = [System.Windows.GridLength]::Auto
    $c2 = New-Object System.Windows.Controls.ColumnDefinition; $c2.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
    [void]$grid.ColumnDefinitions.Add($c1); [void]$grid.ColumnDefinitions.Add($c2)
    $labels = @('Windows edition', 'License', 'License type', 'Active key (last 5)', 'Key in BIOS / UEFI', 'Installed key (registry)')
    $valBoxes = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $labels.Count; $i++) {
        $rd = New-Object System.Windows.Controls.RowDefinition; $rd.Height = [System.Windows.GridLength]::Auto
        [void]$grid.RowDefinitions.Add($rd)
        $l = New-Object System.Windows.Controls.TextBlock
        $l.Text = $labels[$i]; $l.Margin = '0,5,26,5'; $l.Foreground = Get-Brush '#9AA0B8'
        [System.Windows.Controls.Grid]::SetRow($l, $i); [System.Windows.Controls.Grid]::SetColumn($l, 0)
        $v = New-Object System.Windows.Controls.TextBlock
        $v.Margin = '0,5,0,5'; $v.Foreground = Get-Brush '#E6E8F2'; $v.TextWrapping = 'Wrap'; $v.MaxWidth = 430; $v.FontWeight = 'SemiBold'
        [System.Windows.Controls.Grid]::SetRow($v, $i); [System.Windows.Controls.Grid]::SetColumn($v, 1)
        [void]$grid.Children.Add($l); [void]$grid.Children.Add($v); [void]$valBoxes.Add($v)
    }
    $script:ActUi = @{
        Icon = $w.FindName('actIcon'); Head = $w.FindName('actHead'); Sub = $w.FindName('actSub'); Note = $w.FindName('actNote')
        Copy = $w.FindName('actCopy'); Again = $w.FindName('actAgain'); Vals = @($valBoxes); Labels = $labels
    }
    $script:ActReport = ''
    $script:ActUi.Copy.Add_Click({ try { [System.Windows.Clipboard]::SetText([string]$script:ActReport); Log 'Activation details copied to the clipboard.' 'INFO' 'Activation' } catch { Log ('Could not copy: ' + $_.Exception.Message) 'WARN' 'Activation' } })
    $script:ActUi.Again.Add_Click({ Start-ActivationJob })
    $w.FindName('actClose').Add_Click({ $script:ActWin.Close() })
    $w.Add_Closed({ try { if ($script:ActTimer) { $script:ActTimer.Stop() } } catch { }; $script:ActUi = $null })
    if (Test-Rtl) { $w.FlowDirection = 'RightToLeft' }
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    Start-ActivationJob
    [void]$w.ShowDialog()
}
$btnActCheck.Add_Click({ Show-ActivationCheck })

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


=======
﻿# ==============================================================================
# MODULE: src\pages\Config.ps1
# ==============================================================================
# Module: pages/Config - Config page
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Build UI: Config
# ------------------------------------------------------------------
$featureBoxes = New-Object System.Collections.ArrayList
$script:FeatDone = @{}        # features that are already on (cannot be ticked again)
$script:FeatPending = @{}     # enabled by this tool in this session, waiting for a restart
foreach ($f in $Features) {
    $cb = New-Object System.Windows.Controls.CheckBox
    $cb.Content = $f.Name; $cb.Tag = $f
    [void]$featuresPanel.Children.Add($cb)
    [void]$featureBoxes.Add($cb)
}

# reads the real state of every optional feature (runs in the background so the page never freezes)
$script:FeatJob = {
    $r = @{}
    try { foreach ($x in @(Get-CimInstance -ClassName Win32_OptionalFeature -ErrorAction Stop)) { $r[[string]$x.Name] = [int]$x.InstallState } } catch { }
    if ($r.Count -eq 0) {
        try { foreach ($x in @(Get-WindowsOptionalFeature -Online -ErrorAction Stop)) { $st = 2; if ([string]$x.State -match '^Enabled') { $st = 1 }; $r[[string]$x.FeatureName] = $st } } catch { }
    }
    try { $bc = (& bcdedit.exe /enum '{current}' 2>$null | Out-String); $r['__f8legacy'] = [int]($bc -match 'bootmenupolicy\s+Legacy') } catch { }
    $r
}
function Set-FeatureBoxStates {
    param($Map)
    foreach ($cb in $featureBoxes) {
        $f = $cb.Tag
        $on = $false
        if ($f.Cmd) {
            if ($Map.ContainsKey('__f8legacy')) {
                $leg = ([int]$Map['__f8legacy'] -eq 1)
                if ($f.Name -match '- Enable$') { $on = $leg } else { $on = (-not $leg) }
            }
        } else {
            $found = @($f.Features | Where-Object { $Map.ContainsKey([string]$_) })
            if ($found.Count -gt 0) { $on = (@($found | Where-Object { [int]$Map[[string]$_] -ne 1 }).Count -eq 0) }
        }
        $pend = ((-not $on) -and $script:FeatPending.ContainsKey($f.Name))
        if ($on -or $pend) {
            $script:FeatDone[$f.Name] = $true
            $cb.IsChecked = $true; $cb.IsHitTestVisible = $false; $cb.Focusable = $false
            if ($pend) { $cb.Content = $f.Name + '   - enabled, restart Windows to finish'; $cb.Foreground = Get-Brush '#FFB454' }
            elseif ($f.Cmd) { $cb.Content = $f.Name + '   - current setting'; $cb.Foreground = Get-Brush '#3DDC84' }
            else { $cb.Content = $f.Name + '   - already enabled'; $cb.Foreground = Get-Brush '#3DDC84' }
        } else {
            $script:FeatDone.Remove($f.Name)
            $cb.IsHitTestVisible = $true; $cb.Focusable = $true
            $cb.Content = $f.Name
            $cb.ClearValue([System.Windows.Controls.Control]::ForegroundProperty)
        }
    }
}
function Update-FeatureStates {
    if ($script:FeatPs) { return }
    try {
        $script:FeatPs = [powershell]::Create()
        [void]$script:FeatPs.AddScript($script:FeatJob.ToString())
        $script:FeatHandle = $script:FeatPs.BeginInvoke()
    } catch { $script:FeatPs = $null; return }
    $script:FeatTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:FeatTimer.Interval = [TimeSpan]::FromMilliseconds(400)
    $script:FeatTimer.Add_Tick({
        if (-not $script:FeatHandle -or -not $script:FeatHandle.IsCompleted) { return }
        $script:FeatTimer.Stop()
        $map = $null
        try { $map = @($script:FeatPs.EndInvoke($script:FeatHandle))[0] } catch { }
        try { $script:FeatPs.Dispose() } catch { }
        $script:FeatPs = $null
        if ($map -is [hashtable]) { Set-FeatureBoxStates $map }
    })
    $script:FeatTimer.Start()
}

$btnFeatures.Add_Click({ Start-Work {
    $sel = @($featureBoxes | Where-Object { $_.IsChecked -and -not $script:FeatDone.ContainsKey([string]$_.Tag.Name) })
    if ($sel.Count -eq 0) { Log 'No features selected (the ticked ones are already enabled).' 'WARN' 'Features'; return }
    $n = $sel.Count; $i = 0
    foreach ($cb in $sel) {
        if ($script:CancelRequested) { throw 'Cancelled by user' }
        $f = $cb.Tag
        $script:SegStart = 100.0 * $i / $n; $script:SegEnd = 100.0 * ($i + 1) / $n
        Set-Progress $script:SegStart ('Enabling: ' + $f.Name + '  (' + ($i + 1) + '/' + $n + ')')
        Log "Enabling: $($f.Name)" 'INFO' 'Features'
        if ($f.Cmd) {
            $code = Invoke-External $f.Cmd.File $f.Cmd.Arg
            Log "  exit code $code" 'INFO' 'Features'
        } else {
            foreach ($nm in $f.Features) {
                if ($script:CancelRequested) { throw 'Cancelled by user' }
                $code = Invoke-External 'dism.exe' "/Online /Enable-Feature /FeatureName:$nm /All /NoRestart /Quiet"
                Log "  $nm -> exit code $code (3010 = restart required)" 'INFO' 'Features'
                if ($code -eq 3010) { $script:FeatPending[$f.Name] = $true }
            }
        }
        $i++
    }
    $script:SegStart = -1.0
    Log 'Done. Restart Windows to finish enabling features.' 'INFO' 'Features'
    Update-FeatureStates
} 'Enabling features...' })

foreach ($f in $Fixes) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $f.Name; $b.Tag = $f
    $b.Add_Click({
        param($s, $e)
        $act = $s.Tag.Action
        Start-Work { & $act }
    })
    [void]$fixesPanel.Children.Add($b)
}
foreach ($p in $LegacyPanels) {
    $b = New-Object System.Windows.Controls.Button
    $b.Content = $p.Name; $b.Tag = $p
    $b.Add_Click({
        param($s, $e)
        $pp = $s.Tag
        try {
            if ($pp.Arg) { Start-Process $pp.File -ArgumentList $pp.Arg } else { Start-Process $pp.File }
        } catch { Log "Could not open $($pp.Name): $($_.Exception.Message)" 'ERROR' 'Config' }
    })
    [void]$panelsPanel.Children.Add($b)
}
# ------------------------------------------------------------------
# Windows activation check (status, edition, product key)
# ------------------------------------------------------------------
$script:ActJob = {
    $o = @{ Ok = $false; Edition = ''; Status = -1; Name = ''; Desc = ''; Channel = ''; Partial = ''; Grace = 0; BiosKey = ''; RegKey = ''; Err = '' }
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
        $dv = [string]$cv.DisplayVersion
        if (-not $dv) { $dv = [string]$cv.ReleaseId }
        $extra = ''
        if ($dv) { $extra = $dv + ', ' }
        $o.Edition = ([string]$os.Caption).Trim() + '   (' + $extra + 'build ' + [string]$os.BuildNumber + ')'
    } catch { }
    try {
        $list = @(Get-CimInstance -ClassName SoftwareLicensingProduct -Filter "ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL" -ErrorAction Stop)
        $p = $list | Where-Object { [string]$_.Name -notmatch 'ESU|add-on' } | Select-Object -First 1
        if (-not $p -and $list.Count -gt 0) { $p = $list[0] }
        if ($p) {
            $o.Ok = $true
            $o.Status = [int]$p.LicenseStatus; $o.Name = [string]$p.Name; $o.Desc = [string]$p.Description
            $o.Channel = [string]$p.ProductKeyChannel; $o.Partial = [string]$p.PartialProductKey; $o.Grace = [int]$p.GracePeriodRemaining
        } else { $o.Err = 'No Windows license was found on this PC.' }
    } catch { $o.Err = $_.Exception.Message }
    try { $o.BiosKey = ([string](Get-CimInstance -ClassName SoftwareLicensingService -ErrorAction Stop).OA3xOriginalProductKey).Trim() } catch { }
    try {
        $dpid = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop).DigitalProductId
        if ($dpid -and $dpid.Count -ge 67) {
            $map = 'BCDFGHJKMPQRTVWXY2346789'
            $v = [byte[]]$dpid.Clone()
            $isWin8 = [int][math]::Floor($v[66] / 6) -band 1
            $v[66] = [byte](($v[66] -band 0xF7) -bor (($isWin8 -band 2) * 4))
            $key = ''; $last = 0
            for ($i = 24; $i -ge 0; $i--) {
                $cur = 0
                for ($j = 14; $j -ge 0; $j--) {
                    $cur = $cur * 256 + $v[$j + 52]
                    $v[$j + 52] = [byte][math]::Floor($cur / 24)
                    $cur = $cur % 24
                }
                $key = [string]$map[$cur] + $key; $last = $cur
            }
            if ($isWin8 -eq 1) { $r = $key.Substring(1); $key = $r.Substring(0, $last) + 'N' + $r.Substring($last) }
            $o.RegKey = ($key -replace '(.{5})(?=.)', '$1-')
        }
    } catch { }
    $o
}

function Start-ActivationJob {
    $ui = $script:ActUi
    $ui.Head.Text = 'Checking Windows activation...'
    $ui.Sub.Text = ''
    $ui.Copy.IsEnabled = $false; $ui.Again.IsEnabled = $false
    foreach ($t in $ui.Vals) { $t.Text = '...' }
    $ui.Note.Visibility = 'Collapsed'
    try {
        $script:ActPs = [powershell]::Create()
        [void]$script:ActPs.AddScript($script:ActJob.ToString())
        $script:ActHandle = $script:ActPs.BeginInvoke()
    } catch { $ui.Head.Text = 'Could not start the check'; $ui.Sub.Text = $_.Exception.Message; $ui.Again.IsEnabled = $true; return }
    if ($script:ActTimer) { $script:ActTimer.Stop() }
    $script:ActTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:ActTimer.Interval = [TimeSpan]::FromMilliseconds(300)
    $script:ActTimer.Add_Tick({
        if (-not $script:ActHandle -or -not $script:ActHandle.IsCompleted) { return }
        $script:ActTimer.Stop()
        $res = $null
        try { $res = @($script:ActPs.EndInvoke($script:ActHandle))[0] } catch { }
        try { $script:ActPs.Dispose() } catch { }
        Set-ActivationUi $res
    })
    $script:ActTimer.Start()
}

function Set-ActivationUi {
    param($r)
    $ui = $script:ActUi
    if (-not $ui) { return }
    $ui.Again.IsEnabled = $true
    $green = '<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="46" Height="46"><Ellipse Fill="#3DDC84"/><Path Data="M13,24 L20,31 L33,16" Stroke="#1F2329" StrokeThickness="3.4" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Grid>'
    $icon = $green
    if (-not $r -or -not ($r -is [hashtable])) {
        $ui.Head.Text = 'Could not read the license status'; $ui.Sub.Text = 'Try again in a moment.'
        $icon = Get-MsgIconXaml 'Warning'
    } else {
        $stNames = @{ 0 = 'Unlicensed'; 1 = 'Licensed'; 2 = 'Initial grace period'; 3 = 'Additional grace period'; 4 = 'Non-genuine grace period'; 5 = 'Notification mode'; 6 = 'Extended grace period' }
        $report = New-Object System.Collections.Generic.List[string]
        if ($r.Ok -and [int]$r.Status -eq 1) {
            $ui.Head.Text = 'Windows is activated'
            $ui.Sub.Text = 'License status: Licensed'
            if (([string]$r.Channel + [string]$r.Desc) -match 'Volume|KMS|GVLK' -and [int]$r.Grace -gt 0) {
                $ui.Sub.Text = 'Volume (KMS) activation - about ' + [int]([int]$r.Grace / 1440) + ' day(s) left until the next automatic renewal.'
            }
        } elseif ($r.Ok) {
            $ui.Head.Text = 'Windows is NOT activated'
            $nm = [string]$stNames[[int]$r.Status]; if (-not $nm) { $nm = 'Unknown' }
            $ui.Sub.Text = 'License status: ' + $nm
            $icon = Get-MsgIconXaml 'Error'
        } else {
            $ui.Head.Text = 'Could not read the license status'
            $ui.Sub.Text = [string]$r.Err
            $icon = Get-MsgIconXaml 'Warning'
        }
        $type = [string]$r.Channel; if (-not $type) { $type = [string]$r.Desc }
        $last5 = ''
        if ($r.Partial) { $last5 = 'XXXXX-XXXXX-XXXXX-XXXXX-' + [string]$r.Partial }
        $bios = [string]$r.BiosKey; if (-not $bios) { $bios = 'None stored in the BIOS / UEFI' }
        $reg = [string]$r.RegKey; $regMismatch = $false
        if (-not $reg) { $reg = 'Not available' }
        elseif ($r.Partial -and -not $reg.EndsWith([string]$r.Partial)) { $reg = $reg + '   (generic - not the active key)'; $regMismatch = $true }
        $vals = @([string]$r.Edition, [string]$r.Name, $type, $last5, $bios, $reg)
        for ($i = 0; $i -lt $ui.Vals.Count; $i++) { if (-not $vals[$i]) { $vals[$i] = '-' }; $ui.Vals[$i].Text = $vals[$i] }
        $script:ActReport = "Windows activation`r`n" + $ui.Head.Text + "`r`n" + $ui.Sub.Text + "`r`n"
        for ($i = 0; $i -lt $ui.Labels.Count; $i++) { $script:ActReport += $ui.Labels[$i] + ': ' + $vals[$i] + "`r`n" }
        $ui.Copy.IsEnabled = $true
        if ($r.Ok -and (-not [string]$r.BiosKey) -and ($regMismatch -or -not [string]$r.RegKey)) {
            $ui.Note.Text = 'Windows 10 / 11 usually uses a digital license linked to this PC or your Microsoft account, so the full key often cannot be read back. The last 5 characters above identify the active key.'
            $ui.Note.Visibility = 'Visible'
        }
    }
    try {
        $ui.Icon.Children.Clear()
        [void]$ui.Icon.Children.Add([Windows.Markup.XamlReader]::Parse((Convert-XamlTheme -Text $icon -Literal)))
    } catch { }
    Log ($ui.Head.Text) 'INFO' 'Activation'
}

function Show-ActivationCheck {
    $xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Rock Toolbox" SizeToContent="WidthAndHeight" ResizeMode="NoResize" ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner" Background="#1F2329" FontFamily="Segoe UI" FontSize="13" MinWidth="600" MaxWidth="760">
  <Window.Resources>
__STYLES__
  </Window.Resources>
  <Border BorderBrush="#2B3038" BorderThickness="1" Padding="24,22">
    <StackPanel>
      <DockPanel LastChildFill="True">
        <Grid x:Name="actIcon" DockPanel.Dock="Left" Width="46" Height="46" VerticalAlignment="Top" Margin="0,0,16,0"/>
        <StackPanel VerticalAlignment="Center">
          <TextBlock x:Name="actHead" FontSize="17" FontWeight="Bold" Foreground="White" TextWrapping="Wrap"/>
          <TextBlock x:Name="actSub" Foreground="#9AA0B8" TextWrapping="Wrap" Margin="0,2,0,0"/>
        </StackPanel>
      </DockPanel>
      <Border Background="#1B1F24" BorderBrush="#2B3038" BorderThickness="1" CornerRadius="6" Margin="0,16,0,0" Padding="14,10">
        <Grid x:Name="actGrid"/>
      </Border>
      <TextBlock x:Name="actNote" Foreground="#8D93AD" FontSize="11.5" TextWrapping="Wrap" MaxWidth="640" Margin="2,10,0,0" Visibility="Collapsed"/>
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,18,0,0">
        <Button x:Name="actCopy" Style="{StaticResource DiaBtn2}" Content="Copy details" Cursor="Hand"/>
        <Button x:Name="actAgain" Style="{StaticResource DiaBtn2}" Content="Check again" Margin="10,0,0,0" Cursor="Hand"/>
        <Button x:Name="actClose" Style="{StaticResource DiaBtn}" Content="Close" IsDefault="True" IsCancel="True" Margin="10,0,0,0" Cursor="Hand"/>
      </StackPanel>
    </StackPanel>
  </Border>
</Window>
'@
    $xaml = $xaml.Replace('__STYLES__', $script:DiaStylesXaml)
    $xaml = Add-RockDialogChrome -Text $xaml -Title 'Windows Activation'
    $xaml = Convert-XamlTheme -Text $xaml -Literal
    $w = [Windows.Markup.XamlReader]::Parse($xaml)
    Initialize-RockDialogClose $w
    $script:ActWin = $w
    $grid = $w.FindName('actGrid')
    $c1 = New-Object System.Windows.Controls.ColumnDefinition; $c1.Width = [System.Windows.GridLength]::Auto
    $c2 = New-Object System.Windows.Controls.ColumnDefinition; $c2.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
    [void]$grid.ColumnDefinitions.Add($c1); [void]$grid.ColumnDefinitions.Add($c2)
    $labels = @('Windows edition', 'License', 'License type', 'Active key (last 5)', 'Key in BIOS / UEFI', 'Installed key (registry)')
    $valBoxes = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $labels.Count; $i++) {
        $rd = New-Object System.Windows.Controls.RowDefinition; $rd.Height = [System.Windows.GridLength]::Auto
        [void]$grid.RowDefinitions.Add($rd)
        $l = New-Object System.Windows.Controls.TextBlock
        $l.Text = $labels[$i]; $l.Margin = '0,5,26,5'; $l.Foreground = Get-Brush '#9AA0B8'
        [System.Windows.Controls.Grid]::SetRow($l, $i); [System.Windows.Controls.Grid]::SetColumn($l, 0)
        $v = New-Object System.Windows.Controls.TextBlock
        $v.Margin = '0,5,0,5'; $v.Foreground = Get-Brush '#E6E8F2'; $v.TextWrapping = 'Wrap'; $v.MaxWidth = 430; $v.FontWeight = 'SemiBold'
        [System.Windows.Controls.Grid]::SetRow($v, $i); [System.Windows.Controls.Grid]::SetColumn($v, 1)
        [void]$grid.Children.Add($l); [void]$grid.Children.Add($v); [void]$valBoxes.Add($v)
    }
    $script:ActUi = @{
        Icon = $w.FindName('actIcon'); Head = $w.FindName('actHead'); Sub = $w.FindName('actSub'); Note = $w.FindName('actNote')
        Copy = $w.FindName('actCopy'); Again = $w.FindName('actAgain'); Vals = @($valBoxes); Labels = $labels
    }
    $script:ActReport = ''
    $script:ActUi.Copy.Add_Click({ try { [System.Windows.Clipboard]::SetText([string]$script:ActReport); Log 'Activation details copied to the clipboard.' 'INFO' 'Activation' } catch { Log ('Could not copy: ' + $_.Exception.Message) 'WARN' 'Activation' } })
    $script:ActUi.Again.Add_Click({ Start-ActivationJob })
    $w.FindName('actClose').Add_Click({ $script:ActWin.Close() })
    $w.Add_Closed({ try { if ($script:ActTimer) { $script:ActTimer.Stop() } } catch { }; $script:ActUi = $null })
    if (Test-Rtl) { $w.FlowDirection = 'RightToLeft' }
    try { $w.Owner = $window; if ($window.Icon) { $w.Icon = $window.Icon } } catch { }
    Start-ActivationJob
    [void]$w.ShowDialog()
}
$btnActCheck.Add_Click({ Show-ActivationCheck })

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


>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
