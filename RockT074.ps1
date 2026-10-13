$btnDebInst.Add_Click({ Start-Work {
    Log 'Checking which of these apps are installed...'
    $names = @(Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $n = 0
    foreach ($cb in $bloatBoxes) {
        $pat = [string]$cb.Tag.Name
        $hit = (@($names | Where-Object { $_ -like $pat }).Count -gt 0)
        $cb.IsChecked = $hit
        if ($hit) { $n++ }
    }
    Log "Selected $n installed app(s)."
} 'Checking installed apps...' })
