# R-Downloader

A fast, standalone download manager for Windows (native WPF / C#, no PowerShell, no .NET SDK needed to build).

* Multi-connection downloads (up to 32) with resume, queues, speed limit and automatic retries
* Torrents and magnet links (through aria2, downloaded on first use)
* IDM-style window: big toolbar, categories tree (Compressed, Documents, Music, Programs, Video, Unfinished, Finished, Queues), file-type icons
* Download File Info / progress windows with a live connections table
* **Rlist** files: a playlist of download links, editable in Notepad, opens with a double-click (no UAC prompt)
* Browser extension for Edge, Chrome, Brave, Opera and Firefox (folder `extension`)
* Menu bar (Tasks / File / Downloads / View / Help) with every action of the list, shortcuts, sorting, scheduler and "when all downloads finish" (shut down / sleep / hibernate / exit)
* Options window with 11 tabs: General, File Types, Save To, Downloads, Connection, Proxy, Site Logins, Sounds, Speed Limiter, Appearance, Torrents
* Add-a-torrent window, magnet / .torrent association, seeding time, share ratio and upload limit
* Its own sounds (complete, failed, queue start, queue finished) - you can pick your own .wav files
* Browser-cards extension window (Edge, Chrome, Firefox, Opera, Brave) with link test and repair
* Dark / Light mode, Classic / Modern look, English / Arabic and more languages
* Runs in the tray, optional start with Windows

## Build

Open `installer\RDownloader.iss` in Inno Setup 6.6+ and press **Compile (F9)**. The script compiles the program with the C# compiler that is built into Windows (csc.exe) and writes `RDownloader-Setup-1.2.0.exe` to `out\`. If the compile fails, read `build.log` next to the project.

## Data

Settings, the download list and logs live in `%LOCALAPPDATA%\RDownloader`. On the first start the look, language, download settings and the download list are copied over from Rock Toolbox if it was installed (nothing is deleted there).

## Browser extension

R-Downloader listens on `127.0.0.1:53741` (up to 53750). Load the folder `extension\chromium` (or `extension\firefox`) in the browser; see `extension\README.txt`.
