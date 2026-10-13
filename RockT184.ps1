$script:TabNames     = @('Install', 'Tweaks', 'Debloat', 'Config', 'Updates', 'Windows Update', 'Uninstall Apps', 'Drivers', 'Wallpapers', 'Winget ID', 'Winget Upgrade', 'God Mode', 'Settings')

foreach ($n in $script:BgVariants.Keys) { [void]$cmbBgStyle.Items.Add($n) }
