$btnInstallCustom.Add_Click({ Start-Work {
    $id = $txtCustomId.Text.Trim()
    if (-not $id) { Log 'Type a winget ID first (example: Valve.Steam).' 'WARN' 'Winget'; return }
    Invoke-Winget 'install' @($id)
} })
