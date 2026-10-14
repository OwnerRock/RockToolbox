# Module: features/Cursors - Mouse cursor apply / restore
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.


$CursorSchemeName = 'Windows 11 Cursors Dark by Jepri Creations'
$CursorDirName    = 'Windows_11_dark'
# registry value -> file inside the pack (same mapping as the original Install.inf)
$CursorMap = [ordered]@{
    Arrow='pointer.cur'; Help='help.cur'; AppStarting='working.ani'; Wait='busy.ani'
    crosshair='precision.cur'; precisionhair='precision.cur'; IBeam='beam.cur'; NWPen='handwriting.cur'
    No='unavailable.cur'; SizeNS='vert.cur'; SizeWE='horz.cur'; SizeNWSE='dgn1.cur'; SizeNESW='dgn2.cur'
    SizeAll='move.cur'; UpArrow='alternate.cur'; Hand='link.cur'; Person='person.cur'; Pin='pin.cur'
}
# order Windows uses for the "Schemes" entry
$CursorSchemeOrder = 'pointer.cur','help.cur','working.ani','busy.ani','precision.cur','beam.cur','handwriting.cur','unavailable.cur','vert.cur','horz.cur','dgn1.cur','dgn2.cur','move.cur','alternate.cur','link.cur','person.cur','pin.cur'

Add-Type -MemberDefinition '[DllImport("user32.dll",SetLastError=true)] public static extern bool SystemParametersInfo(uint a,uint b,IntPtr c,uint d);' -Name Cur -Namespace RockNative -ErrorAction SilentlyContinue
function Update-SystemCursors {
    # SPI_SETCURSORS (0x57) + SPIF_UPDATEINIFILE|SPIF_SENDCHANGE (3): reload cursors without logging off
    [void][RockNative.Cur]::SystemParametersInfo(0x57, 0, [IntPtr]::Zero, 3)
}
function Set-CursorRegistry {
    param($Values, [string]$Scheme, [int]$Source, [string]$SchemeList)
    $key = 'HKCU:\Control Panel\Cursors'
    if (-not (Test-Path -LiteralPath $key)) { New-Item -Path $key -Force | Out-Null }
    Set-ItemProperty -LiteralPath $key -Name '(default)' -Value $Scheme
    foreach ($n in $Values.Keys) {
        New-ItemProperty -LiteralPath $key -Name $n -Value $Values[$n] -PropertyType ExpandString -Force | Out-Null
    }
    New-ItemProperty -LiteralPath $key -Name 'Scheme Source' -Value $Source -PropertyType DWord -Force | Out-Null
    if ($SchemeList) {
        $sk = "$key\Schemes"
        if (-not (Test-Path -LiteralPath $sk)) { New-Item -Path $sk -Force | Out-Null }
        New-ItemProperty -LiteralPath $sk -Name $Scheme -Value $SchemeList -PropertyType ExpandString -Force | Out-Null
    }
}

