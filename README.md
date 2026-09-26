# SimSeatLock

Lightweight seat-lock motion compensation for VR sim racing and flight.
One pose process. One measured chassis sensor today; commanded platform
pose later, same interface. Injection path is chosen from the running
exe: implicit OpenXR layer for ACE / LMU, OpenVR `openvr_api.dll` proxy
for AMS2.

The headset stays in the bucket while a 6DOF platform (ProSimu P5 /
SimTools) moves.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

`T_cor` is IMU-to-eye from `config/geometry.json`. Head motion relative
to the seat is kept. Platform rotation and translation are subtracted
about the center of rotation. Preview off = identity. The injector is
what changes the headset.

Ships against SteamVR on PSVR2. Proven on Assetto Corsa Evo (OpenXR).
Same OpenXR DLL on Le Mans Ultimate when the title loads OpenXR (EAC
blocks the protected launcher; offline `Le Mans Ultimate.exe +VR`).
AMS2 is OpenVR — install `src/SimSeatLock.OpenVR` into
`Automobilista 2\x64`. See [`docs/AMS2.md`](docs/AMS2.md).

| Now (v0.2.3) | Next |
|---|---|
| Witmotion serial IMU (COM8 / 115200), Home, ≥250 Hz shared memory | Predictive `T_rig` from the motion-command stream, IMU residual |
| OpenXR layer (ACE / LMU) + OpenVR proxy (AMS2), same T_view | Same injectors, second `IPoseSource` |
| Exe-based path: ACE/LMU = openxr, AMS2/iRacing = openvr | More titles after we measure how they talk VR |

**Not** SimHub Motion, SRS IntelliComp, FlyPT Mover, or SimTools as a
pose source. **Not** BuzzteeBear OXRMC. Those stacks feed washout or a
third-party layer. SimSeatLock is geometry.

Sibling (TV-canvas warp, do not merge):
https://github.com/Bohn101/psvr2-visual-motion-compensation

Studio 397 / Epic allow-list draft: [`docs/S397-allowlist.txt`](docs/S397-allowlist.txt).
LMU notes: [`docs/LMU.md`](docs/LMU.md).
AMS2 notes: [`docs/AMS2.md`](docs/AMS2.md).

## If you are Grok

Read [`GROK.md`](GROK.md) first.

## Pose viz (v0)

`SimSeatLock.Pose.exe` is the desktop process.

| Channel | When it moves |
|---|---|
| SteamVR | Whenever SteamVR is running. Holds last pose when SteamVR exits. |
| Game | When an injector writes `Local\SimSeatLock.Game.v1`. Holds last/zero until then. |
| T_rig | Witmotion and/or virtual numeric sliders. Home with **Z**. |
| Adjusted | Identity while preview is off. Compensated when armed. Desktop numbers only. |
| Delta | Headset vs adjusted. |

Publish layout: `dotnet publish` copies `config/pose.json` and `config/geometry.json` next to the exe.

## Build / run (Windows, .NET 8)

Git Bash:

```bash
cd /c/Users/Bohnster/sim-seat-lock
git pull origin main
cmd.exe //c src/SimSeatLock.Layer/build-layer.cmd
cmd.exe //c src/SimSeatLock.Layer/install-layer.cmd
cmd.exe //c src/SimSeatLock.OpenVR/build-openvr.cmd
cmd.exe //c src/SimSeatLock.OpenVR/install-ams2.cmd
```

```bat
cd /d C:\Users\Bohnster\sim-seat-lock
dotnet test SimSeatLock.sln -c Release
dotnet publish src\SimSeatLock.Pose\SimSeatLock.Pose.csproj -c Release -r win-x64 --self-contained false -o publish
publish\SimSeatLock.Pose.exe
```

Requires VS 2022 C++ x64. Details: [`src/SimSeatLock.Layer/README.md`](src/SimSeatLock.Layer/README.md), [`src/SimSeatLock.OpenVR/README.md`](src/SimSeatLock.OpenVR/README.md), [`docs/LAYER.md`](docs/LAYER.md), [`docs/AMS2.md`](docs/AMS2.md).

Defaults: Witmotion COM8, 115200. Home and title Reset View only at SimTools
neutral with compensation disarmed. SteamVR Motion Smoothing off while
testing. Do not load OXRMC at the same time.
