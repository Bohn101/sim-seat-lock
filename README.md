# SimSeatLock

Seat-lock motion compensation for **Assetto Corsa Evo + PSVR2** (SteamVR OpenXR). Same path works for any native-OpenXR title (AMS2, etc.) once the layer is loaded.

Keeps the virtual eyepoint in the bucket while a ProSimu P5 / SimTools platform moves.

```
T_view = inv(T_rig) * T_hmd
```

- **v0 (now):** `SimSeatLock.Pose` — Witmotion serial, Home (Z), shared-memory publish ≥250 Hz, desktop viz.
- **v0 next:** `SimSeatLock.Layer` — identity OpenXR passthrough, then inverse pose.
- **v1:** predictive pose from the motion-command stream, IMU residual.
- **Not** SimHub Motion, **not** SRS IntelliComp, **not** BuzzteeBear OXRMC as a dependency.

Sibling project (TV-canvas warp, do not merge):
https://github.com/Bohn101/psvr2-visual-motion-compensation

## If you are Grok

Read [`GROK.md`](GROK.md) first.

## Pose viz (v0)

`SimSeatLock.Pose.exe` is the VMC-style tray/desktop process.

| Channel | When it moves |
|---|---|
| SteamVR | Whenever SteamVR is running. Holds last pose when SteamVR exits. |
| Game OpenXR | When `SimSeatLock.Layer` writes `Local\SimSeatLock.Game.v1` from `xrLocateViews`. Holds last/zero until then. ACE, AMS2, or any OpenXR title. |
| T_rig | Witmotion and/or virtual numeric sliders. Home with **Z**. |
| Adjusted | Identity while preview is off. `inv(T_rig)*T_hmd` when preview is armed. |
| Delta | Headset vs adjusted. ~identity with preview off; tracks T_rig rotation with preview on. |

Publish layout matches VMC: `dotnet publish` copies `config/pose.json` and `config/geometry.json` next to the exe.

## Build / run (Windows, .NET 8)

```bat
cd %USERPROFILE%
git clone https://github.com/Bohn101/sim-seat-lock.git
cd sim-seat-lock
dotnet test SimSeatLock.sln -c Release
dotnet publish src\SimSeatLock.Pose\SimSeatLock.Pose.csproj -c Release -r win-x64 --self-contained false -o publish
publish\SimSeatLock.Pose.exe
```

Edit `publish\config\pose.json` (COM port) the same way VMC uses `publish\config\appsettings.json`.

`--list-ports`, `--port COM8`, `--source virtual` work from a console.

Home and ACE Reset View only with the platform at SimTools neutral and preview **disarmed**.
