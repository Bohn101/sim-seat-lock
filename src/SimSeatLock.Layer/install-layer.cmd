@echo off
setlocal EnableExtensions
cd /d "%~dp0..\..\publish\layer"
if not exist "%CD%\XR_APILAYER_NOVENDOR_sim_seat_lock.dll" (
  echo Build the layer first: src\SimSeatLock.Layer\build-layer.cmd
  exit /b 1
)

set "LAYERDIR=%CD%"
set "JSON=%LAYERDIR%\XR_APILAYER_NOVENDOR_sim_seat_lock.json"
set "KEY_CU=HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit"
set "KEY_LM=HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0write-layer-json.ps1" -LayerDir "%LAYERDIR%"
if errorlevel 1 exit /b 1

REM Canonical install is HKLM only. Writing the same JSON to HKCU + HKLM
REM makes SteamVR list SimSeatLock twice and the loader can negotiate twice.
REM ACE reads HKLM. SteamVR Off on an HKCU row does not disable HKLM.
reg add "%KEY_LM%" /v "%JSON%" /t REG_DWORD /d 0 /f >nul 2>&1
if errorlevel 1 (
  echo HKLM Implicit needs elevation. Prompting UAC...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'reg.exe' -Verb RunAs -Wait -ArgumentList @('add','HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit','/v','%JSON%','/t','REG_DWORD','/d','0','/f')"
)

REM Drop the HKCU duplicate even if it was enabled or disabled.
reg delete "%KEY_CU%" /v "%JSON%" /f >nul 2>&1

echo.
echo Registered HKLM (enabled=0):
echo   %JSON%
echo HKCU duplicate removed if it existed.
echo.
echo Manifest:
type "%JSON%"
echo.
echo HKCU Implicit:
reg query "%KEY_CU%" 2>nul
echo HKLM Implicit:
reg query "%KEY_LM%" 2>nul
echo.
echo Do not set XR_API_LAYER_PATH or XR_ENABLE_API_LAYERS.
echo Disable: src\SimSeatLock.Layer\disable-layer.cmd
echo Or env DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock=1
echo Do not load BuzzteeBear OXRMC. SteamVR Motion Smoothing OFF.
exit /b 0
