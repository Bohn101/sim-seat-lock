@echo off
setlocal EnableExtensions
REM Disable the implicit layer in both hives without deleting the files.
REM Use this for LMU online (EAC) so the game can start in VR.
REM Re-enable with install-layer.cmd. Does not touch ACE binaries.

set "JSON=C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json"
if exist "%~dp0..\..\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json" (
  pushd "%~dp0..\..\publish\layer"
  set "JSON=%CD%\XR_APILAYER_NOVENDOR_sim_seat_lock.json"
  popd
)

reg add "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%JSON%" /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%JSON%" /t REG_DWORD /d 1 /f >nul 2>&1
if errorlevel 1 (
  echo HKLM disable needs elevation. Prompting UAC...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'reg.exe' -Verb RunAs -Wait -ArgumentList @('add','HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit','/v','%JSON%','/t','REG_DWORD','/d','1','/f')"
)

echo Disabled (DWORD 1) : %JSON%
echo HKCU:
reg query "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo HKLM:
reg query "HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo Re-enable: cmd.exe //c src/SimSeatLock.Layer/install-layer.cmd
exit /b 0
