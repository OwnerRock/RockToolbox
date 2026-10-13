$cmbFindSource.SelectedIndex = 0

function Update-FindList {
    $q = $txtFindQuery.Text.Trim()
    $cat = [string]$cmbFindSource.SelectedItem
    $view = New-Object 'System.Collections.Generic.List[RockPkg]'
    foreach ($p in $script:AllPkgs) {
        if ($cat -and $cat -ne 'All categories' -and $p.Category -ne $cat) { continue }
        if ($q -and $p.Name -notlike "*$q*" -and $p.Id -notlike "*$q*") { continue }
        $view.Add($p)
    }
    $lvFind.ItemsSource = $view
    $txtFindInfo.Text = "$($view.Count) of $($script:AllPkgs.Count) apps. Select a row, then Copy ID / Use in Install tab / Install selected (double-click copies the ID)."
}
