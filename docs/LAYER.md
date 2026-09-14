# SimSeatLock.Layer

OpenXR API layer loaded into ACE (SteamVR OpenXR runtime).

```
Witmotion → Pose → Local\SimSeatLock.Rig.v1 → layer xrLocateViews → title
title views → layer → Local\SimSeatLock.Game.v1 → Pose Game pane
```

Not OXRMC. Not SimHub / SRS / FlyPT / SimTools pose.

## Load

Implicit registry, DWORD 0 = enabled. Working layers live in **HKLM** (ACE may ignore HKCU).

```
HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit
HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit
```

Value name = absolute path of

`C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json`

`install-layer.cmd` writes an **absolute** `library_path` into that JSON and registers both hives (UAC for HKLM).

Disable: `DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock=1`

**Do not set `XR_API_LAYER_PATH` or `XR_ENABLE_API_LAYERS`.** `XR_API_LAYER_PATH` replaces default explicit-layer search and has taken Steam-launched ACE to a flat monitor.

```
reg delete "HKCU\Environment" /F /V XR_API_LAYER_PATH
reg delete "HKCU\Environment" /F /V XR_ENABLE_API_LAYERS
```

Do not load BuzzteeBear OXRMC at the same time.

## Arming

`Armed` in Rig.v1 is **Arm layer (headset)** in Pose. Preview checkbox is desktop numbers only.

Home / ACE Reset View only with platform at SimTools neutral and layer disarmed.

## Proof the DLL loaded

`publish/layer/layer.log` must contain `DllMain PROCESS_ATTACH` and `negotiate OK` after sitting in the car. Pose Game goes LIVE / valid=True.

If the log is missing, ACE never `LoadLibrary`'d the DLL. SteamVR listing the layer is not the same as ACE calling `xrNegotiateLoaderApiLayerInterface`.
