$btnWUAutoOn.Add_Click({ Set-WUAuto $true })

$btnWUScan.Add_Click({ Scan-WinUpdates })
