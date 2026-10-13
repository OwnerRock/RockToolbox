function Get-WUAutoState {
    try { $v = (Get-ItemProperty -LiteralPath $script:AUKey -Name NoAutoUpdate -ErrorAction Stop).NoAutoUpdate; return ([int]$v -ne 1) }
    catch { return $true }
}
