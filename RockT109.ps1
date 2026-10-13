foreach ($c in @($Apps | ForEach-Object { $_.Cat } | Select-Object -Unique)) { [void]$cmbFindSource.Items.Add($c) }
