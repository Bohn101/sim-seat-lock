# Automobilista 2 (Reiza) — OpenVR path

AMS2 / `AMS2AVX.exe` talks SteamVR through OpenVR (`openvr_api.dll`).
It does not create an OpenXR instance. Pose picks the inject path from
the running exe name (`EngineWatch.InjectionPath`): AMS2 and iRacing
are `openvr`; ACE and LMU are `openxr`.

Same Witmotion `IPoseSource`. Same math:

```
T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
```

The OpenVR proxy writes the same `Local\SimSeatLock.Game.v1` map the
OpenXR layer writes, so the Pose Layer lamp is the inject heartbeat for
both paths.

## Install

Git Bash, AMS2 closed:

```bash
cd /c/Users/Bohnster/sim-seat-lock
git pull origin main
cmd.exe //c src/SimSeatLock.OpenVR/build-openvr.cmd
cmd.exe //c src/SimSeatLock.OpenVR/install-ams2.cmd
```

If Steam is not in a default library, pass the x64 folder:

```bash
cmd.exe //c src/SimSeatLock.OpenVR/install-ams2.cmd "D:\SteamLibrary\steamapps\common\Automobilista 2\x64"
```

That copies stock `openvr_api.dll` to `openvr_api.stock.dll` and drops
`publish\openvr\openvr_api.dll` in its place. SteamVR stays the runtime.
Do not set `XR_API_LAYER_PATH`. Do not put this DLL in ACE or LMU.

Restore stock:

```bash
cmd.exe //c src/SimSeatLock.OpenVR/restore-ams2.cmd
```

Steam verify / an AMS2 update will overwrite `openvr_api.dll`. Re-run
install. Leave `openvr_api.stock.dll` in the folder.

## Proof

1. Pose on, Arm off, Home (Z) at SimTools neutral.
2. Launch AMS2 in **SteamVR mode**.
3. Sit in the car.
4. `Automobilista 2\x64\simseatlock-openvr.log` must contain
   `DllMain PROCESS_ATTACH` for `AMS2AVX.exe`, `stock ...openvr_api.stock.dll`,
   and `hooked IVRCompositor_`.
5. Pose Layer lamp green. Game pane `openvr live`.
6. Arm only after Home. Disarm before Reset View / Home.

If the log says `no stock openvr_api next to proxy`, install did not
write `openvr_api.stock.dll` or the proxy is not the file AMS2 loaded.

## What v0.2.2 vs v0.2.3 changed

v0.2.2 only told you Layer-red was expected on stock SteamVR AMS2.
v0.2.3 is the actual OpenVR inject path.

## Do not

- Do not use OpenComposite for this. We wrap Valve OpenVR; we do not
  translate it to OpenXR.
- Do not replace `openvr_api.dll` inside ACE or LMU.
- Do not set `XR_API_LAYER_PATH` / `XR_ENABLE_API_LAYERS`.
- Do not invent a second pose source. Witmotion stays Source A.
