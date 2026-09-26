@echo off
setlocal EnableExtensions
set "TARGET=%~1"
if "%TARGET%"=="" (
  for %%D in (
    "%ProgramFiles(x86)%\Steam\steamapps\common\Automobilista 2\x64"
    "%ProgramFiles%\Steam\steamapps\common\Automobilista 2\x64"
    "D:\SteamLibrary\steamapps\common\Automobilista 2\x64"
    "E:\SteamLibrary\steamapps\common\Automobilista 2\x64"
    "C:\SteamLibrary\steamapps\common\Automobilista 2\x64"
  ) do if exist "%%~D\openvr_api.stock.dll" set "TARGET=%%~D"
)
if "%TARGET%"=="" (
  echo No openvr_api.stock.dll found. Pass the AMS2 x64 folder.
  exit /b 1
)
if not exist "%TARGET%\openvr_api.stock.dll" (
  echo Missing %TARGET%\openvr_api.stock.dll
  exit /b 1
)
copy /Y "%TARGET%\openvr_api.stock.dll" "%TARGET%\openvr_api.dll" >nul
if errorlevel 1 (
  echo Restore failed. Close AMS2 and retry.
  exit /b 1
)
echo Restored stock openvr_api.dll in %TARGET%
exit /b 0
