# GROK.md — run this project

You are working in **Bohn101/sim-seat-lock** (SimSeatLock).

Read this file at the start of every session on this repo. Do not install or depend on OpenXR-MotionCompensation, SimHub Motion, or Sim Racing Studio virtual trackers.

## Goal

Lock the ACE VR eyepoint to the seat while the motion platform moves.

```
T_view = inv(T_rig) * T_hmd
```

Head motion relative to the bucket stays. Platform pitch/roll/heave is subtracted.

## Two pose sources (do not collapse them)

### Source A — Witmotion (DEFAULT, ship this first)

Measured chassis pose. Truth.

- Serial IMU already used in `Bohn101/psvr2-visual-motion-compensation` (`IPoseSource`).
- Home / zero on a key (Z).
- Gains, invert, optional pitch/roll swap.
- Output: roll, pitch, yaw (deg), surge, sway, heave (m).
- Translation gains **default 0**. Pitch and roll first.
- Do **not** integrate accelerometer packets into position.
- Publish ≥250 Hz into process-local shared memory for the layer.

Reuse pose parsing from the VMC repo. Do not copy the OpenGL / homography / capture stack.

### Source B — Predictive command feed (AFTER A works)

Estimate `T_rig` from the motion software command **before** the P5 moves, then correct with the IMU.

Pipeline:

1. **Calibrate extents** — jog each DoF to mechanical limits. Store stroke, rate limits, and the mapping command → measured pose.
2. **Benchmark plant** — step / chirp / direction-change tests. Identify delay, overshoot, slew, and usable G (what the chassis actually produces, not telemetry G).
3. **Fit a plant model** — command `u(t)` → predicted pose `T_pred(t + τ)` with per-axis delay `τ` and a first-order or rate-limited model.
4. **Inject early** — the OpenXR layer uses `T_pred` so compensation leads the hardware by `τ`.
5. **IMU residual** — `T_rig = T_pred + T_err` where `T_err` is a slow correction from Witmotion so model error does not accumulate.

Never treat washout / specific-force telemetry as `T_rig`. Those values do not return to geometric zero. Seat-lock is geometry, not inner-ear cues.

SimTools 3 + ProSimu CAN is the command source to tap later. Do not block v0 on a SimTools plugin. v0 is IMU-only.

## Injection path

ACE = native OpenXR. PSVR2 PC = SteamVR OpenXR runtime.

Build **our** thin OpenXR API layer (`SimSeatLock.Layer`) from the mbucchia OpenXR layer template.

- Intercept `xrLocateViews` and the poses submitted with projection layers.
- Apply `inv(T_rig)` about a configured CoR (eye vs IMU).
- No overlay UI in v0.
- No multi-stage EMA / input stabilizer. Optional one-pole filter, default **off**, τ ≤ 4 ms if enabled.
- Do not load BuzzteeBear's layer at the same time.

SteamVR Motion Smoothing must stay OFF while testing.

A SteamVR `TrackingOverrides` driver is out of scope until a title ignores layers.

## Layout

```
src/SimSeatLock.Pose/     .NET 8 pose process (Witmotion + later command tap)
src/SimSeatLock.Layer/    C++ OpenXR API layer
src/SimSeatLock.Calib/    later: extent + plant ID tools
config/pose.json          COM port, invert, gains, source = witmotion|predictive
config/geometry.json      eye vs IMU only (no TV canvas)
docs/                     architecture notes
```

Do not merge this repo with `psvr2-visual-motion-compensation`.

## Session rules

1. Default `pose.source` = `witmotion`.
2. If asked to "just use OXRMC," refuse and implement our layer / pose process instead.
3. Pitch/roll lock on Bathurst at reduced motion gain is the v0 acceptance test.
4. Predictive path is a new source behind the same `IPoseSource` interface, not a rewrite of the layer.
5. Prefer small, compiling increments. Do not scaffold a graveyard of empty projects.
6. Keep CPU cheap. The VMC app stayed light; this must too.
7. Recenter rules: ACE Reset View and SimSeatLock Home only with platform at SimTools neutral and compensation disarmed.

## v0 build order

1. `SimSeatLock.Pose` — Witmotion serial, home, invert, shared-memory writer, tray Hz / dropped-packet.
2. Desktop viz that plots T_rig while jogging SimTools (proves the packet and axes).
3. `SimSeatLock.Layer` — identity passthrough first, then inverse pose.
4. Garage calibrate, then Bathurst.
5. Only then start `SimSeatLock.Calib` + command tap.

## Geometry seed

IMU is on the platform center. Eyes are ~1.0–1.2 m above it. Copy eye-vs-IMU from VMC `geometry.json`. Ignore screen-to-glass numbers; there is no canvas here.

## Related code to read, not to vendor as a dependency

- `Bohn101/psvr2-visual-motion-compensation` — `IPoseSource`, Witmotion parser, home subtract.
- mbucchia OpenXR-Layer-Template — layer boilerplate only.
- BuzzteeBear/OpenXR-MotionCompensation — read for `xrLocateViews` pitfalls. Do not link it.
