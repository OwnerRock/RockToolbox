@echo off
title Rock Toolbox launcher
cd /d "%~dp0"
if exist "RockT250.ps1" goto run
echo Error: the 250 RockT001-RockT250 .ps1 files are missing next to this .bat
pause
exit /b 1
:run
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%~dp0RockToolbox.ps1'; Get-ChildItem -LiteralPath '%~dp0' -File | Where-Object { $_.Name -match '^RockT[0-9]{3}\.ps1$' } | ForEach-Object { Unblock-File -LiteralPath $_.FullName }"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RockToolbox.ps1"
if errorlevel 1 pause
