# Automobilista 2 (Reiza)

Same implicit layer as ACE / LMU. Same math. Same Witmotion `IPoseSource`.
No second pose source. Do not load OXRMC / OpenXR Toolkit / OpenComposite
on ACE or LMU.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

## What the four lamps mean on AMS2

Measured 2026-09-25 on PSVR2 + SteamVR OpenXR + `AMS2AVX`:

| Lamp | State | Meaning |
|---|---|---|
| IMU COM8 | green | Witmotion serial live. Independent of the title. |
| SteamVR | green | OpenVR runtime connected. AMS2 talks to SteamVR this way. |
| Engine AMS2AVX | green | EngineWatch saw `AMS2AVX`. Presence only. |
| Layer | **red** | `Local\SimSeatLock.Game.v1` has no writer. |

Pose Game pane: `HOLD engine up (AMS2AVX), waiting for layer`, `Valid=False`.

That is **not** a Pose crash and not a shared-memory race. Restarting
`SimSeatLock.Pose.exe` cannot turn Layer green. The layer DLL is loaded
by the **title** through the Khronos OpenXR loader. `AMS2AVX.exe` on the
stock SteamVR launch path never calls that loader.

Proof the DLL never entered the process:

```
publish/layer/layer.log
```

must contain `DllMain PROCESS_ATTACH` and `negotiate OK` with `exe=`
pointing at `AMS2AVX.exe` (or `AMS2.exe`). If the last attach is ACE / LMU
and there is no AMS2 line after you sat in the car, AMS2 did not
`LoadLibrary` the layer. SteamVR listing SimSeatLock On is not the same
as AMS2 negotiating it.

ACE and LMU already do that negotiation. That is why those titles go
four-green on the same install.

## Why AMS2 is different

Madness-engine VR is OpenVR (`openvr_api.dll` under `Automobilista 2\x64`)
and / or LibOVR. Steam launch options:

- `steam://launch/1066890/vr` — SteamVR / OpenVR
- `steam://launch/1066890/othervr` — Oculus / other VR
- pancake — no headset

None of those create an OpenXR instance. Implicit layers hook OpenXR.
An OpenVR game never calls OpenXR, so the layer never loads. Nothing
new appears in `layer.log` because the layer was never running.

`game_processes` already lists `AMS2AVX` and `AMS2`. That only drives
the Engine lamp.

## Do not do these

- Do not set `XR_API_LAYER_PATH` or `XR_ENABLE_API_LAYERS` (breaks ACE).
- Do not replace `openvr_api.dll` inside ACE or LMU.
- Do not use OpenComposite as a global SteamVR switch on a PSVR2 box.
  PSVR2 is a SteamVR device. The OpenXR runtime must stay
  `steamxr_win64.json`.
- Do not invent a second injector (OpenVR driver, overlay warp, SimTools
  pose tap). Seat-lock stays one layer + one pose process.

## Optional experiment (AMS2 folder only)

Community titles that are OpenVR-only sometimes grow an OpenXR instance
when `Automobilista 2\x64\openvr_api.dll` is replaced with OpenComposite's
64-bit `openvr_api.dll`, **and** SteamVR remains the active OpenXR runtime.

If you try it:

1. Copy the stock `openvr_api.dll` to `openvr_api.dll.bak`.
2. Drop OpenComposite's 64-bit DLL next to `AMS2AVX.exe` only.
3. SteamVR → OpenXR → make SteamVR the default runtime.
4. Layer registry still HKLM only. `disable-layer.cmd` still works.
5. Launch **SteamVR mode**, sit in the car, then read `layer.log`.
6. Success: `DllMain PROCESS_ATTACH ... AMS2AVX.exe` + `negotiate OK`,
   Pose Layer lamp green, Game `Valid=True`.
7. Restore `openvr_api.dll.bak` before touching ACE / LMU / other titles
   in that folder tree. Do not leave OpenComposite in a shared SteamVR
   override.

This path is unproven on this PSVR2 box. Measure `layer.log` before
calling it a product feature. If AMS2 then fails to start VR, restore
the bak file — that is an OpenComposite / runtime problem, not a Pose bug.

## Recenter

Platform at SimTools neutral, Arm off. AMS2 recenter + SimSeatLock Home (Z).
