function Set-ChipLook {
    foreach ($c in $script:Chips) {
        if ([string]$c.Tag -eq $script:CatFilter) { Set-Res $c 'Background' '#4A78B0'; Set-Res $c 'Foreground' '#FFFFFF' } else { Set-Res $c 'Background' '#1E3547'; Set-Res $c 'Foreground' '#F0F4FF' }
    }
}
foreach ($cat in (@('All') + @($Apps | ForEach-Object { $_.Cat } | Select-Object -Unique))) {
    $chip = New-Object System.Windows.Controls.Button
    $chip.Content = $cat; $chip.Tag = $cat; $chip.Padding = '12,4'; $chip.Margin = '0,0,6,6'
    $chip.Add_Click({
        param($s, $e)
        $script:CatFilter = [string]$s.Tag
        Set-ChipLook
        Apply-AppFilter
    })
    [void]$chipPanel.Children.Add($chip)
    [void]$script:Chips.Add($chip)
}
Set-ChipLook
$txtAppSearch.Add_TextChanged({ Apply-AppFilter })

$btnInstall.Add_Click({ Start-Work {
    $ids = @($script:Selected.Keys)
    if ($ids.Count -eq 0) { Log 'No apps selected.' 'WARN' 'Winget'; return }
    Invoke-Winget 'install' $ids
    Log 'Install queue finished.' 'INFO' 'Winget'
} })
