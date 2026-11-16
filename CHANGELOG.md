# Changelog

All notable changes to **Rock Toolbox** are listed here.
This file compares the old single-file PowerShell edition (**3.0.0**, `RockToolbox.ps1`) with the native C# edition (**4.1.0**).
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning: [SemVer](https://semver.org/).

## [4.1.0]

### Added
- **R-Downloader 1.2**: the download manager is now its own program (multi-connection downloads up to 32, resume, queues, scheduler, torrents and magnet links, Rlist playlists, Grabber, clipboard watching, "when all downloads finish" action, browser extension for Edge / Chrome / Brave / Opera / Firefox, its own sounds, 11-tab Options window, menu bar).
- Optional task **Install R-Downloader** in the Rock Toolbox setup (file-type association for `.Rlist`, desktop icon, start with Windows).
- On first start R-Downloader copies the look, language and download list from Rock Toolbox (nothing is deleted).

### Changed
- **Downloads** is no longer a page inside Rock Toolbox; it moved to R-Downloader.
- Rock Toolbox navigation is now grouped: **Apps** (Install, Updates, Uninstall Apps), **System** (Tweaks, Debloat, Config, Windows Update, Drivers, Startup), **Maintenance** (Cleanup, Repair, System Info), **More** (Wallpapers, God Mode).
- The installer builds both programs from one script (`installer/RockToolbox.iss`).

### Removed
- The built-in Downloads tab and its settings (`DlDir`, `DlMax`, `DlLimit`, `DlConn`).

## [4.0.0]

### Added
- **Native C# / WPF application.** The app itself no longer runs on PowerShell (the 3.0.0 script re-launched itself in Windows PowerShell 5.1 and compiled helper code on first start). Windows PowerShell is only called for a few helpers: SpotX, the Chocolatey bootstrap, one Cleanup step and the Debloat fallback.
- **Real installer** (Inno Setup, English and Arabic wizard, light/dark aware) instead of `Start-RockToolbox.bat` / `irm ... | iex`.
- **winget and Chocolatey are installed quietly** by the setup when they are missing (bundled in `redist/`), and on demand from the Install page.
- **Drivers: hardware scan + Microsoft Update Catalog.** Reads installed driver version and date from WMI, looks each hardware ID up in the Update Catalog and shows every update as **"from X to Y"** (Installed vs New version/date). Downloads the `.cab`, unpacks it with `expand.exe`, installs with `pnputil`, makes a restore point first. Results are cached for 6 hours (`driver-cache.json`). *(3.0.0 asked the Windows Update API for missing drivers.)*
- **Gaming section in Config**:
  - *Best Windows settings for games - Desktop PC* (Game Mode on, game recording off, GPU scheduling on, mouse acceleration off, CPU/GPU priority for games, High performance power plan).
  - *Best Windows settings for games - Laptop* (same, but only while charging; the battery profile is not touched).
  - *Game Loop / Tencent Gaming Buddy - Boost for PUBG Mobile* (high-performance GPU, High CPU priority, no fullscreen optimizations, recommended in-game engine settings for this PC).
  - *Restore previous settings*: every changed value is saved first (`gameboost.bak`).
- **Back / Forward navigation** with toolbar buttons, `Alt+Left` / `Alt+Right` and the mouse side buttons.
- **About window** (version, Windows version, developer, credits).
- **Welcome tour: "Pick your look"** step (Classic or Modern, Light or Dark).
- **Wallpapers: My pictures** and **Add pictures...** (browse local images, works without internet). Daily Bing wallpaper now runs through the tiny `RockToolboxDaily.exe` + scheduled task, no PowerShell.
- **Log viewer** with colours, *Errors only*, Copy, Clear, Refresh; click an error in the status bar to jump to it (`native.log`).
- **Runs in the system tray**: closing the window keeps Rock Toolbox running in the background (tray menu: Open, Run at startup, Exit) with a modern dark menu and fade-in.
- **Start with Windows** in the background through a scheduled task, plus a **Keep running in the tray** switch in Settings.
- **Single instance**: opening the app again brings the existing window to the front.
- Command-line switches: `--background`, `--quit`, `--enable-startup`, `--disable-startup`, `--pin-taskbar`, `--unpin-taskbar`.
- Windows 11 rounded window corners and dark title bar on every window.
- **Fix DNS** shortcut when GitHub or a download source cannot be reached (update window, wallpapers).
- Link texts show a small card with the real address on hover.

### Changed
- **Instant look changes**: language, light/dark, accent colour, background and Classic/Modern are applied by rebuilding the window, with **no restart**.
- **Self-update** now downloads the new **installer** (`.exe`) from the latest GitHub Release (four sources tried in turn, with *Fix DNS* on failure). 3.0.0 replaced its own `.ps1` file.
- **Modern Install page** is a new layout: search + WinGet/Chocolatey switch, one scrolling row of categories, a grid of cards, and a **"Your list"** panel with the main buttons and *More options*.
- **Cleanup** scans and deletes in parallel (`FastFs`) and gives a more detailed result: size before / after per drive, what was freed, what is in use and what was denied.
- App catalogue grew from 434 to 435 winget entries (added `Google.QuickShare`); nothing was removed.
- Language packs (Arabic, French, Spanish, German, Turkish, Russian, Chinese, Japanese, Portuguese, Italian, Hindi) are installed with the program (`lang\*.json`) instead of being downloaded on demand, so every language works offline.
- Settings are still stored in `%LOCALAPPDATA%\RockToolbox\settings.json` with the same keys, so 3.0.0 settings carry over.

### Removed
- The PowerShell console window, banner and the *relaunch as admin in PowerShell 5.1* step. The app now asks for administrator rights through its manifest.

### Known limitation
- Tweak **Memory Compression - Disable** is still listed but only shows a warning: Windows exposes it only through PowerShell cmdlets, which the native build no longer uses.

### Notes
- The old PowerShell version is kept in `reference-powershell/` for reference only.
- Log text stays in English; menus, tabs and sections are translated.
- Each language pack has 611 strings; English is built in.

## [3.0.0] - baseline (`RockToolbox.ps1`)
- Single-file PowerShell + WPF script, launched with `Start-RockToolbox.bat` or `irm ... | iex`.
- 16 tabs: Install, Tweaks, Debloat, Config, Updates, Windows Update, Uninstall Apps, Drivers, System Info, Cleanup, Startup, Repair, **Downloads**, Wallpapers, God Mode, Settings.
- 434 winget apps in 12 categories, Chocolatey switch, Package ID and Winget Upgrade views.
- Modern (gradient) and Classic looks, 12 interface languages (packs downloaded on demand).
