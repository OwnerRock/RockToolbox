$btnWpMore.Add_Click({ if ($script:wpCards.Count -gt 0) { Start-WpSearch $script:wpMode $script:wpQuery $script:wpTitle $true } })
