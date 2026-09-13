# Architecture

## Why not OXRMC / SimHub / SRS / FlyPT / SimTools pose taps

Those stacks feed washout or commanded targets through a third-party OpenXR layer with extra filters. That is why surge/sway numbers lag and never sit at zero. SimSeatLock uses measured geometry (v0) and a calibrated plant model (v1).

**v0 does not use SimTools mmap, SimTools UDP, SimTools serial, or FlyPT Mover as a pose source.** Witmotion serial is the only live chassis source. Virtual numeric is a debug overlay / stand-in.

OpenXR API layers are an official Khronos hook. Motion compensation is not an official OpenXR feature. We use the hook; we own the math.

## v0 — measured

```
Witmotion → Pose process → Local\SimSeatLock.Rig.v1  → our OpenXR layer → SteamVR → title
title xrLocateViews → layer → Local\SimSeatLock.Game.v1 → Pose viz
SteamVR openvr_api   → Pose viz (independent of the title)
```

`SimSeatLock.Pose` publishes T_rig at ≥250 Hz. The layer is not required for IMU + SteamVR + virtual numeric. Game OpenXR numbers stay at last/zero until the layer writes the Game map.

Math (CoR = IMU, `T_cor` = imu-to-eye):

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

Channels are not collapsed:

- SteamVR seated/standing `HmdMatrix34` → quat + metres
- Game `XrPosef` from `xrLocateViews` (ACE, AMS2, …)
- T_rig from Witmotion / virtual numeric (Euler deg + metres, gains, Home)
- Adjusted preview about CoR (off = identity)
- Delta = expected headset vs adjusted

Dropped packets count checksum / sync failures only. Accel, gyro, and port status packets are valid protocol traffic, not drops.

## v1 — predictive + residual

```
SimTools / ProSimu command u(t)
        → plant model T_pred(t+τ)
        → layer uses T_pred immediately
Witmotion T_meas(t)
        → slow residual T_err = T_meas - T_pred_delayed
        → T_rig = T_pred + T_err
```

`τ` is identified from step tests (command vs IMU). Compensating on `T_meas` alone is always late by `τ`. Compensating on `u(t)` without an IMU drifts when the plant saturates.

## Geometry

IMU is CoR (platform center). Eyes ~1.0–1.2 m above.

| Field | Meaning |
|---|---|
| `eye_forward_m` | +front / −aft |
| `eye_right_m` | +right / −left |
| `eye_up_m` | +up (default 1.10) |
| `imu_to_eye_m` | derived OpenXR metres: X = right, Y = up, Z = −forward |

## Plant ID (v1)

Per axis: stroke min/max, max rate, delay `τ`, first-order time constant or rate limit, backlash if visible. Store in `config/plant.json`. Do not guess from game telemetry.
