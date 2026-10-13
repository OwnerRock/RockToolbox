$script:CancelRequested = $false
$script:SegStart = -1.0
$script:SegEnd = -1.0
function Start-Work {
    param([scriptblock]$Block, [string]$Title = 'Working...')
    if ($script:Busy) { Log 'Another task is still running, please wait...' 'WARN' 'Task'; return }
    $script:Busy = $true
    $script:CancelRequested = $false
    $script:SegStart = -1.0; $script:SegEnd = -1.0
    $window.Cursor = [System.Windows.Input.Cursors]::Wait
    $pbMain.Tag = 'busy'
    Set-Res $txtStatus 'Foreground' '#C9CDE0'
    Set-Progress -1 $Title
    $ok = $true
    $cancelled = $false
    try { & $Block }
    catch {
        if ($_.Exception.Message -match 'Cancelled by user|cancelled by user') {
            $cancelled = $true
            $ok = $false
            Log 'Action stopped by user.' 'WARN' 'Task'
        } else {
            $ok = $false
            Log $_.Exception.Message 'ERROR' 'Task'
        }
    }
    finally {
        $script:Busy = $false
        $script:CancelRequested = $false
        $script:SegStart = -1.0; $script:SegEnd = -1.0
        $window.Cursor = $null
        $pbMain.Tag = $null
    }
    if ($ok) {
        $t = $null
        if ($txtStatus.Text -eq $Title) { $t = 'Done' }
        Set-Progress 100 $t
    } else {
        $pbMain.IsIndeterminate = $false; Set-PbValue 0 -Instant; $txtPct.Text = ''
        if ($cancelled) { $txtStatus.Text = 'Stopped'; Set-Res $txtStatus 'Foreground' '#FFB454' }
    }
}

function Set-Reg {
    param([string]$Path, [string]$Name, $Value, [string]$Type = 'DWord')
    if (-not (Test-Path -LiteralPath $Path)) { New-Item -Path $Path -Force | Out-Null }
    New-ItemProperty -LiteralPath $Path -Name $Name -Value $Value -PropertyType $Type -Force | Out-Null
}
