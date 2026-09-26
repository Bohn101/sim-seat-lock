# Research and long-term development

Living diary of how SimSeatLock actually attached to each title.
Architecture and install steps stay in [`ARCHITECTURE.md`](ARCHITECTURE.md),
[`LAYER.md`](LAYER.md), [`AMS2.md`](AMS2.md), [`LMU.md`](LMU.md).
This file is the *why* and the failed turns — the part that disappears
across dozens of chats if it is not written down.

Last updated: 2026-09-26 (Wellington, CO). Pose v0.2.4. AMS2 compensated.
ACC compensated on `IVRCompositor_022`. ACE / LMU still on the implicit
OpenXR layer.

---

## What this project is

One measured chassis pose (`T_rig` from WitMotion COM8) published at
≥250 Hz in `Local\\SimSeatLock.Rig.v1`. One formula:

```
T_view = T_cor × inv(T_rig) × inv(T_cor) × T_hmd
```

`T_cor` is IMU-to-eye from `config/geometry.json`. The headset stays in
the bucket; the 6DOF platform (Qubic / SimTools) is subtracted about the
center of rotation. Head motion relative to the seat is kept.

The *injector* is what changes. Pose never invents a game camera. It
only writes `T_rig` and reads back whatever the injector put in
`Local\\SimSeatLock.Game.v1`.

PSVR2 on PC is always SteamVR underneath. OpenComposite / OpenXR Toolkit
would add a hop. We did not take that hop for AMS2 or ACC.

---

## Title matrix (2026-09-26)

| Title | Engine | Process | VR API the game actually calls | Injector | Status |
|---|---|---|---|---|---|
| Assetto Corsa Evo | current Kunos / ACE | `AssettoCorsaEVO` | OpenXR | implicit layer `XR_APILAYER_NOVENDOR_sim_seat_lock` | working |
| Le Mans Ultimate | SMS / S397 | `Le Mans Ultimate` | OpenXR (offline `+VR`; EAC blocks the protected launcher) | same OpenXR layer | working when the layer is allowed to load |
| Automobilista 2 | Reiza Madness / SMS Phoenix | `AMS2AVX` | OpenVR C API, `IVRCompositor_029` | `openvr_api.dll` proxy in `Automobilista 2\\x64` | working 2026-09-25 |
| Assetto Corsa Competizione | UE4 (`AC2-Win64-Shipping`) | `AC2-Win64-Shipping` | OpenVR 1.5.17 loader, `IVRCompositor_022` / `IVRSystem_020` | thin 24-export proxy in `Engine\\Binaries\\ThirdParty\\OpenVR\\OpenVRv1_5_17\\Win64` | working 2026-09-26 |
| iRacing | — | `iRacingSim64DX11` | OpenVR (classified, not proven this week) | same idea as AMS2 | not proven |

Path is chosen from the running exe (`EngineWatch.InjectionPath`).
OpenVR-native today: names containing `AMS2` or `iRacing`.
OpenXR-native: `AssettoCorsa`, `assetto_corsa`, `Le Mans`, `LeMans`.
`AC2-Win64-Shipping` is OpenVR in practice; the classifier still says
`unknown` until that name is added. Detection still works if the process
is listed under Game Processes.

---

## OpenXR path (ACE, LMU)

Khronos implicit API layer. Official hook, unofficial feature.

- Manifest: `XR_APILAYER_NOVENDOR_sim_seat_lock.json`
- Install: `src/SimSeatLock.Layer/install-layer.cmd` writes the layer
  into the current-user OpenXR implicit list. No game-folder DLL.
- Negotiate on `xrCreateInstance` / `xrLocateViews`.
- Compensated views written back; Game.v1 heartbeat is the Layer lamp.

ACE loaded the layer and went green without a per-title shim. LMU needs
the unprotected exe + `+VR`; Easy Anti-Cheat on the store launcher will
not load a side-loaded layer. See [`LMU.md`](LMU.md) and the S397
allow-list draft.

Do not drop an `openvr_api.dll` proxy into ACE or LMU. Those titles are
not looking for that file as their VR entry.

---

## Automobilista 2 — OpenVR, 253 exports, Madness engine

AMS2 never creates an OpenXR instance. The OpenXR layer stays red
forever on a stock AMS2 install. That was the original 2026-09-25 report:
IMU, SteamVR, Engine green; Layer waiting.

### What AMS2 actually talks to

`D:\\Games\\Automobilista 2\\x64\\openvr_api.dll` (243 200 bytes,
stock dated 2026-06-15). Python PE dump: **253 named exports**, including
`VR_Init`, `VR_Shutdown`, `VR_GetGenericInterface`, `VRCompositor`,
`VRSystem`, the flat `VR_IVRCompositor_WaitGetPoses` family, and a pile
of `UnityHooks_*` leftovers from an old engine era.

`VR_Init` returns an `IVRSystem*`. Runtime compositor requested as
`IVRCompositor_029`.

### What failed, in order

1. **Hoping OpenXR would attach.** It cannot. No instance, no layer.
2. **Replacing `openvr_api.dll` with a short export list.** Loader / IAT
   expected the modern flat C API. Missing `VR_Init` → splash death.
3. **`.def` `NAME = openvr_api_orig.NAME` for all 253.** MSVC LNK2001 /
   unresolved on that syntax. Switched to
   `#pragma comment(linker, "/export:NAME=openvr_api_orig.NAME")` in
   `forwards.cpp`.
4. **PE-forwarding `VR_Init` itself.** The forward bound, but we could
   not log or wrap it. Explicit stub + `EnsureOrig()` + `GetProcAddress`
   on `openvr_api_orig.dll`.
5. **Vtable hook on every `VRCompositor()` factory pointer, at init.**
   Two different objects (`GetGenericInterface IVRCompositor_029` vs
   `VRCompositor()`). Hooking the factory during splash made AMS2 exit
   cleanly (~8 s), not BugSplat. Skip-hook probe: game stayed up, Game L
   stayed identity.
6. **Hooking only `WaitGetPoses` (vtable slot 2).** AMS2 calls it **three
   times** to start the compositor, then goes quiet. Game L flickered
   identity ↔ one-off pose. Frame loop is `GetLastPoses` (slot 3).
7. **Identity `WriteAlive` on every `VRCompositor()`.** 100 Hz zeros
   beat the three real samples. Layer lamp lied (green + `0,0,0`).

### What worked

- Proxy as `openvr_api.dll`, stock saved as `openvr_api.stock.dll`, orig
  for forwards as `openvr_api_orig.dll`.
- Stub `VR_Init` / `VR_GetGenericInterface` / presence.
- PE-forward everything else to orig.
- Save the `IVRCompositor_029` pointer from GIPA.
- Do **not** hook the `VRCompositor()` factory object.
- After splash: vtable slot 2 (`WaitGetPoses`) **and** slot 3
  (`GetLastPoses`). Patch `TrackedDevicePose[0]` (HMD) with the same
  `ApplyCompensateRigid` as the OpenXR layer.
- Game R stays identity. OpenVR is one HMD pose. Ignore R.
- Proof: Brands Hatch bank, Arm off = world rides the chassis, Arm on =
  horizon planted.

Log: `D:\\Games\\Automobilista 2\\x64\\simseatlock-openvr.log`.

`math.cpp` originally failed the OpenVR project with missing `cosf` /
`sinf` — include `<cmath>` and keep `pose_math.h` (renamed from
`math.h` so it does not collide with the CRT).

---

## Assetto Corsa Competizione — UE4, OpenVR 1.5.17, 24 exports

ACC does **not** load `openvr_api.dll` from `AC2\\Binaries\\Win64`.
UE4 keeps the Valve loader here:

```
...\Assetto Corsa Competizione\Engine\Binaries\ThirdParty\OpenVR\OpenVRv1_5_17\Win64\openvr_api.dll
```

Stock size **597 792** bytes (2026-07-23). Python PE dump: **24 exports
only**. No `VR_Init`. No `VRCompositor`. No `WaitGetPoses` flat API.

```
LiquidVR
VRCompositorSystemInternal
VRControlPanel
VRDashboardManager
VROculusDirect
VROverlayView
VRPaths
VRRenderModelsInternal
VRSceneGraph
VRTrackedCameraInternal
VRVirtualDisplay
VR_GetGenericInterface
VR_GetInitToken
VR_GetRuntimePath
VR_GetStringForHmdError
VR_GetVRInitErrorAsEnglishDescription
VR_GetVRInitErrorAsSymbol
VR_InitInternal
VR_InitInternal2
VR_IsHmdPresent
VR_IsInterfaceVersionValid
VR_IsRuntimeInstalled
VR_RuntimePath
VR_ShutdownInternal
```

Dropping the AMS2 253-export proxy on that file: DllMain → orig binds
`init=0 gipa=1 wait=0 compositor=0` → `VR_IsHmdPresent → 1` → UE4
fatal. ACC calls `VR_InitInternal2`, which the AMS2 proxy never stubbed.

### What worked

Separate project: `SimSeatLock.OpenVR.ACC.vcxproj` →
`publish\\openvr-acc\\openvr_api.dll` (~151 KB).

- `acc-exports.def` + `acc-forwards.cpp`: only those 24 names.
- `proxy-acc.cpp` stubs `VR_InitInternal`, `VR_InitInternal2`,
  `VR_ShutdownInternal`, `VR_GetGenericInterface`, presence.
- Splash-only first (no vtable). UE4 lived. In-car. Layer green from
  identity heartbeat. Game L zeros — expected.
- GIPA log (2026-09-25 23:48):
  `IVRSystem_020`, **`IVRCompositor_022`**, `IVROverlay_019` then `_028`,
  `IVRChaperone_003`, `IVRExtendedDisplay_001`, `IVRInput_007`.
- Same slot-2 / slot-3 hook as AMS2, aimed at the `_022` pointer.
  Hook immediately on GIPA (ACC has no late `VRCompositor()` pump; a
  4-second delay would never fire).

Process name is `AC2-Win64-Shipping`. Task Manager’s “AC2” grouping is
not the name `EngineWatch` matches. Add `AC2-Win64-Shipping` to
`pose.json` Game Processes.

Install cannot use `install-ams2.cmd` as written: `Program Files (x86)`
breaks `cmd.exe` `if "%TARGET%"=="" (` because `(x86)` opens a block.
Copy from Git Bash (Administrator if the copy is denied):

```bash
SRC=/c/Users/Bohnster/sim-seat-lock/publish/openvr-acc/openvr_api.dll
DST="/c/Program Files (x86)/Steam/steamapps/common/Assetto Corsa Competizione/Engine/Binaries/ThirdParty/OpenVR/OpenVRv1_5_17/Win64"
cp "$DST/openvr_api.dll" "$DST/openvr_api.stock.dll"   # once
cp "$DST/openvr_api.stock.dll" "$DST/openvr_api_orig.dll"
cp "$SRC" "$DST/openvr_api.dll"
```

Restore: copy stock back over `openvr_api.dll`.

### ACC camera vs our math (2026-09-26 drive notes)

Whip / cookies / sudden platform hits: Arm off = thrown around the
cockpit, Arm on = planted. That is the injector.

Slow crawl on long straights and flowing bends is **not** a missed
vtable slot. Three other layers:

1. ACC cockpit/helmet motion and Lock to Horizon. Documents
   `Assetto Corsa Competizione\\Config\\cameraSettings.json` —
   `"generalMovement": 0`. In-car View Settings: horizon lock 0%, cockpit
   camera not helmet.
2. PSVR2 inside-out SLAM in a dark carbon cockpit (feature-poor).
3. Sustained platform attitude: IMU `T_rig` and SteamVR fusion both see
   the tilt; residuals show up over seconds. Yaw gain is already 0.

Do not tighten the hook to chase that crawl. It would fight ACC’s own
camera and undo the whip case.

---

## Pose UI (passthrough / grayscale)

PSVR2 pass-through is a muddy mono camera. Color-only lamps were
unreadable. v0.2.4 Live tab:

| Tile | On (black fill, white text) | Off (white fill, black text) |
|---|---|---|
| 0 ARM | `0 ARM  ON` | `0 ARM  OFF` |
| 1 IMU | `1 IMU  ON` | `1 IMU  OFF` |
| 2 VR | `2 VR  ON` | `2 VR  OFF` |
| 3 LYR | `3 LYR  ON` | `3 LYR  OFF` |
| 4 ENG | `4 ENG  ON` | `4 ENG  OFF` |

Luminance + a number, not hue. Rebuild with
`dotnet publish ... -o publish` and launch `start-pose.cmd`.
`install-ams2.cmd` does **not** rebuild Pose. Title bar version is
`Program.Version` in `src/SimSeatLock.Pose/Program.cs`.

`3 LYR` green + Game L at `0,0,0` means heartbeat only (identity
`WriteAlive`). Compensation is proven when Game L tracks Adjusted and
the horizon stays put with Arm on.

---

## Tooling that ate evenings

- **Git Bash vs cmd.** `cmd.exe //c build-openvr.cmd` from the *project*
  folder, not from `~/`. `src` is not a cmd command.
- **`cmd.exe //c dumpbin /exports`.** Git Bash ate `/exports` as a path.
  Call dumpbin by full MSVC path, or use the Python PE parser.
- **`Program Files (x86)`.** Never `if "%TARGET%"=="" (` after the path
  is assigned. Use `if "%~1"==""` or copy from Bash.
- **Admin.** ACC lives under Program Files. Git Bash must be elevated
  to replace `openvr_api.dll`.
- **SteamVR not restarted** between proxy iterations. Usually fine when
  the title is closed. After a BugSplat loop, exit SteamVR from the tray
  once. Not required every install.
- **“Generic Windows” / leak / nothing works until reboot.** Happened.
  Do not debug a proxy through that. Reboot, re-install stock, then the
  candidate DLL.
- **Do not restart Steam for every copy.** Game closed is enough to
  replace the DLL next to the exe / ThirdParty folder.
- **Two `openvr_api.dll` trees.** AMS2 `x64\\` and ACC `OpenVRv1_5_17\\Win64`
  are independent. Restoring one does not touch the other.
- **Steam verify / game update** overwrites the proxy. Keep
  `openvr_api.stock.dll` in-folder and re-copy.

---

## Files that matter

| Path | Role |
|---|---|
| `src/SimSeatLock.Pose/` | Desktop viz, WitMotion, SteamVR seated, SHM publisher |
| `src/SimSeatLock.Layer/` | OpenXR implicit layer (ACE, LMU) |
| `src/SimSeatLock.OpenVR/proxy.cpp` + `forwards.cpp` | AMS2 253-export proxy |
| `src/SimSeatLock.OpenVR/proxy-acc.cpp` + `acc-forwards.cpp` | ACC 24-export proxy |
| `src/SimSeatLock.Layer/math.cpp` | Shared `ApplyCompensateRigid` |
| `config/geometry.json` | IMU-to-eye |
| `config/pose.json` | Port, gains, game process list |
| `publish/openvr/openvr_api.dll` | AMS2 payload |
| `publish/openvr-acc/openvr_api.dll` | ACC payload |

---

## What we are not doing

- Not SimHub Motion, SRS IntelliComp, FlyPT Mover, or SimTools as a pose
  source. WitMotion is Source A. Virtual numeric is debug.
- Not BuzzteeBear OXRMC at the same time.
- Not OpenComposite for PSVR2 titles. SteamVR is already the runtime.
- Not merging the TV-canvas sibling
  (`psvr2-visual-motion-compensation`).

---

## Open

- Mark `AC2` / `AC2-Win64-Shipping` as OpenVR in `EngineWatch` so the
  Game pane says `openvr live` instead of `unknown live`.
- `install-acc.cmd` that does not choke on `(x86)`.
- iRacing still classified, not driven.
- v1 predictive `T_rig` from the motion-command stream + IMU residual
  ([`ARCHITECTURE.md`](ARCHITECTURE.md)).
- ACC long-duration crawl: treat as camera / SLAM, not a new hook.
