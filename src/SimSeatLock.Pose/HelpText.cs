namespace SimSeatLock.Pose;

static class HelpText
{
    public const string Body =
"""
SimSeatLock.Pose v0.2.1

Seat-lock keeps the VR eyepoint in the bucket while the platform moves.

    T_view = T_cor × inv(T_rig) × inv(T_cor) × T_hmd

T_hmd     Headset pose from SteamVR / OpenXR (before the layer).
T_rig     Platform pose from WitMotion after Home, invert, swap, gains.
T_cor     IMU-to-eye offset from geometry.json.
inv(...)  Inverse pose (undo that transform).
T_view    Pose the layer writes into xrLocateViews when Arm is on.

The OpenXR layer name requested for the LMU Easy Anti-Cheat allow-list is
exactly:

    XR_APILAYER_NOVENDOR_sim_seat_lock

That string is the Khronos api_layer.name. Do not rename it for branding.


LIVE — PANES

SteamVR
    Seated (or standing) headset pose whenever SteamVR is running.
    Independent of the title. HOLD = last sample after SteamVR exits.

Game OpenXR
    Left / right eye poses the layer writes to Local\\SimSeatLock.Game.v1
    from xrLocateViews. LIVE means the DLL loaded. "No engine" only means
    EngineWatch did not see a name from Game Processes. The layer can be
    LIVE without that list matching.

Adjusted T_view
    Desktop preview of the same product the layer applies when Arm is on.
    Arm off = identity = raw SteamVR. This pane does not move the headset;
    the layer does.

Delta (Headset vs Adjusted)
    SteamVR minus Adjusted. Arm off: should stay ~0. Arm on: the rotation
    and CoR drag that inv(T_rig) removed.

T_rig
    WitMotion and/or Virtual Numeric after Home. Raw = packet. Home =
    offset captured with Home IMU (Z). T_rig = raw − home, then invert /
    swap / gains. Eye offset is OpenXR metres (see Coordinates).


LIVE — CONTROLS

Arm Layer / Preview inv(T_rig)
    Sets Rig.v1 Armed. Off = identity in the headset. On = seat-lock.
    Home and title Reset View only with Arm off and the platform at
    SimTools neutral.

Home IMU (Z)
    Captures the current IMU reading as zero. Not SimTools actuator home
    (that stroke-calibrates the P5). Refuse if Arm is on.

Zero Virtual
    Sets the Virtual Numeric sliders to 0. Does not touch the IMU home.

Virtual Numeric T_rig overlay
    Debug source. Added to (or used instead of) WitMotion when Source Mode
    is Virtual. Roll / Pitch / Yaw in degrees. Surge / Sway / Heave in
    metres. Translation gains default to 0 on the IMU path.

Lamps
    Green = live. Red = down. IMU = COM port open and angle packets.
    SteamVR = runtime connected. Layer = Game.v1 heartbeat. Engine =
    a Game Processes name is running.


CONFIG

Source Mode
    WitMotion (serial IMU) or Virtual Numeric. Stored as witmotion|virtual.

WitMotion Port / Baud
    Default COM8 / 115200. Dropdown lists ports Windows can see. "Access
    to the path COM8 is denied" means another process already opened it
    (second Pose.exe, a terminal, or WitMotion's own tool). Close that,
    then restart Pose. SKU (WT901C-232) is not in the 0x55 angle packet;
    we do not invent it.

Invert / Swap Pitch-Roll
    After Home. Proven convention: lean right = +roll, nose up = +pitch.
    Do not "fix" with Swap unless new shots show T_rig itself is wrong.

Gain Rotation / Translation
    Multipliers. Translation defaults 0. Pitch and roll first.

SteamVR Seated
    Read seated vs standing tracking space.

Game Processes
    EngineWatch only — status text, not a pose source. Match is Windows
    Process.ProcessName (no .exe). Spaces are required when the name has
    them: "Le Mans Ultimate" is correct; "LeMansUltimate" is a fallback.
    AssettoCorsaEVO has no spaces. Add Running… appends a live ProcessName.

Eye Forward / Right / Up (metres, vehicle labels)
    +forward toward the screen, +right, +up. Eyes are 0.27 m aft and
    1.15 m above the IMU, so Forward = −0.27, Right = 0, Up = 1.15.


COORDINATES (do not swap these)

Two label sets, one stored pose.

Vehicle / Config (what you edit)
    Forward  −0.27 m   (aft of the IMU)
    Right     0.00 m
    Up        1.15 m   (IMU to headset lenses)

OpenXR imu_to_eye (what the layer uses, Y-up, −Z forward)
    X = Right     =  0.00
    Y = Up        =  1.15
    Z = −Forward  =  0.27

That is why T_rig prints x=0.000 y=1.150 z=0.270 (right, up, −forward).
It is not X-forward / Y-sway / Z-heave. Changing it to X=−0.27, Z=1.15
would break T_view. Surge / sway / heave stay vehicle words on the IMU
path; they map onto that OpenXR frame in math.cpp / PoseMath.cs.


EAC / LMU

Steam Play uses Easy Anti-Cheat and will refuse this DLL until S397/Epic
allow-list XR_APILAYER_NOVENDOR_sim_seat_lock. Offline test:
Le Mans Ultimate.exe +VR. Draft: docs/S397-allowlist.txt.
""";
}
