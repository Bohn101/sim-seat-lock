@echo off
setlocal EnableExtensions
echo === SimSeatLock layer discovery ===
echo.
echo ActiveRuntime HKLM:
reg query "HKLM\SOFTWARE\Khronos\OpenXR\1" /v ActiveRuntime 2>nul
echo ActiveRuntime HKCU:
reg query "HKCU\SOFTWARE\Khronos\OpenXR\1" /v ActiveRuntime 2>nul
echo.
echo Implicit HKCU:
reg query "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo Implicit HKLM:
reg query "HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo.
echo User XR_* env (these must NOT be set):
reg query "HKCU\Environment" /v XR_API_LAYER_PATH 2>nul
reg query "HKCU\Environment" /v XR_ENABLE_API_LAYERS 2>nul
reg query "HKCU\Environment" /v XR_RUNTIME_JSON 2>nul
reg query "HKCU\Environment" /v DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock 2>nul
echo Process env:
if defined XR_API_LAYER_PATH echo XR_API_LAYER_PATH=%XR_API_LAYER_PATH%
if defined XR_ENABLE_API_LAYERS echo XR_ENABLE_API_LAYERS=%XR_ENABLE_API_LAYERS%
if defined XR_RUNTIME_JSON echo XR_RUNTIME_JSON=%XR_RUNTIME_JSON%
if not defined XR_API_LAYER_PATH if not defined XR_ENABLE_API_LAYERS echo   XR_API_LAYER_PATH / XR_ENABLE_API_LAYERS unset (good)
echo.
echo Publish files:
dir /b "C:\Users\Bohnster\sim-seat-lock\publish\layer" 2>nul
echo.
if exist "C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json" (
  echo Manifest:
  type "C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json"
  echo.
)
if exist "C:\Users\Bohnster\sim-seat-lock\publish\layer\layer.log" (
  echo layer.log last 20 lines:
  powershell -NoProfile -Command "Get-Content -LiteralPath 'C:\Users\Bohnster\sim-seat-lock\publish\layer\layer.log' -Tail 20"
  echo.
  echo LMU / ACE attach lines:
  findstr /I /C:"Le Mans Ultimate" /C:"AssettoCorsa" /C:"negotiate OK" "C:\Users\Bohnster\sim-seat-lock\publish\layer\layer.log"
) else (
  echo layer.log MISSING — title never LoadLibrary'd the DLL.
)
echo.
echo If layer.log is still missing after ACE / LMU VR: the title is not chaining
echo Khronos implicit layers (OpenVR launch, EAC strip, or a loader that skips HKCU/HKLM).
exit /b 0
