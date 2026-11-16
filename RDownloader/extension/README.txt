R-Downloader - browser extension
===========================================

The extension sends browser downloads to the R-Downloader desktop app.
R-Downloader must be running. If it is not, the browser downloads normally.

Chrome, Edge, Brave, Opera (Chromium)
-------------------------------------
1. Open the extensions page:
   chrome://extensions   (Chrome)
   edge://extensions     (Edge)
   brave://extensions    (Brave)
   opera://extensions    (Opera)
2. Turn on "Developer mode".
3. Click "Load unpacked" and pick the folder: extension\chromium
   (or unzip RDownloader-Chromium.zip and pick the unzipped folder).
4. Pin the extension icon. Click it to turn capture on/off and set filters.

Firefox
-------
1. Open about:debugging#/runtime/this-firefox
2. Click "Load Temporary Add-on..." and pick extension\firefox\manifest.json
   The add-on is removed when Firefox closes; load it again next time.
3. For a permanent install you need a signed build. Release Firefox only
   accepts signed add-ons. Firefox Developer Edition, Nightly and ESR can
   install an unsigned add-on after you set xpinstall.signatures.required
   to false in about:config.
4. If downloads are not captured, open about:addons -> R-Downloader
   Downloader -> Permissions and allow access to 127.0.0.1.

Settings (toolbar popup)
------------------------
- On/off toggle (badge shows ON / OFF).
- Connection status: green "Connected" or red "R-Downloader is not running".
- Minimum size in MB (0 = all sizes).
- File types to catch, comma separated, e.g. zip, exe, mp4 (empty = all).
- Right-click a link -> "Download with R-Downloader".
