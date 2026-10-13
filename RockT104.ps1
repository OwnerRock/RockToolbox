$btnWpDailyOn.Add_Click({ Start-Work {
    $script = Join-Path $AppDir 'DailyBing.ps1'
    $body = @'
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'
$dir = Join-Path $env:LOCALAPPDATA 'RockToolbox\Wallpapers'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$file = Join-Path $dir 'bing_daily.jpg'
for ($i = 0; $i -lt 5; $i++) {
    try {
        $r = Invoke-RestMethod 'https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US' -TimeoutSec 25
        $url = 'https://www.bing.com' + $r.images[0].urlbase + '_UHD.jpg'
        Invoke-WebRequest $url -OutFile $file -UseBasicParsing -TimeoutSec 60
        break
    } catch { Start-Sleep -Seconds 20 }
}
if (Test-Path $file) {
    Add-Type -MemberDefinition '[DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int SystemParametersInfo(int a,int b,string c,int d);' -Name WP -Namespace W
    [void][W.WP]::SystemParametersInfo(20, 0, $file, 3)
}
'@
    Set-Content -Path $script -Value $body -Encoding UTF8
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`""
    $t1 = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
    $t2 = New-ScheduledTaskTrigger -Daily -At '9:00AM'
    $set = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    $pri = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger @($t1, $t2) -Settings $set -Principal $pri -Force | Out-Null
    Start-ScheduledTask -TaskName $TaskName
    Log 'Daily Bing wallpaper enabled (runs at logon and every day at 9:00 AM, even when this tool is closed).'
} })
