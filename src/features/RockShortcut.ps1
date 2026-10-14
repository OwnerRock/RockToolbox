# Module: features/RockShortcut - Rock icon file + desktop shortcut
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Rock icon file + shortcut (a .bat can't carry an icon, a .lnk can)
# ------------------------------------------------------------------
$RockScriptPath = $script:RockMain      # path of RockToolbox.ps1 (not of this module)
function Save-RockIcon {
    param([string]$Path)
    $sizes = 16, 32, 48, 64, 128, 256
    $pngs = New-Object System.Collections.ArrayList
    foreach ($sz in $sizes) {
        $bmp = New-RockBitmap $sz
        $m = New-Object System.IO.MemoryStream
        $bmp.Save($m, [System.Drawing.Imaging.ImageFormat]::Png)
        [void]$pngs.Add($m.ToArray())
        $m.Dispose(); $bmp.Dispose()
    }
    $fs = [System.IO.File]::Create($Path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    try {
        $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
            $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
            $bw.Write([uint16]1); $bw.Write([uint16]32)
            $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
            $offset += $pngs[$i].Length
        }
        foreach ($b in $pngs) { $bw.Write([byte[]]$b) }
    } finally { $bw.Dispose(); $fs.Dispose() }
}
function New-RockShortcut {
    param([string]$Folder, [string]$Icon)
    $ws  = New-Object -ComObject WScript.Shell
    $lnk = $ws.CreateShortcut((Join-Path $Folder 'Rock Toolbox.lnk'))
    $lnk.TargetPath       = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $lnk.Arguments        = "-NoProfile -ExecutionPolicy Bypass -STA -File `"$RockScriptPath`""
    $lnk.WorkingDirectory = Split-Path -Parent $RockScriptPath
    $lnk.IconLocation     = "$Icon,0"
    $lnk.Description      = 'Rock Toolbox'
    $lnk.Save()
    return $lnk.FullName
}

