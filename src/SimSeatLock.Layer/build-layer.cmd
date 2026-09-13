@echo off
setlocal
cd /d "%~dp0"
if not defined VSINSTALLDIR (
  if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" (
    call "%ProgramFiles%\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=amd64
  ) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\Common7\Tools\VsDevCmd.bat" (
    call "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\Common7\Tools\VsDevCmd.bat" -arch=amd64
  ) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\VsDevCmd.bat" (
    call "%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64
  )
)
msbuild SimSeatLock.Layer.vcxproj /p:Configuration=Release /p:Platform=x64 /m
if errorlevel 1 exit /b 1
echo Built publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.dll
exit /b 0
