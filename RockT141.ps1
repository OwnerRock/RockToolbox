function Update-WUAutoText {
    $on = Get-WUAutoState
    if ($on) { $txtWUAuto.Text = 'Automatic updates: ON'; Set-Res $txtWUAuto 'Foreground' '#3DDC84' }
    else     { $txtWUAuto.Text = 'Automatic updates: OFF'; Set-Res $txtWUAuto 'Foreground' '#FF6B6B' }
    $btnWUAutoOff.IsEnabled = $on
    $btnWUAutoOn.IsEnabled = (-not $on)
}
