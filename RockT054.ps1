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

# ------------------------------------------------------------------
# Build UI: Config
