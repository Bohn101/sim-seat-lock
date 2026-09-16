# Le Mans Ultimate (Studio 397)

Same implicit layer as ACE. Same math. Same Witmotion `IPoseSource`.
No second pose source. Do not load OXRMC / OpenXR Toolkit / OpenComposite.

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

## Git Bash only (do not use Git CMD)

Git CMD treats `cmd.exe //c` as a new empty shell — you only see the Windows
banner and no `OK: SimSeatLock registered`. Open **Git Bash** and paste:

```bash
cd /c/Users/Bohnster/sim-seat-lock
git pull origin main
cmd.exe //c src/SimSeatLock.Layer/install-layer.cmd
cmd.exe //c src/SimSeatLock.Layer/diagnose-layer.cmd
```

Allow the UAC prompt. You must see `Registered HKLM` and either
`OK: SimSeatLock registered in HKLM only.` or the Implicit HKLM query listing
the publish JSON. If you only see `Microsoft Windows [Version ...]` the script
did not run.

## Where the layer lives

One DLL, one JSON:

`C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.dll`
`C:\Users\Bohnster\sim-seat-lock\publish\layer\XR_APILAYER_NOVENDOR_sim_seat_lock.json`

`build\layer\Release\*.recipe` / `.iobj` / `.ipdb` are MSBuild junk, not a
second install. `src\SimSeatLock.Layer\*.json` is the source template.

Install writes **HKLM** Implicit, DWORD `0` = enabled, DWORD `1` = disabled:

```
regedit → HKEY_LOCAL_MACHINE\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit
```

Value **name** = the full JSON path above. Value **data** = `0x00000000` (On).

HKLM = whole PC. HKCU = this Windows user only. SteamVR lists **both** hives,
so the same JSON in both = two identical rows. New `install-layer.cmd` enables
HKLM and **deletes** the HKCU copy so SteamVR shows one line.

Disable for LMU online without deleting files:

```bash
cd /c/Users/Bohnster/sim-seat-lock
cmd.exe //c src/SimSeatLock.Layer/disable-layer.cmd
```

## Process names (EngineWatch)

`Le Mans Ultimate` and `LeMansUltimate`. Do not add `VR Server`.
Do not wipe COM8 or geometry −0.27 / 1.15.

## Launch

Official Steam option: **Launch Le Mans Ultimate in Steam VR Mode**.
Not **Play Le Mans Ultimate** (pancake).

**OpenXR Mode** is the ACE-shaped path (Khronos loader + implicit layers +
SteamVR runtime `steamxr_win64.json`).

Do not set `XR_API_LAYER_PATH` / `XR_ENABLE_API_LAYERS`.
Do not replace `openvr_api.dll`. Do not use OpenComposite.

## EAC vs DLC crash

Protected Steam launch + unofficial layer → `Unable to start the game in VR mode`
and often a **blank** game log. That is EAC refusing `LoadLibrary`.

`Le Mans Ultimate.exe +VR` skips EAC. If that path dies at ~4 s with BugSplat
and the trace says:

```
DLC file DLC Organiser.JSON not found in Core\Shared\DLC.mas
FATAL!!!
[ELS] Fatal Error 11
```

that is a **broken/incomplete install after the reinstall**, not the layer.
Steam → Le Mans Ultimate → Properties → Installed Files → Verify integrity.
Then launch once from Steam (pancake is fine) until the menu appears. Only
then retry `+VR`.

Official offline (no EAC, no online):
https://guide.lemansultimate.com/hc/en-gb/articles/14590657733903-How-do-I-run-the-game-without-Easy-Anti-Cheat-EAC

## SimTools / Toolkit

SimTools does not inject into the game process. The OpenXR layer does.
SimSeatLock does **not** need OpenXR Toolkit. Do not load OXRMC with us.

## Proof order

1. Git Bash install + diagnose. SteamVR shows **one** SimSeatLock row, On.
2. Pose on, Arm off, smoothing off, OXRMC off.
3. Game must reach the main menu from Steam at least once after verify.
4. Offline `Le Mans Ultimate.exe +VR`.
5. Success: `publish/layer/layer.log` has `DllMain PROCESS_ATTACH` +
   `Le Mans Ultimate.exe` + `negotiate OK`. Pose Game LIVE.
6. A/B lean. Online Steam VR Mode with layer On stays blocked until an
   Epic/S397 allow-list. Use `disable-layer.cmd` for online VR without lock.

## Recenter

Platform at SimTools neutral, Arm off. LMU **VR Centre head position** /
Reset View + SimSeatLock Home (Z).
