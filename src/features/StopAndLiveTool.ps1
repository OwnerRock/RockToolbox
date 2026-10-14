# Module: features/StopAndLiveTool - Global Stop button + Invoke-LiveTool
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ---- (8) Stop button in the bottom bar (works for every tab) ----
$btnStopGlobal.Add_Click({
    if (-not $script:Busy) { return }
    $script:CancelRequested = $true
    Log 'Stop requested - cancelling current action...' 'WARN' 'UI'
})

# runs a console tool and streams its output; $OnText gets every chunk. Honors the Stop button.
function Invoke-LiveTool {
    param([string]$File, [string]$ToolArgs, [string]$Encoding = 'OEM', [scriptblock]$OnText)
    if ($script:CancelRequested) { throw 'Cancelled by user' }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $File
    $psi.Arguments = $ToolArgs
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    if ($Encoding -eq 'Unicode') { $psi.StandardOutputEncoding = [System.Text.Encoding]::Unicode }
    else {
        $cp = [System.Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage
        try { $psi.StandardOutputEncoding = [System.Text.Encoding]::GetEncoding($cp) } catch { }
    }
    $p = [System.Diagnostics.Process]::Start($psi)
    $errTask = $p.StandardError.ReadToEndAsync()
    $buf = New-Object 'char[]' 4096
    $task = $p.StandardOutput.ReadAsync($buf, 0, $buf.Length)
    while ($true) {
        if ($script:CancelRequested) {
            try { & taskkill.exe /PID $p.Id /T /F 2>&1 | Out-Null } catch { }
            try { $p.WaitForExit(3000) | Out-Null } catch { }
            throw 'Cancelled by user'
        }
        if ($task.IsCompleted) {
            $n = [int]$task.Result
            if ($n -le 0) { break }
            if ($OnText) { $null = & $OnText ([string]::new($buf, 0, $n)) }
            $task = $p.StandardOutput.ReadAsync($buf, 0, $buf.Length)
        } else {
            DoEvents
            Start-Sleep -Milliseconds 30
        }
    }
    $p.WaitForExit()
    return [int]$p.ExitCode
}

