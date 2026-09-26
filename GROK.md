# GROK.md — run this project

You are working in **Bohn101/sim-seat-lock** (SimSeatLock).

Read this file at the start of every session on this repo. Do not install or depend on OpenXR-MotionCompensation, SimHub Motion, Sim Racing Studio, FlyPT Mover, or SimTools 3 mmap/UDP/serial as a pose source.

## Goal

Lock the VR eyepoint to the seat while the motion platform moves. Proven on ACE. Same Witmotion `IPoseSource` for every title. Injection path is chosen from the running exe — not a second pose source.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

`T_cor` is the IMU-to-eye translation from `config/geometry.json`. Head motion relative to the bucket stays. Platform pitch/roll/heave is subtracted about the CoR.

## Two pose sources (do not collapse them)

### Source A — Witmotion (DEFAULT, ship this first)

Measured chassis pose. Truth.

- Serial IMU. Same protocol family as the VMC repo (`IPoseSource`), not a project reference.
- Home / zero on a key (Z).
- Gains, invert, optional pitch/roll swap.
- Output: roll, pitch, yaw (deg), surge, sway, heave (m).
- Translation gains **default 0**. Pitch and roll first.
- Do **not** integrate accelerometer packets into position.
- Publish ≥250 Hz into process-local shared memory for whichever injector is live.
- Dropped counter = checksum/sync failures only. Valid accel/gyro/port packets are not drops.

Reuse pose *parsing* from the VMC repo. Do not copy the OpenGL / homography / capture stack. Do not add `psvr2-visual-motion-compensation` as a dependency. Do not merge the repos.

### Source B — Predictive command feed (AFTER A works)

Estimate `T_rig` from the motion software command **before** the P5 moves, then correct with the IMU. Same `IPoseSource`. Not in v0.

Never treat washout / specific-force telemetry as `T_rig`. Seat-lock is geometry, not inner-ear cues.

## Injection paths (chosen from the exe)

`EngineWatch.InjectionPath(processName)`:

- `openxr` — Assetto Corsa Evo, Le Mans Ultimate. Implicit layer
  `XR_APILAYER_NOVENDOR_sim_seat_lock`. See `docs/LMU.md`.
- `openvr` — AMS2 / AMS2AVX, iRacing. Drop-in `openvr_api.dll` in the
  title's x64 folder (`src/SimSeatLock.OpenVR`). See `docs/AMS2.md`.
- `unknown` — Engine lamp only until we measure how that title talks VR.

PSVR2 PC = SteamVR runtime on both paths. The OpenVR proxy forwards
Valve `openvr_api.stock.dll`; it does not use OpenComposite and does not
replace the OpenXR runtime.

Both injectors apply the same `T_view` product and write
`Local\SimSeatLock.Game.v1`. Pose Layer lamp = that heartbeat.

Do not load BuzzteeBear's layer at the same time.
SteamVR Motion Smoothing OFF while testing.

## Layout

```
src/SimSeatLock.Pose/     .NET 8 pose process (Witmotion + later command tap)
src/SimSeatLock.Layer/    C++ OpenXR API layer (ACE / LMU)
src/SimSeatLock.OpenVR/   C++ openvr_api.dll proxy (AMS2)
src/SimSeatLock.Calib/    later: extent + plant ID tools
config/pose.json          COM port, invert, gains, source = witmotion|predictive
config/geometry.json      eye vs IMU only (no TV canvas)
docs/                     architecture notes
```

## Session rules

1. Default `pose.source` = `witmotion`. Default port COM8, 115200.
2. If asked to "just use OXRMC," refuse and implement our layer / pose process instead.
3. Pitch/roll lock on Bathurst at reduced motion gain is the v0 acceptance test (ACE). LMU A/B is lean at the eye → mostly dx, horizon level. AMS2 proof is `simseatlock-openvr.log` + Layer green, then the same lean test.
4. Predictive path is a new source behind the same `IPoseSource` interface, not a rewrite of either injector.
5. Prefer small, compiling increments. Do not scaffold a graveyard of empty projects.
6. Keep CPU cheap.
7. Recenter rules: title Reset View / VR Centre head position and SimSeatLock Home only with platform at SimTools neutral and compensation disarmed.
8. Terminal commands for the user: Git Bash only (not Git CMD / cmd.exe).
9. Do not use OpenXR-MotionCompensation, SimHub Motion, SRS, FlyPT Mover, or SimTools mmap/UDP/serial as a pose source.
10. Do not set `XR_API_LAYER_PATH` / `XR_ENABLE_API_LAYERS` unless a measured test shows LMU needs them **and** ACE still loads.
11. OpenVR proxy is AMS2-folder-only. Never drop it into ACE or LMU. Restore with `restore-ams2.cmd`.

## v0 build order

1. `SimSeatLock.Pose` — Witmotion serial, home, invert, shared-memory writer, tray Hz / dropped-packet. (v0.2)
2. Desktop viz that plots T_rig while jogging the platform (proves the packet and axes).
3. `SimSeatLock.Layer` — identity passthrough first, write Game.v1, then inverse pose about CoR.
4. Garage calibrate, then Bathurst. LMU uses the same installed layer; first proof is LoadLibrary + Game LIVE.
5. AMS2 uses `SimSeatLock.OpenVR` drop-in; first proof is `simseatlock-openvr.log` + Game LIVE.
6. Only then start `SimSeatLock.Calib` + command tap.

## Geometry seed

IMU is on the platform center (CoR). Eyes are ~1.0–1.2 m above it.

`geometry.json` labels:

- `eye_forward_m` (+front / −aft)
- `eye_right_m` (+right / −left)
- `eye_up_m` (default 1.10)
- `imu_to_eye_m` derived: X = right, Y = up, Z = −forward

## Related code to read, not to vendor as a dependency

- `Bohn101/psvr2-visual-motion-compensation` — `IPoseSource`, Witmotion parser, home subtract.
- mbucchia OpenXR-Layer-Template — layer boilerplate only.
- BuzzteeBear/OpenXR-MotionCompensation — read for `xrLocateViews` pitfalls. Do not link it.
- Valve openvr.h — IVRCompositor WaitGetPoses slot 2 is the AMS2 hook.
