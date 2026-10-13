$btnDrvOn.Add_Click({  Start-Work { Set-ToggleState $DriverToggle $true; Log 'Drivers via Windows Update: allowed.' 'INFO' 'Update' } })
