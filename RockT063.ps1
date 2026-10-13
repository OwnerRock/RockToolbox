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
