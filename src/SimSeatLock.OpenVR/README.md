# SimSeatLock.OpenVR

Drop-in `openvr_api.dll` for titles that talk SteamVR through OpenVR
(AMS2 / `AMS2AVX.exe`). Same pose process. Same `T_view` math. Same
`Local\SimSeatLock.Game.v1` heartbeat Pose already watches.

Not OpenComposite. Not an OpenXR layer. SteamVR stays the runtime.
We load the game's original `openvr_api.stock.dll` and patch HMD poses
after Valve returns them.

ACE and LMU keep the implicit OpenXR layer. Do not put this DLL in
those folders.

## Build / install (Git Bash)

```bash
cd /c/Users/Bohnster/sim-seat-lock
cmd.exe //c src/SimSeatLock.OpenVR/build-openvr.cmd
cmd.exe //c src/SimSeatLock.OpenVR/install-ams2.cmd
```

Pass the x64 folder if Steam is not in a default library:

```bash
cmd.exe //c src/SimSeatLock.OpenVR/install-ams2.cmd "D:\\SteamLibrary\\steamapps\\common\\Automobilista 2\\x64"
```

Restore stock VR: `cmd.exe //c src/SimSeatLock.OpenVR/restore-ams2.cmd`

Proof: `Automobilista 2\\x64\\simseatlock-openvr.log` shows
`DllMain PROCESS_ATTACH` for AMS2AVX.exe and `hooked IVRCompositor_`.
Pose Layer lamp turns green. Arm only after Home (Z).
