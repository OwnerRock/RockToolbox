$btnDns.Add_Click({ Start-Work {
    $name = [string]$cmbDns.SelectedItem
    $addr = $DnsMap[$name]
    Log "Setting DNS: $name" 'INFO' 'DNS'
    foreach ($a in @(Get-NetAdapter | Where-Object { $_.Status -eq 'Up' })) {
        if ($addr) { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses $addr }
        else       { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses }
        Log "  adapter: $($a.Name)" 'INFO' 'DNS'
    }
    Clear-DnsClientCache
    Log 'DNS updated and cache flushed.' 'INFO' 'DNS'
} })

# ------------------------------------------------------------------
# DATA: Mouse cursor pack (Windows 11 Cursors Concept Dark by JepriCreations)
