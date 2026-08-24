@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "PROJECT_DIR=%~dp0"
set "GAME_ROOT=%~1"
if "%GAME_ROOT%"=="" set "GAME_ROOT=%KOIKATSU_DIR%"
set "SOURCE=%PROJECT_DIR%bin\Release\KK_BodyMaskLayers.dll"
set "PLUGIN_DIR=%GAME_ROOT%\BepInEx\plugins\KK_BodyMaskLayers"
set "TARGET=%PLUGIN_DIR%\KK_BodyMaskLayers.dll"
set "BACKUP_DIR=%PLUGIN_DIR%\backup"

if "%GAME_ROOT%"=="" (
  echo ERROR: Pass the Koikatsu game directory or set KOIKATSU_DIR.
  echo Usage: deploy-release.bat "KOIKATSU_GAME_DIRECTORY"
  exit /b 2
)

if not exist "%GAME_ROOT%\Koikatu.exe" (
  echo ERROR: Koikatsu was not found at "%GAME_ROOT%".
  echo Usage: deploy-release.bat "KOIKATSU_GAME_DIRECTORY"
  exit /b 2
)

call "%PROJECT_DIR%build-release.bat"
if errorlevel 1 exit /b %errorlevel%

if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"
if errorlevel 1 (
  echo ERROR: Could not create "%PLUGIN_DIR%".
  exit /b 3
)

if exist "%TARGET%" (
  for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "STAMP=%%I"
  if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
  rem Use .bak so BepInEx does not scan the backup as a plugin.
  copy /Y "%TARGET%" "%BACKUP_DIR%\KK_BodyMaskLayers_!STAMP!.dll.bak" >nul
  if errorlevel 1 (
    echo ERROR: Could not back up the existing DLL. Deployment stopped.
    exit /b 4
  )
)

copy /Y "%SOURCE%" "%TARGET%" >nul
if errorlevel 1 (
  echo ERROR: Could not copy "%SOURCE%" to "%TARGET%".
  exit /b 5
)

echo Deployed only: "%TARGET%"
echo Restart Koikatsu or CharaStudio before testing. Do not hot-reload this plugin.
exit /b 0
