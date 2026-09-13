# SimSeatLock

Seat-lock motion compensation for **Assetto Corsa Evo + PSVR2** (SteamVR OpenXR). Same path works for any native-OpenXR title (AMS2, etc.) once the layer is loaded.

Keeps the virtual eyepoint in the bucket while a ProSimu P5 / SimTools platform moves.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

`T_cor` is IMU-to-eye from `config/geometry.json`. Preview off = identity (desktop numbers only). The OpenXR layer is what changes the headset.

- **v0.2 (now):** `SimSeatLock.Pose` — Witmotion serial (COM8 / 115200 default), Home (Z), shared-memory publish ≥250 Hz, desktop viz. Dropped = checksum/sync only.
- **v0 next:** `SimSeatLock.Layer` — identity OpenXR passthrough, write `Game.v1`, then inverse pose about CoR when Rig.v1 `Armed`.
- **v1:** predictive pose from the motion-command stream, IMU residual.
- **Not** SimHub Motion, **not** SRS IntelliComp, **not** FlyPT Mover, **not** SimTools mmap/UDP/serial as a pose source, **not** BuzzteeBear OXRMC as a dependency.

Sibling project (TV-canvas warp, do not merge):
https://github.com/Bohn101/psvr2-visual-motion-compensation

## If you are Grok

Read [`GROK.md`](GROK.md) first.

## Pose viz (v0)

`SimSeatLock.Pose.exe` is the desktop process.

| Channel | When it moves |
|---|---|
| SteamVR | Whenever SteamVR is running. Holds last pose when SteamVR exits. |
| Game OpenXR | When `SimSeatLock.Layer` writes `Local\SimSeatLock.Game.v1` from `xrLocateViews`. Holds last/zero until then. ACE, AMS2, or any OpenXR title. |
| T_rig | Witmotion and/or virtual numeric sliders. Home with **Z**. |
| Adjusted | Identity while preview is off. `T_cor * inv(T_rig) * inv(T_cor) * T_hmd` when preview is armed. Desktop numbers only — does not change the headset. |
| Delta | Headset vs adjusted. ~identity with preview off; tracks T_rig rotation with preview on. |

v0 does **not** read SimTools mmap, SimTools UDP, SimTools serial, or FlyPT Mover.

Publish layout: `dotnet publish` copies `config/pose.json` and `config/geometry.json` next to the exe.

## Build / run (Windows, .NET 8, Git CMD)

Existing clone:

```bat
cd /d C:\Users\Bohnster\sim-seat-lock
git fetch origin
git pull origin main
dotnet test SimSeatLock.sln -c Release
dotnet publish src\SimSeatLock.Pose\SimSeatLock.Pose.csproj -c Release -r win-x64 --self-contained false -o publish
publish\SimSeatLock.Pose.exe
```

## Layer (Git CMD)

```bat
cd /d C:\Users\Bohnster\sim-seat-lock
src\SimSeatLock.Layer\build-layer.cmd
src\SimSeatLock.Layer\install-layer.cmd
```

Requires VS 2022 C++ x64. Details: [`src/SimSeatLock.Layer/README.md`](src/SimSeatLock.Layer/README.md) and [`docs/LAYER.md`](docs/LAYER.md).

Edit `publish\config\pose.json` (COM port). Defaults: Witmotion COM8, 115200.

`--list-ports`, `--port COM8`, `--source virtual`, `--help` work from Git CMD / cmd.exe (AttachConsole).

Home and ACE Reset View only with the platform at SimTools neutral and preview **disarmed**. SteamVR Motion Smoothing off while testing. Do not load BuzzteeBear's layer at the same time.
