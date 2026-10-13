$btnWpDailyOff.Add_Click({ Start-Work {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Log 'Daily Bing wallpaper disabled.'
} })

# ------------------------------------------------------------------
# Winget ID list (ready list of every app in Rock Toolbox) + Winget Upgrade page
