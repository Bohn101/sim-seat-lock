# SimSeatLock.Layer

OpenXR API layer loaded into ACE (SteamVR OpenXR runtime).

```
Witmotion → Pose → Local\SimSeatLock.Rig.v1 → layer xrLocateViews → title
title views → layer → Local\SimSeatLock.Game.v1 → Pose Game pane
```

Not OXRMC. Not SimHub / SRS / FlyPT / SimTools pose.

## Load

Implicit registry value (DWORD 0 = enabled):

`HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit`

`= C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json`

Disable: `DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock=1`

Or one-shot:

```bat
set XR_API_LAYER_PATH=C:\Users\Bohnster\sim-seat-lock\publish\layer
set XR_ENABLE_API_LAYERS=XR_APILAYER_NOVENDOR_sim_seat_lock
```

Do not load BuzzteeBear OXRMC at the same time.

## Arming

`Armed` in Rig.v1 is **Arm layer (headset)** in Pose. Preview checkbox is desktop numbers only.

Home / ACE Reset View only with platform at SimTools neutral and layer disarmed.
