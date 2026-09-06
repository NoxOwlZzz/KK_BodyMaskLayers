@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "PROJECT=%PROJECT_DIR%KK_BodyMaskLayers.csproj"
set "MSBUILD_EXE=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

if not exist "%MSBUILD_EXE%" (
  for /f "usebackq delims=" %%I in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD_EXE=%%I"
)

if not exist "%MSBUILD_EXE%" (
  echo ERROR: MSBuild was not found. Install Visual Studio Build Tools or edit MSBUILD_EXE.
  exit /b 2
)

"%MSBUILD_EXE%" "%PROJECT%" /t:Rebuild /p:Configuration=Release /m /nologo /v:minimal
if errorlevel 1 exit /b %errorlevel%

if not exist "%PROJECT_DIR%bin\Release\KK_BodyMaskLayers.dll" (
  echo ERROR: Release DLL was not produced.
  exit /b 3
)

if exist "%PROJECT_DIR%bin\Release\KK_BodyMaskLayers.pdb" (
  echo ERROR: Release unexpectedly produced a PDB.
  exit /b 4
)

echo Built: %PROJECT_DIR%bin\Release\KK_BodyMaskLayers.dll
exit /b 0
