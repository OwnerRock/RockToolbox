# Module: pages/Updates - Updates page (profiles + pause)
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Windows Update profiles
# ------------------------------------------------------------------
$WU = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
function Reset-UpdatePolicy {
    Remove-Item -LiteralPath $WU -Recurse -Force -ErrorAction SilentlyContinue
    Set-Service wuauserv -StartupType Manual -ErrorAction SilentlyContinue
    Set-Service UsoSvc -StartupType Automatic -ErrorAction SilentlyContinue
}
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
$btnUpdRec.Add_Click({ Start-Work {
    Reset-UpdatePolicy
    Set-Reg $WU 'DeferFeatureUpdates' 1
    Set-Reg $WU 'DeferFeatureUpdatesPeriodInDays' 365
    Set-Reg $WU 'DeferQualityUpdates' 1
    Set-Reg $WU 'DeferQualityUpdatesPeriodInDays' 4
    Set-Reg $WU 'ExcludeWUDriversInQualityUpdate' 1
    Set-Reg "$WU\AU" 'NoAutoUpdate' 0
    Set-Reg "$WU\AU" 'AUOptions' 3
    Set-Reg "$WU\AU" 'NoAutoRebootWithLoggedOnUsers' 1
    Set-UpdateTasks $true
    Start-Service wuauserv -ErrorAction SilentlyContinue
    Log 'Recommended update profile applied. Restart Windows.' 'INFO' 'Update'
} })
$btnUpdDefault.Add_Click({ Start-Work {
    Reset-UpdatePolicy
    Set-UpdateTasks $true
    Start-Service wuauserv -ErrorAction SilentlyContinue
    Start-Service UsoSvc -ErrorAction SilentlyContinue
    Log 'Windows Update restored to default. Restart Windows.' 'INFO' 'Update'
} })
$btnUpdDisable.Add_Click({ Start-Work {
    if (-not (New-SafetyRestorePoint 'disabling Windows Update')) { throw 'Cancelled by user' }
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
$DriverToggle = @($Toggles | Where-Object { $_.Name -like 'Automatic Driver*' })[0]
$btnDrvOff.Add_Click({ Start-Work { Set-ToggleState $DriverToggle $false; Log 'Drivers via Windows Update: blocked.' 'INFO' 'Update' } })
$btnDrvOn.Add_Click({  Start-Work { Set-ToggleState $DriverToggle $true; Log 'Drivers via Windows Update: allowed.' 'INFO' 'Update' } })
$btnUpdOpen.Add_Click({ Start-Process 'ms-settings:windowsupdate' })

