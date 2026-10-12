@echo off
title Rock Toolbox launcher
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%~dp0RockToolbox.ps1'"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RockToolbox.ps1"
if errorlevel 1 pause
