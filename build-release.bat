@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"

call "%PROJECT_DIR%build.bat"
if errorlevel 1 exit /b %errorlevel%

if not exist "%PROJECT_DIR%bin\Release\KK_BodyMaskLayers.dll" (
  echo ERROR: Release DLL was not produced.
  exit /b 3
)

if exist "%PROJECT_DIR%bin\Release\KK_BodyMaskLayers.pdb" (
  echo ERROR: Release unexpectedly produced a PDB.
  exit /b 4
)

echo Release ready: "%PROJECT_DIR%bin\Release\KK_BodyMaskLayers.dll"
exit /b 0
