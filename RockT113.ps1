$cmbFindSource.Add_SelectionChanged({ Update-FindList })

$btnFindCopy.Add_Click({
    $it = $lvFind.SelectedItem
    if (-not $it) { Log 'Select an app first.' 'WARN' 'Find'; return }
    try { [System.Windows.Clipboard]::SetText($it.Id); Log ('Copied ID: ' + $it.Id) 'INFO' 'Find' }
    catch { Log ('Could not copy: ' + $_.Exception.Message) 'ERROR' 'Find' }
})
