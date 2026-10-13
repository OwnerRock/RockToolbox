$DriverToggle = @($Toggles | Where-Object { $_.Name -like 'Automatic Driver*' })[0]
