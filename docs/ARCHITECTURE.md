# Architecture

## Why not OXRMC / SimHub / SRS

Those stacks feed washout or commanded targets through a third-party OpenXR layer with extra filters. That is why surge/sway numbers lag and never sit at zero. SimSeatLock uses measured geometry (v0) and a calibrated plant model (v1).

OpenXR API layers are an official Khronos hook. Motion compensation is not an official OpenXR feature. We use the hook; we own the math.

## v0 — measured

```
Witmotion → Pose process → shared memory → our OpenXR layer → SteamVR → ACE
```

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
