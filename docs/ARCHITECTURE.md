# Architecture

## Why not OXRMC / SimHub / SRS

Those stacks feed washout or commanded targets through a third-party OpenXR layer with extra filters. That is why surge/sway numbers lag and never sit at zero. SimSeatLock uses measured geometry (v0) and a calibrated plant model (v1).

OpenXR API layers are an official Khronos hook. Motion compensation is not an official OpenXR feature. We use the hook; we own the math.

## v0 — measured

```
Witmotion → Pose process → Local\SimSeatLock.Rig.v1  → our OpenXR layer → SteamVR → title
title xrLocateViews → layer → Local\SimSeatLock.Game.v1 → Pose viz
SteamVR openvr_api   → Pose viz (independent of the title)
```

`SimSeatLock.Pose` publishes T_rig at ≥250 Hz. The layer is not required for IMU + SteamVR + virtual numeric. Game OpenXR numbers stay at last/zero until the layer writes the Game map.

Channels are not collapsed:

- SteamVR seated/standing `HmdMatrix34` → quat + metres
- Game `XrPosef` from `xrLocateViews` (ACE, AMS2, …)
- T_rig from Witmotion / virtual numeric (Euler deg + metres, gains, Home)
- Adjusted preview `inv(T_rig) * T_hmd` (off = identity)
- Delta = expected headset vs adjusted

## v1 — predictive + residual

```
SimTools / ProSimu command u(t)
        → plant model T_pred(t+τ)
        → layer uses T_pred immediately
Witmotion T_meas(t)
        → slow residual T_err = T_meas - T_pred_delayed
        → T_rig = T_pred + T_err
```

`τ` is identified from step tests (command vs IMU). Typical actuator+filter delay is tens of milliseconds. Compensating on `T_meas` alone is always late by `τ`. Compensating on `u(t)` without an IMU drifts when the plant saturates or the belt loads the chassis.

## Plant ID (v1)

Per axis:

- stroke min/max (mm or deg)
- max rate
- delay `τ`
- first-order time constant or rate limit
- direction-change backlash if visible
- peak |a| the chassis actually delivered (for sanity, not for washout)

Store in `config/plant.json`. Do not guess from game telemetry.
