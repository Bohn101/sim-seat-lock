# SimSeatLock.Layer

Thin OpenXR API layer. Pattern from mbucchia/OpenXR-Layer-Template
(`xrNegotiateLoaderApiLayerInterface`, implicit JSON, `xrLocateViews` hook).
Not OpenXR-MotionCompensation. Not a SimTools / FlyPT pose tap.

## What this increment does

1. Identity `xrLocateViews` passthrough (call next, return views).
2. Write `Local\SimSeatLock.Game.v1` so Pose Game channel leaves HOLD.
3. If `Local\SimSeatLock.Rig.v1` `Armed != 0`, apply

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

about the CoR in `geometry.json` / Rig block `EyeX/Y/Z`.

Preview in Pose is still desktop-only. Use **Arm layer (headset)** to set `Armed`.

## Build / register (Git CMD)

```bat
cd /d C:\Users\Bohnster\sim-seat-lock
git fetch origin
git pull origin main
src\SimSeatLock.Layer\build-layer.cmd
src\SimSeatLock.Layer\install-layer.cmd
```

Requires VS 2022 C++ (v143) x64. Output:

`publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.dll`

## Test

1. SteamVR Motion Smoothing off.
2. Do not load BuzzteeBear OXRMC.
3. Start `publish\SimSeatLock.Pose.exe`, platform at SimTools neutral, Home (Z) with compensation disarmed.
4. Launch ACE VR. Pose Game channel should go LIVE (`layer seq N`).
5. Arm layer only after Home. Disarm before Reset View / Home.
