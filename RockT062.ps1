function Set-UpdateTasks {
    param([bool]$Enable)
    foreach ($tp in '\Microsoft\Windows\UpdateOrchestrator\', '\Microsoft\Windows\WindowsUpdate\') {
        foreach ($task in @(Get-ScheduledTask -TaskPath $tp -ErrorAction SilentlyContinue)) {
            try {
                if ($Enable) { Enable-ScheduledTask -InputObject $task -ErrorAction Stop | Out-Null }
                else         { Disable-ScheduledTask -InputObject $task -ErrorAction Stop | Out-Null }
            } catch { }
        }
    }
}
