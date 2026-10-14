@echo off
title Rock Toolbox
setlocal

rem Starts Rock Toolbox. The script asks for Administrator by itself (UAC prompt).
rem Keep this file next to RockToolbox.ps1 and the src / ui folders.

set "ROCK=%~dp0RockToolbox.ps1"

if not exist "%ROCK%" (
    echo RockToolbox.ps1 was not found next to this file:
    echo   %~dp0
    echo.
    pause
    exit /b 1
)

if not exist "%~dp0src\core\Window.ps1" (
    echo The "src" folder is missing. Extract the whole zip, do not copy the .ps1 alone.
    echo.
    pause
    exit /b 1
)

rem Windows PowerShell 5.1 (powershell.exe), STA is required for the WPF window.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%ROCK%"

endlocal
