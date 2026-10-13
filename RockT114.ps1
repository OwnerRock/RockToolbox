$lvFind.Add_MouseDoubleClick({
    $it = $lvFind.SelectedItem
    if ($it) {
        try { [System.Windows.Clipboard]::SetText($it.Id); Log ('Copied ID: ' + $it.Id) 'INFO' 'Find' } catch { }
    }
})
