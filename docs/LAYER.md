# SimSeatLock.Layer

OpenXR API layer loaded into ACE / LMU / any native-OpenXR title (SteamVR OpenXR runtime).

```
Witmotion → Pose → Local\SimSeatLock.Rig.v1 → layer xrLocateViews → title
title views → layer → Local\SimSeatLock.Game.v1 → Pose Game pane
```

Not OXRMC. Not SimHub / SRS / FlyPT / SimTools pose.

## Load

One implicit registration. DWORD 0 = enabled. Canonical hive is **HKLM**
(ACE ignores HKCU; Toolkit / OXRMC also use HKLM).

```
HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit
```

Value name = absolute path of

`C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json`

`install-layer.cmd` writes an **absolute** `library_path` and enables HKLM.
It **deletes** the same JSON from HKCU. Writing both hives made SteamVR list
the layer twice and the loader could negotiate it twice.

Disable without deleting files: `disable-layer.cmd` (DWORD 1 both hives).

Disable env: `DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock=1`

**Do not set `XR_API_LAYER_PATH` or `XR_ENABLE_API_LAYERS`.** `XR_API_LAYER_PATH` replaces default explicit-layer search and has taken Steam-launched ACE to a flat monitor.

```
reg delete "HKCU\Environment" /F /V XR_API_LAYER_PATH
reg delete "HKCU\Environment" /F /V XR_ENABLE_API_LAYERS
```

Do not load BuzzteeBear OXRMC at the same time.

## Arming

`Armed` in Rig.v1 is **Arm layer (headset)** in Pose. Preview checkbox is desktop numbers only.

Home / title Reset View only with platform at SimTools neutral and layer disarmed.

## Proof the DLL loaded

`publish/layer/layer.log` must contain `DllMain PROCESS_ATTACH` and `negotiate OK` after sitting in the car. Pose Game goes LIVE / valid=True.

If the log is missing, the title never `LoadLibrary`'d the DLL. SteamVR listing the layer is not the same as the title calling `xrNegotiateLoaderApiLayerInterface`.

LMU: see [`docs/LMU.md`](LMU.md). EAC blocks this DLL on `start_protected_game`. Offline proof is `Le Mans Ultimate.exe` +VR.
