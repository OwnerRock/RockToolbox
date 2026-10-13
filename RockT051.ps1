function Set-CursorRegistry {
    param($Values, [string]$Scheme, [int]$Source, [string]$SchemeList)
    $key = 'HKCU:\Control Panel\Cursors'
    if (-not (Test-Path -LiteralPath $key)) { New-Item -Path $key -Force | Out-Null }
    Set-ItemProperty -LiteralPath $key -Name '(default)' -Value $Scheme
    foreach ($n in $Values.Keys) {
        New-ItemProperty -LiteralPath $key -Name $n -Value $Values[$n] -PropertyType ExpandString -Force | Out-Null
    }
    New-ItemProperty -LiteralPath $key -Name 'Scheme Source' -Value $Source -PropertyType DWord -Force | Out-Null
    if ($SchemeList) {
        $sk = "$key\Schemes"
        if (-not (Test-Path -LiteralPath $sk)) { New-Item -Path $sk -Force | Out-Null }
        New-ItemProperty -LiteralPath $sk -Name $Scheme -Value $SchemeList -PropertyType ExpandString -Force | Out-Null
    }
}

# ------------------------------------------------------------------
# Rock icon file + shortcut (a .bat can't carry an icon, a .lnk can)
