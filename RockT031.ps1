$btnShowInstalled.Add_Click({ Start-Work {
    $wg = Get-Winget
    if (-not $wg) { Log 'winget not found.' 'WARN' 'Winget'; return }
    Log 'Scanning installed apps (winget list)...' 'INFO' 'Winget'
    $tmp = Join-Path $env:TEMP 'rock_winget_list.txt'
    [void](Invoke-WingetTable 'list --accept-source-agreements' $tmp)
    $txt = ''
    if (Test-Path $tmp) { $txt = Get-Content $tmp -Raw -ErrorAction SilentlyContinue }
    $n = 0
    foreach ($it in $script:AppCards) {
        $hit = $false
        if ($txt) { $hit = [bool]($txt -match [regex]::Escape($it.App.Id)) }
        if ($hit) { $it.App.Badge.Visibility = 'Visible'; $n++ } else { $it.App.Badge.Visibility = 'Collapsed' }
    }
    Log "$n of the listed apps are already installed (marked in green)." 'INFO' 'Winget'
} })

# ------------------------------------------------------------------
# Build UI: Tweaks + Customize
