redist\  -  files that Rock Toolbox's setup installs quietly together with the program
==================================================================================

Put these 3 files in THIS folder before you compile installer\RockToolbox.iss:

1) App Installer (winget)
   File name:  Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle
   Download:   https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle

2) winget libraries (VCLibs + UI.Xaml). Keep it as a .zip - the setup compiler unpacks it by itself.
   File name:  DesktopAppInstaller_Dependencies.zip
   Download:   https://github.com/microsoft/winget-cli/releases/latest/download/DesktopAppInstaller_Dependencies.zip

3) Chocolatey. It downloads as chocolatey.<version>.nupkg - keep that name, do not unpack it.
   Download:   https://community.chocolatey.org/api/v2/package/chocolatey

What the setup does with them (nothing is shown to the user):
 - winget:      installed for this PC if "winget" is not there yet.
 - Chocolatey:  copied to C:\ProgramData\chocolatey (+ PATH and ChocolateyInstall) if it is not there yet.
They stay installed when Rock Toolbox is removed (they are system tools other programs use too).

If a file is missing the setup is still built, only without that part (a note appears in the compiler output).
