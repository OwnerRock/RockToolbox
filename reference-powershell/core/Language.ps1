# ==============================================================================
# MODULE: src\core\Language.ps1
# ==============================================================================
# Module: core/Language - English/Arabic localization (gear menu choice)
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Language: settings['Language'] = 'English' | 'Arabic'
# The XAML is translated at load time (Convert-XamlLang), code-built
# labels use L, and the whole UI mirrors to RightToLeft in Arabic.
# ------------------------------------------------------------------

$script:Loc = [ordered]@{}   # filled from the language file of the chosen language (English needs none)

function Test-Arabic { return ([string]$script:Settings['Language'] -eq 'Arabic') }
function Test-Translated { return ([string]$script:Settings['Language'] -ne 'English') }
function Test-Rtl { return (@('Arabic', 'Persian', 'Hebrew') -contains [string]$script:Settings['Language']) }

# every language the gear menu offers. English + Arabic are built in; the others are language packs
# (a small .json file: English text -> translated text) downloaded once from the official Rock Toolbox repo.
$script:LangDir = Join-Path $AppDir 'lang'
$script:LangPackBase = 'https://raw.githubusercontent.com/OwnerRock/RockToolbox/main/lang/'
$script:LangList = [ordered]@{
    'English'    = @{ Code = 'en'; Native = 'English' }
    'Arabic'     = @{ Code = 'ar'; Native = 'العربية' }
    'French'     = @{ Code = 'fr'; Native = 'Français' }
    'Spanish'    = @{ Code = 'es'; Native = 'Español' }
    'German'     = @{ Code = 'de'; Native = 'Deutsch' }
    'Turkish'    = @{ Code = 'tr'; Native = 'Türkçe' }
    'Russian'    = @{ Code = 'ru'; Native = 'Русский' }
    'Chinese'    = @{ Code = 'zh'; Native = '中文' }
    'Japanese'   = @{ Code = 'ja'; Native = '日本語' }
    'Portuguese' = @{ Code = 'pt'; Native = 'Português' }
    'Italian'    = @{ Code = 'it'; Native = 'Italiano' }
    'Hindi'      = @{ Code = 'hi'; Native = 'हिन्दी' }
}
function Get-LangPackPath([string]$Name) { return (Join-Path $script:LangDir ([string]$script:LangList[$Name].Code + '.json')) }
function Test-LangPack([string]$Name) {
    if (-not $script:LangList.Contains($Name)) { return $false }
    return (Test-Path -LiteralPath (Get-LangPackPath $Name))
}
function Read-LangJson([string]$Path) {
    Add-Type -AssemblyName System.Web.Extensions -ErrorAction SilentlyContinue
    $ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $ser.MaxJsonLength = [int]::MaxValue
    return $ser.DeserializeObject([System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8))
}
function Import-LangPack([string]$Name) {
    try {
        if (-not (Test-LangPack $Name)) { return $false }
        $j = Read-LangJson (Get-LangPackPath $Name)
        $script:Loc.Clear()
        $n = 0
        foreach ($k in @($j.Keys)) { if ($j[$k]) { $script:Loc[[string]$k] = [string]$j[$k]; $n++ } }
        return ($n -gt 0)
    } catch { return $false }
}
$script:LastLangError = ''
function Get-LangPack([string]$Name) {
    $script:LastLangError = ''
    try {
        if (-not $script:LangList.Contains($Name)) { return $false }
        if (-not (Test-Path -LiteralPath $script:LangDir)) { New-Item -ItemType Directory -Force -Path $script:LangDir | Out-Null }
        try { [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072 } catch { }
        $code = [string]$script:LangList[$Name].Code
        $tmp = Join-Path $env:TEMP ('rock_lang_' + $code + '.json')
        $urls = @(($script:LangPackBase + $code + '.json'), ('https://github.com/OwnerRock/RockToolbox/raw/main/lang/' + $code + '.json'))
        Log ("Downloading the $Name language pack...") 'INFO' 'Lang'
        $got = $false
        foreach ($u in $urls) {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
            try { Get-RemoteFile $u $tmp; if ((Test-Path -LiteralPath $tmp) -and ((Get-Item -LiteralPath $tmp).Length -gt 10)) { $got = $true; break } }
            catch { $script:LastLangError = $_.Exception.Message; Log ("  $u -> " + $_.Exception.Message) 'WARN' 'Lang' }
            if (-not $got) {
                try {
                    $ProgressPreference = 'SilentlyContinue'
                    Invoke-WebRequest -UseBasicParsing -Uri $u -OutFile $tmp -TimeoutSec 25 -Headers @{ 'User-Agent' = 'Mozilla/5.0 RockToolbox' }
                    if ((Test-Path -LiteralPath $tmp) -and ((Get-Item -LiteralPath $tmp).Length -gt 10)) { $got = $true; break }
                } catch { $script:LastLangError = $_.Exception.Message; Log ("  $u (2nd try) -> " + $_.Exception.Message) 'WARN' 'Lang' }
            }
        }
        if (-not $got) { throw $(if ($script:LastLangError) { $script:LastLangError } else { 'No data received.' }) }
        $j = Read-LangJson $tmp
        if (@($j.Keys).Count -lt 1) { throw 'The language file is empty.' }
        Copy-Item -LiteralPath $tmp -Destination (Get-LangPackPath $Name) -Force
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        return $true
    } catch { $script:LastLangError = $_.Exception.Message; Log ("Could not download the $Name language pack: " + $_.Exception.Message) 'WARN' 'Lang'; return $false }
}
# a saved language that is not built in must have its pack on disk, otherwise fall back to English
$script:SavedLang = [string]$script:Settings['Language']
if ($script:SavedLang -ne 'English') {
    if (-not ($script:LangList.Contains($script:SavedLang) -and (Import-LangPack $script:SavedLang))) { $script:Settings['Language'] = 'English' }
}

# Returns the Arabic text when the interface language is Arabic, otherwise the original.
function L {
    param([string]$Text)
    if (Test-Translated) {
        $t = $script:Loc[$Text]
        if ($t) { return $t }
    }
    return $Text
}

# Translates every Text / Content / ToolTip attribute of a XAML string through $script:Loc.
function Convert-XamlLang {
    param([string]$Text)
    $eval = [System.Text.RegularExpressions.MatchEvaluator]{
        param($m)
        $k = $m.Groups[2].Value
        if ($script:Loc.Contains($k)) { $script:Loc[$k] } else { $m.Value }
    }
    return [regex]::Replace($Text, '(?<=\b(Text|Content|ToolTip)=")([^"]*)', $eval)
}

# Starts a fresh copy of the app (used after the language changes and at the end of the welcome tour).
function Restart-Rock {
    try {
        if ($env:ROCK_LAUNCHER -and (Test-Path -LiteralPath $env:ROCK_LAUNCHER)) {
            Start-Process -FilePath $env:ROCK_LAUNCHER -WorkingDirectory (Split-Path -Parent $env:ROCK_LAUNCHER)
        } else {
            $relaunch = "-NoProfile -ExecutionPolicy Bypass -STA -File `"$script:RockMain`""
            if ($NoConsole) { Start-Process powershell.exe -ArgumentList ($relaunch + ' -NoConsole -WindowStyle Hidden') -WindowStyle Hidden }
            else            { Start-Process powershell.exe -ArgumentList $relaunch }
        }
    } catch { }
}

# Switches the language, saves it, then restarts the app so every screen is rebuilt in the new language.
# A language that is not built in needs its pack first: it is downloaded once from the official repo.
function Set-RockLanguage {
    param([string]$Value)
    if (([string]$script:Settings['Language']) -eq $Value) { Hide-GearMenu; return }
    if ($Value -ne 'English' -and -not (Test-LangPack $Value)) {
        $script:LangDlOk = $false
        Start-Work { $script:LangDlOk = [bool](Get-LangPack $Value) } ('Downloading the ' + $Value + ' language pack...')
        if (-not $script:LangDlOk) {
            [void](Show-RockMessage -Text ("Could not download the $Value language pack.`nCheck your internet connection and try again.") -Caption 'Language' -Buttons 'OK' -Icon 'Warning')
            return
        }
    }
    $script:Settings['Language'] = $Value
    Save-Settings
    Restart-Rock
    $window.Close()
}

