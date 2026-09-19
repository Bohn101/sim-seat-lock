using SimSeatLock.Pose.Config;
using SimSeatLock.Pose.Publish;
using SimSeatLock.Pose.Tracking;
using SimSeatLock.Pose.Witmotion;

namespace SimSeatLock.Pose;

public sealed partial class MainForm
{
    void PushVirtual()
    {
        _pose.Virtual.Set(new PoseSample
        {
            RollDeg = (double)_roll.Value,
            PitchDeg = (double)_pitch.Value,
            YawDeg = (double)_yaw.Value,
            SurgeM = (double)_surge.Value,
            SwayM = (double)_sway.Value,
            HeaveM = (double)_heave.Value,
            Valid = true,
            TimestampUtc = DateTime.UtcNow
        });
    }

    void TryHome()
    {
        if (_previewArmed)
        {
            _homeNote = "Home refused — disarm Arm first (platform at SimTools neutral)";
            return;
        }
        _pose.CaptureHome();
        _homeNote = "homed";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Z)
        {
            TryHome();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void RefreshLive()
    {
        var steam = _steam.Latest;
        var gameL = _game.Left;
        var gameR = _game.Right;
        var rig = _pose.Latest;
        var raw = _pose.RawLive;
        var home = _pose.Home;
        _geom.Normalize();
        var adj = PoseMath.Compensate(steam, rig, _geom.ImuToEyeM.X, _geom.ImuToEyeM.Y, _geom.ImuToEyeM.Z, _previewArmed);
        if (!_previewArmed && steam.Valid)
            adj = steam with { Space = steam.Space };
        var delta = PoseMath.Delta(steam, adj);

        _steamBox.Text = FormatSteam(steam);
        _gameBox.Text = FormatGame(gameL, gameR);
        _adjBox.Text = FormatAdjusted(adj);
        _deltaBox.Text = FormatDelta(delta, steam, adj);
        _rigBox.Text = FormatRig(raw, home, rig);

        bool imu = _serial.IsLive;
        bool svr = _steam.IsLive;
        bool layer = _game.LayerLive;
        bool eng = _game.Watch.Detected;
        SetLamp(_lampImu, imu, imu ? $"IMU {_cfg.Witmotion.Port}" : "IMU");
        SetLamp(_lampSteam, svr, "SteamVR");
        SetLamp(_lampLayer, layer, "Layer");
        SetLamp(_lampEngine, eng, eng ? $"Engine {_game.Watch.Status}" : "Engine");

        string engine = eng ? $"Engine {_game.Watch.Status}" : "Engine none";
        string layerTxt = layer ? "Layer LIVE" : "Layer waiting";
        _status.Text =
            $"{SourceModeLabel(_pose.Mode, _cfg.Witmotion.Port)}   " +
            $"IMU {(_serial.IsLive ? "LIVE" : _serial.Status)}   " +
            $"SteamVR {(_steam.IsLive ? "LIVE seated" : _steam.Status)}   " +
            $"{engine}   {layerTxt}   " +
            $"{_serial.AngleHz:0} Hz  dropped {_serial.DroppedChecksum}" +
            (string.IsNullOrEmpty(_homeNote) ? "" : "   " + _homeNote);
    }

    static string SourceModeLabel(string mode, string port)
    {
        if (string.Equals(mode, "virtual", StringComparison.OrdinalIgnoreCase))
            return "Source Mode: Virtual Numeric";
        return string.IsNullOrWhiteSpace(port)
            ? "Source Mode: WitMotion"
            : "Source Mode: WitMotion " + port;
    }

    string FormatSteam(in RigidPose p)
    {
        var e = p.ToEulerSample();
        return
            "SteamVR\r\n" +
            $"SteamVR  {LiveHold(p.Live)}  {(_steam.IsLive ? _steam.Status : "Off")}  Space={p.Space}\r\n" +
            FormatRigid(p, e);
    }

    string FormatGame(in RigidPose l, in RigidPose r)
    {
        var el = l.ToEulerSample();
        var er = r.ToEulerSample();
        return
            "Game OpenXR\r\n" +
            $"Game L  {LiveHold(l.Live)}  {_game.Status}\r\n" +
            $"Space={l.Space}\r\n" +
            FormatRigid(l, el) +
            "\r\n" +
            $"Game R  {LiveHold(r.Live)}  Space={r.Space}\r\n" +
            FormatRigid(r, er);
    }

    string FormatAdjusted(in RigidPose p)
    {
        var e = p.ToEulerSample();
        string mode = _previewArmed
            ? "LIVE  T_cor × inv(T_rig) × inv(T_cor) × T_hmd"
            : "LIVE  passthrough (Arm Off = headset)";
        return
            "Adjusted T_view\r\n" +
            $"Adjusted ({mode})  Space={p.Space}\r\n" +
            FormatRigid(p, e);
    }

    string FormatDelta(in RigidPose p, in RigidPose steam, in RigidPose adj)
    {
        var e = p.ToEulerSample();
        string hint = _previewArmed
            ? "Armed: headset minus adjusted (what inv(T_rig) about CoR removed)"
            : "Disarmed: identity, should stay ~0";
        return
            "Delta  Headset vs Adjusted\r\n" +
            $"Delta  {LiveHold(p.Live)}  {hint}\r\n" +
            FormatRigid(p, e) +
            "\r\n" +
            $"Drag m  dX={adj.Px - steam.Px,8:0.0000}  dY={adj.Py - steam.Py,8:0.0000}  dZ={adj.Pz - steam.Pz,8:0.0000}";
    }

    string FormatRig(in PoseSample raw, in PoseSample home, in PoseSample rig)
    {
        PoseMath.EulerDegToQuat(rig.RollDeg, rig.PitchDeg, rig.YawDeg, out var qx, out var qy, out var qz, out var qw);
        PoseMath.QuatConjugate(qx, qy, qz, qw, out var iqx, out var iqy, out var iqz, out var iqw);
        double ox = _geom.ImuToEyeM.X, oy = _geom.ImuToEyeM.Y, oz = _geom.ImuToEyeM.Z;
        PoseMath.Rotate(iqx, iqy, iqz, iqw, ox, oy, oz, out var rx, out var ry, out var rz);
        double dx = rx - ox, dy = ry - oy, dz = rz - oz;
        return
            "T_rig\r\n" +
            $"{SourceModeLabel(_pose.Mode, _cfg.Witmotion.Port)}\r\n" +
            $"Raw    Roll={raw.RollDeg,7:0.00}  Pitch={raw.PitchDeg,7:0.00}  Yaw={raw.YawDeg,7:0.00}  " +
            $"Surge={raw.SurgeM:0.000}  Sway={raw.SwayM:0.000}  Heave={raw.HeaveM:0.000}\r\n" +
            $"Valid={Flag(raw.Valid)}  Home Roll={home.RollDeg,7:0.00}  Pitch={home.PitchDeg,7:0.00}  Yaw={home.YawDeg,7:0.00}\r\n" +
            $"T_rig  Roll={rig.RollDeg,7:0.00}  Pitch={rig.PitchDeg,7:0.00}  Yaw={rig.YawDeg,7:0.00}  " +
            $"Surge={rig.SurgeM:0.000}  Sway={rig.SwayM:0.000}  Heave={rig.HeaveM:0.000}\r\n" +
            $"T_rig Quat       X={qx,8:0.0000}  Y={qy,8:0.0000}  Z={qz,8:0.0000}  W={qw,8:0.0000}\r\n" +
            $"inv(T_rig) Quat  X={iqx,8:0.0000}  Y={iqy,8:0.0000}  Z={iqz,8:0.0000}  W={iqw,8:0.0000}\r\n" +
            $"Eye Offset m (OpenXR)  X={ox,7:0.000} Right  Y={oy,7:0.000} Up  Z={oz,7:0.000} −Forward\r\n" +
            $"Drag at Eye m          dX={dx,8:0.0000}  dY={dy,8:0.0000}  dZ={dz,8:0.0000}\r\n" +
            $"Valid={Flag(rig.Valid)}  WitMotion {_serial.Status}  Live={Flag(_serial.IsLive)}";
    }

    static string FormatRigid(in RigidPose p, in PoseSample e) =>
        $"Position m   X={p.Px,8:0.0000}  Y={p.Py,8:0.0000}  Z={p.Pz,8:0.0000}\r\n" +
        $"Quaternion   X={p.Qx,8:0.0000}  Y={p.Qy,8:0.0000}  Z={p.Qz,8:0.0000}  W={p.Qw,8:0.0000}\r\n" +
        $"Euler deg    Roll={e.RollDeg,7:0.00}  Pitch={e.PitchDeg,7:0.00}  Yaw={e.YawDeg,7:0.00}\r\n" +
        $"Valid={Flag(p.Valid)}";

    static string LiveHold(bool live) => live ? "LIVE" : "HOLD";
    static string Flag(bool v) => v ? "True" : "False";

    void PublishLoop()
    {
        var next = Environment.TickCount64;
        while (_run)
        {
            try
            {
                var rig = _pose.Latest;
                PoseMath.EulerDegToQuat(rig.RollDeg, rig.PitchDeg, rig.YawDeg, out var qx, out var qy, out var qz, out var qw);
                _geom.Normalize();
                uint flags = 0;
                if (_serial.IsLive || _pose.Mode == "virtual") flags |= (uint)RigFlags.RigLive;
                if (_steam.IsLive) flags |= (uint)RigFlags.SteamVrLive;
                if (_game.LayerLive) flags |= (uint)RigFlags.GameLive;
                if (_previewArmed) flags |= (uint)RigFlags.Armed;
                var block = new RigBlock
                {
                    Flags = flags,
                    FileTimeUtc = DateTime.UtcNow.ToFileTimeUtc(),
                    RigHz = (float)_serial.AngleHz,
                    RigDropped = (uint)Math.Min(uint.MaxValue, _serial.DroppedChecksum),
                    RollDeg = (float)rig.RollDeg,
                    PitchDeg = (float)rig.PitchDeg,
                    YawDeg = (float)rig.YawDeg,
                    SurgeM = (float)rig.SurgeM,
                    SwayM = (float)rig.SwayM,
                    HeaveM = (float)rig.HeaveM,
                    Qx = (float)qx, Qy = (float)qy, Qz = (float)qz, Qw = (float)qw,
                    EyeX = (float)_geom.ImuToEyeM.X,
                    EyeY = (float)_geom.ImuToEyeM.Y,
                    EyeZ = (float)_geom.ImuToEyeM.Z,
                    Armed = _previewArmed ? 1 : 0
                };
                _shm.Write(block);
            }
            catch { }

            next += 4;
            var sleep = next - Environment.TickCount64;
            if (sleep > 0)
                Thread.Sleep((int)Math.Min(sleep, 8));
            else
                next = Environment.TickCount64;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _run = false;
        _ui.Stop();
        base.OnFormClosed(e);
    }
}
