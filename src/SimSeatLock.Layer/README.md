# SimSeatLock.Layer

Thin OpenXR API layer. Pattern from mbucchia/OpenXR-Layer-Template
(`xrNegotiateLoaderApiLayerInterface`, implicit JSON, `xrLocateViews` hook).
Not OpenXR-MotionCompensation. Not a SimTools / FlyPT pose tap.

## What this increment does

1. Identity `xrLocateViews` passthrough (call next, return views).
2. Write `Local\\SimSeatLock.Game.v1` so Pose Game channel leaves HOLD.
3. If `Local\\SimSeatLock.Rig.v1` `Armed != 0`, apply

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

about the CoR in `geometry.json` / Rig block `EyeX/Y/Z`.

Preview in Pose is still desktop-only. Use **Arm layer (headset)** to set `Armed`.

Discovery: **HKLM only**, absolute `library_path`, static CRT (`/MT`).
Do not also register HKCU — SteamVR then shows two identical rows.
`DllMain` logs to `publish/layer/layer.log` as soon as any process LoadLibrarys the DLL.
Do **not** set `XR_API_LAYER_PATH` or `XR_ENABLE_API_LAYERS`.

Titles: ACE (proven), LMU (same DLL; EAC blocks it on the protected launcher).
See [`docs/LMU.md`](../../docs/LMU.md).

## Build / register (Git Bash)

```bash
cd /c/Users/Bohnster/sim-seat-lock
git fetch origin
git pull origin main
# stash config/geometry.json and config/pose.json first if you have local COM8 / -0.27
cmd.exe //c src/SimSeatLock.Layer/build-layer.cmd
cmd.exe //c src/SimSeatLock.Layer/install-layer.cmd
cmd.exe //c src/SimSeatLock.Layer/diagnose-layer.cmd
```

Requires VS 2022 C++ (v143) x64. `install-layer.cmd` will prompt UAC for HKLM.
Output:

`publish\\layer\\XR_APILAYER_NOVENDOR_sim_seat_lock.dll`

Disable for LMU online: `cmd.exe //c src/SimSeatLock.Layer/disable-layer.cmd`

## Test

1. SteamVR Motion Smoothing off.
2. Do not load BuzzteeBear OXRMC. Do not UNBLOCK OXRMC.
3. Start `publish/SimSeatLock.Pose.exe`, platform at SimTools neutral, Home (Z) with compensation disarmed.
4. SteamVR (one SimSeatLock row, On) then ACE **Play Assetto Corsa EVO**, or LMU offline `Le Mans Ultimate.exe` +VR.
5. Success: `publish/layer/layer.log` has `DllMain PROCESS_ATTACH` + `negotiate OK` for that exe, Pose Game LIVE (`valid=True`).
6. Arm layer only after Home. Disarm before Reset View / Home.
