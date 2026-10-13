
# ------------------------------------------------------------------
# Theme engine + settings (settings.json in %LOCALAPPDATA%\RockToolbox)
# ------------------------------------------------------------------
$script:SettingsFile = Join-Path $AppDir 'settings.json'
$script:SettingsDefault = @{
    Theme = 'Dark'; Accent = 'Lime'; AccentHex = '#A3E635'; Background = 'Default'
    FontSize = 13; Scale = 100; Topmost = $false; StartMaximized = $false; StatusBar = $true
    HideConsole = $true; RememberTab = $false; StartTab = 'Install'; LastTab = 'Install'
    BgColor = ''; BgImage = ''; BgOpacity = 40
    ScrollAnim = 'Smooth'; ScrollSpeed = 'Normal'; PageAnim = 'Slide up'; CardAnim = $true; BgBlur = 0
}
$script:Settings = @{}
foreach ($k in $script:SettingsDefault.Keys) { $script:Settings[$k] = $script:SettingsDefault[$k] }

function Read-Settings {
    if (-not (Test-Path -LiteralPath $script:SettingsFile)) { return }
    try {
        $j = Get-Content -LiteralPath $script:SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($k in @($script:SettingsDefault.Keys)) {
            $p = $j.PSObject.Properties[$k]
            if ($p -and $null -ne $p.Value) { $script:Settings[$k] = $p.Value }
        }
    } catch { }
}
function Save-Settings {
    try { $script:Settings | ConvertTo-Json | Set-Content -LiteralPath $script:SettingsFile -Encoding UTF8 } catch { }
}

$script:AccentPresets = [ordered]@{
    'Ocean'='#4FD1FF'; 'Blue'='#4F8CFF'; 'Violet'='#8B7BFF'; 'Purple'='#B36BFF'; 'Pink'='#FF6BC1'; 'Rose'='#FF5C7A'
    'Red'='#FF5555'; 'Orange'='#FF9F43'; 'Amber'='#FFC84A'; 'Lime'='#A3E635'; 'Green'='#3DDC84'; 'Teal'='#2DD4BF'
}
