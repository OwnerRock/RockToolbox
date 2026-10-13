$btnUltOff.Add_Click({ Start-Work {
    powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e | Out-Null
    $list = powercfg -list | Out-String
    foreach ($line in ($list -split "`r?`n")) {
        if ($line -match 'Ultimate Performance') {
            $g = [regex]::Match($line, $guidRx).Value
            if ($g) { powercfg -delete $g | Out-Null }
        }
    }
    Log 'Back to Balanced; Ultimate Performance plan removed.' 'INFO' 'Power'
} })

$DnsMap = [ordered]@{
    'Cloudflare (1.1.1.1)' = @('1.1.1.1', '1.0.0.1')
    'Google (8.8.8.8)'     = @('8.8.8.8', '8.8.4.4')
    'Quad9 (9.9.9.9)'      = @('9.9.9.9', '149.112.112.112')
    'AdGuard (blocks ads)' = @('94.140.14.14', '94.140.15.15')
    'Automatic (DHCP)'     = $null
}
