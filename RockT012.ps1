function Remove-Reg {
    param([string]$Path, [string]$Name)
    Remove-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction SilentlyContinue
}

# runs a program hidden; while it runs the progress bar creeps through the current segment ($script:SegStart..SegEnd)
function Invoke-External {
    param([string]$File, [string]$ArgString, [string]$OutFile)
    if ($script:CancelRequested) { throw 'Cancelled by user' }
    $sp = @{ FilePath = $File; ArgumentList = $ArgString; WindowStyle = 'Hidden'; PassThru = $true }
    if ($OutFile) { $sp['RedirectStandardOutput'] = $OutFile }
    $p = Start-Process @sp
    $null = $p.Handle
    $t0 = Get-Date
    $lastSet = [DateTime]::MinValue
    while (-not $p.HasExited) {
        if ($script:CancelRequested) {
            try { $p.Kill() } catch { }
            try { $p.WaitForExit(2000) } catch { }
            throw 'Cancelled by user'
        }
        DoEvents
        Start-Sleep -Milliseconds 16
        if ($script:SegStart -ge 0 -and ([DateTime]::Now - $lastSet).TotalMilliseconds -ge 200) {
            $lastSet = [DateTime]::Now
            $el = ((Get-Date) - $t0).TotalSeconds
            $frac = 1 - [Math]::Exp(-$el / 25.0)
            Set-Progress ($script:SegStart + ($script:SegEnd - $script:SegStart) * $frac * 0.92)
        }
    }
    return $p.ExitCode
}

function Get-RemoteFile {
    param([string]$Url, [string]$Path)
    if ($script:CancelRequested) { throw 'Cancelled by user' }
    $wc = New-Object System.Net.WebClient
    $wc.Headers['User-Agent'] = 'Mozilla/5.0 RockToolbox'
    $wc.Add_DownloadProgressChanged({
        param($s, $e)
        try { Set-Progress ([double]$e.ProgressPercentage) ('Downloading... ' + [int]($e.BytesReceived / 1KB) + ' KB') } catch { }
    })
    $task = $wc.DownloadFileTaskAsync($Url, $Path)
    while (-not $task.IsCompleted) {
        if ($script:CancelRequested) {
            try { $wc.CancelAsync() } catch { }
            $wc.Dispose()
            throw 'Cancelled by user'
        }
        DoEvents
        Start-Sleep -Milliseconds 16
    }
    $wc.Dispose()
    if ($task.IsFaulted) { throw $task.Exception.InnerException }
}

function Get-Winget {
    $w = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($w) { return $w.Source }
    return $null
}

# runs winget inside its own hidden console that is 400 columns wide, so table columns (IDs) are never truncated
