# SimSeatLock

Seat-lock motion compensation for **Assetto Corsa Evo + PSVR2** (SteamVR OpenXR).

Keeps the virtual eyepoint in the bucket while a ProSimu P5 / SimTools platform moves.

- **v0 (now):** measured pose from the Witmotion IMU already on the chassis.
- **v1 (next):** predictive pose from the motion-command stream *before* actuators move, calibrated against the IMU.
- **Not** SimHub Motion, **not** SRS IntelliComp, **not** BuzzteeBear OXRMC as a dependency.

Sibling project (TV-canvas warp for PSVR2 cameras):
https://github.com/Bohn101/psvr2-visual-motion-compensation

## If you are Grok

Read [`GROK.md`](GROK.md) first. That file is the runbook for this repo.

## Hardware context

- ProSimu P5 MP (CAN), SimTools 3
- SIM Experience SIM Commander G-belt (independent of MC)
- Witmotion IMU at platform CoR (between actuators)
- PSVR2 on PC via SteamVR as the OpenXR runtime
- ACE is native OpenXR

## Status

Seed only. Pose process and OpenXR layer are not implemented yet.
