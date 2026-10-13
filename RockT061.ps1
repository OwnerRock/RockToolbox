function Reset-UpdatePolicy {
    Remove-Item -LiteralPath $WU -Recurse -Force -ErrorAction SilentlyContinue
    Set-Service wuauserv -StartupType Manual -ErrorAction SilentlyContinue
    Set-Service UsoSvc -StartupType Automatic -ErrorAction SilentlyContinue
}
