$btnUltOn.Add_Click({ Start-Work {
    $o = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 | Out-String
    $m = [regex]::Match($o, $guidRx)
    if ($m.Success) { powercfg -setactive $m.Value | Out-Null; Log 'Ultimate Performance plan enabled.' 'INFO' 'Power' }
    else { Log 'Could not create the Ultimate Performance plan on this edition.' 'WARN' 'Power' }
} })
