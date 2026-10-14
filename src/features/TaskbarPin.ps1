# Module: features/TaskbarPin - Pin / unpin to taskbar
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- taskbar pin (checkbox reflects the real state)
$script:TaskbarDir = Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'

function Get-RockPinnedLinks {
    $res = @()
    if (-not (Test-Path -LiteralPath $script:TaskbarDir)) { return $res }
    try {
        $ws = New-Object -ComObject WScript.Shell
        foreach ($f in @(Get-ChildItem -LiteralPath $script:TaskbarDir -Filter '*.lnk' -ErrorAction SilentlyContinue)) {
            $hit = ($f.BaseName -like '*Rock*Toolbox*')
            if (-not $hit) {
                try {
                    $l = $ws.CreateShortcut($f.FullName)
                    if ($RockScriptPath -and ([string]$l.Arguments).IndexOf($RockScriptPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $hit = $true }
                } catch { }
            }
            if ($hit) { $res += $f.FullName }
        }
    } catch { }
    return $res
}

function Update-TaskbarCheck {
    $canPin = [bool]($RockScriptPath -and (Test-Path -LiteralPath $RockScriptPath))
    $chkTaskbar.IsEnabled = $canPin
    if ($canPin) { $chkTaskbar.ToolTip = $null } else { $chkTaskbar.ToolTip = 'Run Rock Toolbox from the saved .ps1 file to use this.' }
    $chkTaskbar.IsChecked = (@(Get-RockPinnedLinks).Count -gt 0)
}

function Set-LnkAppId([string]$Path, [string]$AppId) {
    if (-not ('RockLnkProps' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class RockLnkProps {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PropertyKey { public Guid fmtid; public uint pid; }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PropVariant {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr ptr;
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    interface IPropertyStore {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHGetPropertyStoreFromParsingName(string path, IntPtr pbc, int flags, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant pv);
    public static void SetAppId(string lnkPath, string appId) {
        Guid iid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        IPropertyStore store;
        SHGetPropertyStoreFromParsingName(lnkPath, IntPtr.Zero, 2, ref iid, out store);
        PropertyKey key = new PropertyKey();
        key.fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        key.pid = 5;
        PropVariant pv = new PropVariant();
        pv.vt = 31;
        pv.ptr = Marshal.StringToCoTaskMemUni(appId);
        try {
            int hr = store.SetValue(ref key, ref pv);
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);
            hr = store.Commit();
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);
        } finally {
            PropVariantClear(ref pv);
            Marshal.ReleaseComObject(store);
        }
    }
}
'@
    }
    [RockLnkProps]::SetAppId($Path, $AppId)
}

# runs the shell's own "Pin to taskbar" / "Unpin from taskbar" handler on a .lnk file
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

