@echo off
setlocal
cd /d "%~dp0..\..\publish\layer"
if not exist "%CD%\XR_APILAYER_NOVENDOR_sim_seat_lock.json" (
  echo Build the layer first: src\SimSeatLock.Layer\build-layer.cmd
  exit /b 1
)
reg add "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%CD%\XR_APILAYER_NOVENDOR_sim_seat_lock.json" /t REG_DWORD /d 0 /f
echo Registered implicit layer:
echo   %CD%\XR_APILAYER_NOVENDOR_sim_seat_lock.json
echo Disable with env DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock=1
echo Do not load BuzzteeBear OXRMC at the same time.
echo SteamVR Motion Smoothing OFF while testing.
exit /b 0
