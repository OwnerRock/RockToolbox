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
