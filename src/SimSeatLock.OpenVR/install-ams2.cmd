@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "PROXY=%~dp0..\..\publish\openvr\openvr_api.dll"
if not exist "%PROXY%" (
  echo Missing %PROXY%
  echo Build first: cmd.exe //c src\SimSeatLock.OpenVR\build-openvr.cmd
  exit /b 1
)

set "TARGET=%~1"
if "%TARGET%"=="" (
  for %%D in (
    "%ProgramFiles(x86)%\Steam\steamapps\common\Automobilista 2\x64"
    "%ProgramFiles%\Steam\steamapps\common\Automobilista 2\x64"
    "D:\SteamLibrary\steamapps\common\Automobilista 2\x64"
    "D:\Games\Automobilista 2\x64"
    "E:\SteamLibrary\steamapps\common\Automobilista 2\x64"
    "C:\SteamLibrary\steamapps\common\Automobilista 2\x64"
  ) do if exist "%%~D\openvr_api.dll" set "TARGET=%%~D"
)
if "%TARGET%"=="" (
  echo Could not find Automobilista 2\x64\openvr_api.dll
  echo Pass the x64 folder: install-ams2.cmd "D:\Games\Automobilista 2\x64"
  exit /b 1
)

set "GAME=%TARGET%\openvr_api.dll"
set "STOCK=%TARGET%\openvr_api.stock.dll"
set "ORIG=%TARGET%\openvr_api_orig.dll"
echo Target %GAME%

for %%F in ("%GAME%") do echo Current dll size=%%~zF date=%%~tF

if exist "%STOCK%" (
  echo Stock already saved: %STOCK%
) else (
  echo Saving stock to openvr_api.stock.dll
  copy /Y "%GAME%" "%STOCK%" >nul
  if errorlevel 1 (
    echo Copy stock failed. Close AMS2 and retry.
    exit /b 1
  )
)
copy /Y "%STOCK%" "%ORIG%" >nul
if errorlevel 1 (
  echo Copy openvr_api_orig.dll failed. Close AMS2 and retry.
  exit /b 1
)

copy /Y "%PROXY%" "%GAME%" >nul
if errorlevel 1 (
  echo Install proxy failed. Close AMS2 and retry.
  exit /b 1
)
copy /Y "%~dp0..\..\config\geometry.json" "%TARGET%\geometry.json" >nul 2>nul
echo Installed SimSeatLock OpenVR forwarder as openvr_api.dll
echo Orig for PE forwards: %ORIG%
echo Restore: cmd.exe //c src\SimSeatLock.OpenVR\restore-ams2.cmd
echo Proof after sitting in the car: %TARGET%\simseatlock-openvr.log
exit /b 0
