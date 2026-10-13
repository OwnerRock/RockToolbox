if ([bool]$script:Settings['StartMaximized']) { $window.WindowState = 'Maximized' }
[void]$window.ShowDialog()

