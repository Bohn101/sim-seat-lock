# Le Mans Ultimate (Studio 397)

Same implicit layer as ACE. Same math. Same Witmotion `IPoseSource`.
No second pose source. Do not load OXRMC / OpenXR Toolkit / OpenComposite.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

## Process names (EngineWatch)

Task Manager on v1.4150 shows parent `Le Mans Ultimate` and child
`Le Mans Ultimate v1.4150`. `Process.ProcessName` for the official exe is
`Le Mans Ultimate` (spaces, no `.exe`). Seed `game_processes` with:

- `Le Mans Ultimate`
- `LeMansUltimate`

Do not add `VR Server` (SteamVR `vrserver`). Do not wipe COM8 or
geometry −0.27 / 1.15 when merging this list into a local `pose.json`.

## Launch (measured, do not assume)

Official Steam option: **Launch Le Mans Ultimate in Steam VR Mode**.
Do **not** pick the default **Play Le Mans Ultimate** (pancake).

LMU also offers **Launch Le Mans Ultimate in OpenXR Mode**. ACE is native
OpenXR through the SteamVR runtime
(`C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json`).
If Steam VR Mode is OpenVR-only, the Khronos implicit layer never
`LoadLibrary`s — `layer.log` will have no `Le Mans Ultimate.exe` attach.
Then retry **OpenXR Mode** with the same SteamVR ActiveRuntime. Still no
OpenComposite, no replaced `openvr_api.dll`, no `XR_API_LAYER_PATH` /
`XR_ENABLE_API_LAYERS` unless a measured test shows LMU needs them **and**
ACE still loads.

Historically `"Le Mans Ultimate.exe" +VR` is the same as Steam VR Mode.

## EAC

LMU ships Easy Anti-Cheat. Unofficial OpenXR layers have been blocked
(`Unable to start the game in VR mode. Check the log for more info.`).
Toolkit was later allow-listed; our DLL is not.

First proof, before any discovery hack:

1. Pose running, Preview / Arm **off**, platform at SimTools neutral.
2. SteamVR up, Motion Smoothing OFF, OXRMC blocked.
3. Launch LMU Steam VR Mode.
4. `publish/layer/layer.log` must contain `DllMain PROCESS_ATTACH` with
   `Le Mans Ultimate.exe` (or the versioned child) **and** `negotiate OK`.
5. Pose Game goes LIVE / `valid=True`. Status: `engine up (Le Mans Ultimate), layer live`.

If the VR error appears and `layer.log` has **no** LMU exe line, EAC or
the OpenVR path stripped the implicit layer. Do not flip HKLM vs HKCU or
set `XR_API_LAYER_PATH` until that log is missing after a Steam VR Mode
**and** an OpenXR Mode attempt. Discovery fix, if needed, is the ACE one:
absolute `library_path`, HKLM + HKCU Implicit = 0x0, SteamVR runtime.

Offline isolation (no online): shortcut to `Le Mans Ultimate.exe` with
`+VR` skips EAC. Only use that to separate "EAC blocked the DLL" from
"wrong launch option".

## Recenter

Platform at SimTools neutral, Arm / Preview off. Then LMU
**VR Centre head position** / Reset View **and** SimSeatLock Home (Z).

## Start order

Pose → SteamVR (layer On, smoothing off, OXRMC blocked) → LMU Steam VR Mode.

## A/B after LIVE

Arm layer. Lean the platform at the eye: mostly `dx`, horizon level.
