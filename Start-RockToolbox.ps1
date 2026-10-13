$Url = 'https://raw.githubusercontent.com/Spidermooon/RockToolbox/main/RockToolbox.ps1'

$inner = @'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$d = Join-Path $env:LOCALAPPDATA 'RockToolbox'
New-Item -ItemType Directory -Path $d -Force | Out-Null
$p = Join-Path $d 'RockToolbox.ps1'
try { Invoke-WebRequest -Uri '{URL}' -OutFile $p -UseBasicParsing } catch { }
if (Test-Path -LiteralPath $p) { & $p } else { Write-Host 'Could not download Rock Toolbox - check your internet.'; Read-Host 'Press Enter to close' }
'@.Replace('{URL}', $Url)

$enc = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($inner))
Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-EncodedCommand', $enc
