$btnDrvOff.Add_Click({ Start-Work { Set-ToggleState $DriverToggle $false; Log 'Drivers via Windows Update: blocked.' 'INFO' 'Update' } })
