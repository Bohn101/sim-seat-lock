@echo off
setlocal EnableExtensions
cd /d "%~dp0..\..\publish\layer"
if not exist "%CD%\XR_APILAYER_NOVENDOR_sim_seat_lock.dll" (
  echo Build the layer first: src\SimSeatLock.Layer\build-layer.cmd
  exit /b 1
)

set "LAYERDIR=%CD%"
set "JSON=%LAYERDIR%\XR_APILAYER_NOVENDOR_sim_seat_lock.json"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0write-layer-json.ps1" -LayerDir "%LAYERDIR%"
if errorlevel 1 exit /b 1

REM HKCU — no admin. SteamVR UI already reads this.
reg add "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%JSON%" /t REG_DWORD /d 0 /f >nul

REM HKLM — what working layers (OXRMC, Toolkit) use. ACE may ignore HKCU.
reg add "HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%JSON%" /t REG_DWORD /d 0 /f >nul 2>&1
if errorlevel 1 (
  echo HKLM Implicit needs elevation. Prompting UAC...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'reg.exe' -Verb RunAs -Wait -ArgumentList @('add','HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit','/v','%JSON%','/t','REG_DWORD','/d','0','/f')"
)

echo.
echo Registered:
echo   %JSON%
echo.
echo Manifest:
type "%JSON%"
echo.
echo HKCU:
reg query "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo HKLM:
reg query "HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo.
echo Do not set XR_API_LAYER_PATH or XR_ENABLE_API_LAYERS.
echo Disable: DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock=1
echo Do not load BuzzteeBear OXRMC. SteamVR Motion Smoothing OFF.
exit /b 0
