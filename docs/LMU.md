# Le Mans Ultimate (Studio 397)

Same implicit layer as ACE. Same math. Same Witmotion `IPoseSource`.
No second pose source. Do not load OXRMC / OpenXR Toolkit / OpenComposite.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

## Where the layer lives

Files (only copy that should exist):

`C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.dll`
`C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json`

Install writes **HKLM** Implicit, DWORD `0` = enabled:

`HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit`
value name = absolute path of that JSON.

Older `install-layer.cmd` also wrote **HKCU**. SteamVR then lists the **same**
layer twice ("2 ACTIVE"). The Khronos loader can negotiate the DLL twice.
SteamVR Off only writes the hive it can touch (usually HKCU), so the HKLM row
looks stuck On. That is not two installs and not GitHub desync.

Fix:

```bash
cd /c/Users/Bohnster/sim-seat-lock
cmd.exe //c src/SimSeatLock.Layer/install-layer.cmd
cmd.exe //c src/SimSeatLock.Layer/diagnose-layer.cmd
```

`install-layer.cmd` now enables HKLM and **deletes** the HKCU duplicate.
Disable for LMU online without uninstalling files:

```bash
cmd.exe //c src/SimSeatLock.Layer/disable-layer.cmd
```

## Process names (EngineWatch)

Task Manager on v1.4150 shows parent `Le Mans Ultimate` and child
`Le Mans Ultimate v1.4150`. Seed `game_processes` with `Le Mans Ultimate`
and `LeMansUltimate`. Do not add `VR Server`. Do not wipe COM8 or
geometry −0.27 / 1.15.

## Launch

Official Steam option: **Launch Le Mans Ultimate in Steam VR Mode**.
Not **Play Le Mans Ultimate** (pancake).

**OpenXR Mode** is the ACE-shaped path (Khronos loader + implicit layers +
SteamVR runtime `steamxr_win64.json`). If Steam VR Mode is OpenVR-only,
`layer.log` will have no `Le Mans Ultimate.exe` attach.

Do not set `XR_API_LAYER_PATH` / `XR_ENABLE_API_LAYERS`.
Do not replace `openvr_api.dll`. Do not use OpenComposite.

## EAC (why VR dies with the layer On)

LMU since 1.2 launches through Easy Anti-Cheat
(`start_protected_game.exe`). EAC inspects DLLs loaded into the game
process. An implicit OpenXR layer is `LoadLibrary`'d by the title when it
creates an OpenXR instance. Unsigned / not-allow-listed layers abort VR
before the game log is written:

`Unable to start the game in VR mode. Check the log for more info.`

Blank game log = crash before the engine logger starts. That matches EAC
refusing the layer, not a missing SimSeatLock bug.

OpenXR Toolkit was blocked the same way, then S397 + Epic **allow-listed
that specific product**. Our DLL is `XR_APILAYER_NOVENDOR_sim_seat_lock`.
Renaming the layer or the exe does not get an EAC exception. Code signing
helps SmartScreen, not EAC. There is no public self-serve whitelist; the
Toolkit path was a studio ticket to Epic.

v1.4 (Jul 2026) added native OpenXR + FOV scaling. v1.4.1.5 (15 Sep 2026)
updated the EAC module again. After a full LMU reinstall you must run the
EAC install bat under
`...\Le Mans Ultimate\EasyAntiCheat` or Steam will show
"Easy Anti-Cheat is not installed."

Official offline (no EAC, no online): launch `Le Mans Ultimate.exe` from
the game folder, or a shortcut with `+VR`.
https://guide.lemansultimate.com/hc/en-gb/articles/14590657733903-How-do-I-run-the-game-without-Easy-Anti-Cheat-EAC

## SimTools / Sim Commander vs the layer

SimTools and Sim Commander talk to the platform over their own protocol.
They are not injected into `Le Mans Ultimate.exe`. EAC does not care.
SimSeatLock.Layer **is** injected. That is why motion hardware works and
the OpenXR layer does not, on the same PC.

## OpenXR Toolkit / OXRMC

SimSeatLock does **not** need OpenXR Toolkit. Toolkit is a separate layer
(sharpen / NIS / foveated / crop FOV).

OXRMC (BuzzteeBear) also does **not** need Toolkit. OXRMC is itself an
OpenXR layer. Do not load OXRMC at the same time as SimSeatLock.

ReShade / replaced `openvr_api.dll` / OpenComposite are the same EAC class
as unofficial layers: blocked unless allow-listed, or only safe on the
offline `Le Mans Ultimate.exe` path.

## Proof order

1. `install-layer.cmd` so SteamVR shows **one** SimSeatLock row, On.
2. Pose on, Arm off, SteamVR smoothing off, OXRMC off.
3. Offline: shortcut `"...\Le Mans Ultimate\Le Mans Ultimate.exe" +VR`.
4. Success: `publish/layer/layer.log` has `DllMain PROCESS_ATTACH` +
   `exe=...\Le Mans Ultimate.exe` + `negotiate OK`. Pose Game LIVE.
5. A/B lean: eye mostly `dx`, horizon level.
6. Online Steam VR Mode with layer On will keep failing until S397/Epic
   allow-list this DLL. Use `disable-layer.cmd` for online pancake-free VR
   without compensation.

## Recenter

Platform at SimTools neutral, Arm off. LMU **VR Centre head position** /
Reset View + SimSeatLock Home (Z).
