@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "GAME_ROOT=%~1"
if "%GAME_ROOT%"=="" set "GAME_ROOT=%KOIKATSU_DIR%"
set "MANAGED=%GAME_ROOT%\Koikatu_Data\Managed"
set "BEPINEX=%GAME_ROOT%\BepInEx"
set "LIB=%PROJECT_DIR%lib"

if "%GAME_ROOT%"=="" (
  echo ERROR: Pass the Koikatsu game directory or set KOIKATSU_DIR.
  echo Usage: copy-references.bat "C:\path\to\Koikatsu"
  exit /b 2
)

if not exist "%GAME_ROOT%\Koikatu.exe" (
  echo ERROR: Koikatsu was not found at "%GAME_ROOT%".
  echo Usage: copy-references.bat "C:\path\to\Koikatsu"
  exit /b 2
)

if not exist "%LIB%" mkdir "%LIB%"

for %%F in (mscorlib.dll System.dll System.Core.dll System.Xml.dll Assembly-CSharp.dll Assembly-CSharp-firstpass.dll UnityEngine.dll UnityEngine.UI.dll TextMeshPro-1.0.55.56.0b12.dll) do (
  if not exist "%MANAGED%\%%F" (
    echo ERROR: Missing managed reference "%MANAGED%\%%F".
    exit /b 3
  )
  copy /Y "%MANAGED%\%%F" "%LIB%\%%F" >nul || exit /b 4
)

copy /Y "%BEPINEX%\core\BepInEx.dll" "%LIB%\BepInEx.dll" >nul || exit /b 4
copy /Y "%BEPINEX%\core\0Harmony.dll" "%LIB%\0Harmony.dll" >nul || exit /b 4
copy /Y "%BEPINEX%\plugins\KKAPI.dll" "%LIB%\KKAPI.dll" >nul || exit /b 4
copy /Y "%BEPINEX%\plugins\KK_BepisPlugins\ExtensibleSaveFormat.dll" "%LIB%\ExtensibleSaveFormat.dll" >nul || exit /b 4

echo Local compile references refreshed in "%LIB%". These files are not copied to plugin output.
exit /b 0
