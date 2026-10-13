$Url = 'https://raw.githubusercontent.com/Spidermooon/RockToolbox/main/RockToolbox.ps1'

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$dir = Join-Path $env:LOCALAPPDATA 'RockToolbox'
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$dst = Join-Path $dir 'RockToolbox.ps1'

Write-Host 'Downloading Rock Toolbox...' -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri $Url -OutFile $dst -UseBasicParsing
} catch {
    if (Test-Path -LiteralPath $dst) {
        Write-Host 'Could not download - starting the last saved copy.' -ForegroundColor Yellow
    } else {
        Write-Host ('Download failed: ' + $_.Exception.Message) -ForegroundColor Red
        return
    }
}

Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-File', ('"' + $dst + '"')
)
