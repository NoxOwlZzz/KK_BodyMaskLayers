@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "MSBUILD_EXE=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

if not exist "%MSBUILD_EXE%" (
  for /f "usebackq delims=" %%I in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD_EXE=%%I"
)

if not exist "%MSBUILD_EXE%" (
  echo ERROR: MSBuild was not found.
  exit /b 2
)

"%MSBUILD_EXE%" "%PROJECT_DIR%tests\KK_BodyMaskLayers.Tests.csproj" /t:Rebuild /p:Configuration=Release /m /nologo /v:minimal
if errorlevel 1 exit /b %errorlevel%

"%PROJECT_DIR%tests\bin\Release\KK_BodyMaskLayers.Tests.exe"
exit /b %errorlevel%
