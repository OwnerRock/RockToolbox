try {
    if (-not ('RockScroll' -as [type])) { Add-Type -TypeDefinition $scrollCode -ReferencedAssemblies 'PresentationFramework', 'PresentationCore', 'WindowsBase' }
    [RockScroll]::Attach($window)
    $script:ScrollOk = $true
} catch { Log ('Smooth scrolling is unavailable: ' + $_.Exception.Message) 'WARN' 'Settings' }

function Apply-Scroll {
    if (-not $script:ScrollOk) { return }
    $m = $script:ScrollModes[[string]$script:Settings['ScrollAnim']]
    if ($null -eq $m) { $m = 1 }
    $sp = $script:ScrollSpeeds[[string]$script:Settings['ScrollSpeed']]
    if ($null -eq $sp) { $sp = 1.0 }
    [RockScroll]::Mode = [int]$m
    [RockScroll]::Speed = [double]$sp
}

foreach ($t in 'Off', 'Smooth', 'Glide', 'Snappy', 'Bouncy') { [void]$cmbScrollAnim.Items.Add($t) }
