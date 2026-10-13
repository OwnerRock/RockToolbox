$btnUpdDefault.Add_Click({ Start-Work {
    Reset-UpdatePolicy
    Set-UpdateTasks $true
    Start-Service wuauserv -ErrorAction SilentlyContinue
    Start-Service UsoSvc -ErrorAction SilentlyContinue
    Log 'Windows Update restored to default. Restart Windows.' 'INFO' 'Update'
} })
