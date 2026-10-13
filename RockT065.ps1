$btnUpdDisable.Add_Click({ Start-Work {
    Reset-UpdatePolicy
    Set-Reg "$WU\AU" 'NoAutoUpdate' 1
    Set-Reg "$WU\AU" 'AUOptions' 1
    foreach ($s in 'wuauserv', 'UsoSvc') {
        Stop-Service $s -Force -ErrorAction SilentlyContinue
        Set-Service $s -StartupType Disabled -ErrorAction SilentlyContinue
    }
    Set-UpdateTasks $false
    Remove-Item "$env:windir\SoftwareDistribution\Download\*" -Recurse -Force -ErrorAction SilentlyContinue
    Log 'Windows Update disabled. Use Restore Defaults to turn it back on.' 'WARN' 'Update'
} })
