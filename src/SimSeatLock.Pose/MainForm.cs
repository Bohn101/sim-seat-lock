using SimSeatLock.Pose.Config;
using SimSeatLock.Pose.Publish;
using SimSeatLock.Pose.Tracking;
using SimSeatLock.Pose.Witmotion;

namespace SimSeatLock.Pose;

public sealed class MainForm : Form
{
    readonly PoseConfig _cfg;
    readonly GeometryConfig _geom;
    readonly CompositePoseSource _pose;
    readonly WitmotionSerialPoseSource _serial;
    readonly SteamVrPoseSource _steam;
    readonly GamePoseSource _game;
    readonly PoseSharedMemory _shm;
    readonly string _cfgPath;
    readonly string _geomPath;

    readonly CheckBox _preview;
    readonly Button _home;
    readonly Button _zeroVirtual;
    readonly TextBox _steamBox;
    readonly TextBox _gameBox;
    readonly TextBox _adjBox;
    readonly TextBox _deltaBox;
    readonly TextBox _rigBox;
    readonly Label _status;
    readonly NumericUpDown _roll, _pitch, _yaw, _surge, _sway, _heave;
    readonly System.Windows.Forms.Timer _ui;
    readonly Thread _pubThread;
    volatile bool _run = true;
    volatile bool _previewArmed;
    string _homeNote = "";

    TextBox _srcBox = null!;
    TextBox _portBox = null!;
    NumericUpDown _baudBox = null!;
    CheckBox _invR = null!, _invP = null!, _invY = null!, _swap = null!, _seated = null!;
    NumericUpDown _gRoll = null!, _gPitch = null!, _gYaw = null!;
    NumericUpDown _gSurge = null!, _gSway = null!, _gHeave = null!;
    NumericUpDown _eyeF = null!, _eyeR = null!, _eyeU = null!;
    TextBox _gamesBox = null!;

    public MainForm(
        PoseConfig cfg,
        GeometryConfig geom,
        CompositePoseSource pose,
        WitmotionSerialPoseSource serial,
        SteamVrPoseSource steam,
        GamePoseSource game,
        PoseSharedMemory shm,
        string cfgPath,
        string geomPath)
    {
        _cfg = cfg;
        _geom = geom;
        _pose = pose;
        _serial = serial;
        _steam = steam;
        _game = game;
        _shm = shm;
        _cfgPath = cfgPath;
        _geomPath = geomPath;
        _previewArmed = cfg.PreviewArmed;

        Text = $"SimSeatLock.Pose v{Program.Version}";
        StartPosition = FormStartPosition.Manual;
        Location = new Point(20, 20);
        ClientSize = new Size(1100, 760);
        MinimumSize = new Size(1000, 700);
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var live = new TabPage("Live");
        var config = new TabPage("Config");
        tabs.TabPages.Add(live);
        tabs.TabPages.Add(config);

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0)
        };

        Controls.Add(tabs);
        Controls.Add(_status);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(6, 4, 6, 4)
        };
        _preview = new CheckBox
        {
            AutoSize = true,
            Text = "Arm layer / Preview inv(T_rig)  -  off = identity (headset unchanged)",
            Checked = _previewArmed
        };
        _preview.CheckedChanged += (_, _) =>
        {
            _previewArmed = _preview.Checked;
            _cfg.PreviewArmed = _previewArmed;
        };
        _home = new Button { Text = "Home T_rig (Z)", AutoSize = true };
        _home.Click += (_, _) => TryHome();
        _zeroVirtual = new Button { Text = "Zero virtual", AutoSize = true };
        _zeroVirtual.Click += (_, _) =>
        {
            _pose.ResetVirtual();
            _roll.Value = 0; _pitch.Value = 0; _yaw.Value = 0;
            _surge.Value = 0; _sway.Value = 0; _heave.Value = 0;
        };
        top.Controls.Add(_preview);
        top.Controls.Add(_home);
        top.Controls.Add(_zeroVirtual);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(4)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _steamBox = MakeBox();
        _gameBox = MakeBox();
        _adjBox = MakeBox();
        _deltaBox = MakeBox();
        grid.Controls.Add(_steamBox, 0, 0);
        grid.Controls.Add(_gameBox, 1, 0);
        grid.Controls.Add(_adjBox, 0, 1);
        grid.Controls.Add(_deltaBox, 1, 1);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 248,
            ColumnCount = 2,
            Padding = new Padding(4)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _rigBox = MakeBox();
        bottom.Controls.Add(_rigBox, 0, 0);

        var virt = new GroupBox { Text = "Virtual numeric T_rig overlay", Dock = DockStyle.Fill };
        var virtGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(6)
        };
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _roll = AddNud(virtGrid, 0, 0, "roll deg", -180, 180, 0.05m, 2);
        _surge = AddNud(virtGrid, 2, 0, "surge m", -2, 2, 0.001m, 4);
        _pitch = AddNud(virtGrid, 0, 1, "pitch deg", -180, 180, 0.05m, 2);
        _sway = AddNud(virtGrid, 2, 1, "sway m", -2, 2, 0.001m, 4);
        _yaw = AddNud(virtGrid, 0, 2, "yaw deg", -180, 180, 0.05m, 2);
        _heave = AddNud(virtGrid, 2, 2, "heave m", -2, 2, 0.001m, 4);
        foreach (var n in new[] { _roll, _pitch, _yaw, _surge, _sway, _heave })
            n.ValueChanged += (_, _) => PushVirtual();
        virt.Controls.Add(virtGrid);
        bottom.Controls.Add(virt, 1, 0);

        live.Controls.Add(grid);
        live.Controls.Add(bottom);
        live.Controls.Add(top);

        BuildConfigTab(config);

        _ui = new System.Windows.Forms.Timer { Interval = 50 };
        _ui.Tick += (_, _) => RefreshLive();
        _ui.Start();

        _pubThread = new Thread(PublishLoop)
        {
            IsBackground = true,
            Name = "SimSeatLock.Rig.pub",
            Priority = ThreadPriority.AboveNormal
        };
        _pubThread.Start();
    }

    void BuildConfigTab(TabPage page)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 16,
            Padding = new Padding(10)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int r = 0;
        void LabelRow(string text)
        {
            root.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
        }

        LabelRow("source");
        _srcBox = new TextBox { Text = _cfg.Source, Dock = DockStyle.Fill };
        root.Controls.Add(_srcBox, 1, r++);

        LabelRow("witmotion.port");
        _portBox = new TextBox { Text = _cfg.Witmotion.Port, Dock = DockStyle.Fill };
        root.Controls.Add(_portBox, 1, r++);

        LabelRow("witmotion.baud");
        _baudBox = new NumericUpDown { Minimum = 9600, Maximum = 921600, Value = Math.Clamp(_cfg.Witmotion.Baud, 9600, 921600), Dock = DockStyle.Left, Width = 120 };
        root.Controls.Add(_baudBox, 1, r++);

        LabelRow("invert");
        var inv = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _invR = new CheckBox { Text = "roll", AutoSize = true, Checked = _cfg.Invert.Roll };
        _invP = new CheckBox { Text = "pitch", AutoSize = true, Checked = _cfg.Invert.Pitch };
        _invY = new CheckBox { Text = "yaw", AutoSize = true, Checked = _cfg.Invert.Yaw };
        inv.Controls.AddRange(new Control[] { _invR, _invP, _invY });
        root.Controls.Add(inv, 1, r++);

        LabelRow("swap_pitch_roll");
        _swap = new CheckBox { AutoSize = true, Checked = _cfg.SwapPitchRoll };
        root.Controls.Add(_swap, 1, r++);

        LabelRow("gain_rot_deg");
        var gr = new FlowLayoutPanel { AutoSize = true };
        _gRoll = TinyGain((decimal)_cfg.GainRotDeg.Roll);
        _gPitch = TinyGain((decimal)_cfg.GainRotDeg.Pitch);
        _gYaw = TinyGain((decimal)_cfg.GainRotDeg.Yaw);
        gr.Controls.AddRange(new Control[] { new Label { Text = "R", AutoSize = true }, _gRoll, new Label { Text = "P", AutoSize = true }, _gPitch, new Label { Text = "Y", AutoSize = true }, _gYaw });
        root.Controls.Add(gr, 1, r++);

        LabelRow("gain_trans_m");
        var gt = new FlowLayoutPanel { AutoSize = true };
        _gSurge = TinyGain((decimal)_cfg.GainTransM.Surge);
        _gSway = TinyGain((decimal)_cfg.GainTransM.Sway);
        _gHeave = TinyGain((decimal)_cfg.GainTransM.Heave);
        gt.Controls.AddRange(new Control[] { new Label { Text = "sg", AutoSize = true }, _gSurge, new Label { Text = "sw", AutoSize = true }, _gSway, new Label { Text = "hv", AutoSize = true }, _gHeave });
        root.Controls.Add(gt, 1, r++);

        LabelRow("steam_vr_seated");
        _seated = new CheckBox { AutoSize = true, Checked = _cfg.SteamVrSeated };
        root.Controls.Add(_seated, 1, r++);

        LabelRow("game_processes");
        _gamesBox = new TextBox { Text = string.Join(Environment.NewLine, _cfg.GameProcesses), Multiline = true, Height = 80, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f) };
        root.Controls.Add(_gamesBox, 1, r++);

        LabelRow("eye_forward_m (+front)");
        _eyeF = new NumericUpDown { DecimalPlaces = 3, Increment = 0.01m, Minimum = -2, Maximum = 2, Value = (decimal)_geom.EyeForwardM, Width = 120 };
        root.Controls.Add(_eyeF, 1, r++);

        LabelRow("eye_right_m (+right)");
        _eyeR = new NumericUpDown { DecimalPlaces = 3, Increment = 0.01m, Minimum = -2, Maximum = 2, Value = (decimal)_geom.EyeRightM, Width = 120 };
        root.Controls.Add(_eyeR, 1, r++);

        LabelRow("eye_up_m (+up)");
        _eyeU = new NumericUpDown { DecimalPlaces = 3, Increment = 0.01m, Minimum = 0.2m, Maximum = 2.5m, Value = (decimal)(_geom.EyeUpM <= 0 ? 1.10 : _geom.EyeUpM), Width = 120 };
        root.Controls.Add(_eyeU, 1, r++);

        var savePose = new Button { Text = "Save pose.json", AutoSize = true };
        savePose.Click += (_, _) =>
        {
            PullPoseFromUi();
            _cfg.Save(_cfgPath);
            _pose.Gains = _cfg.ToGains();
            _pose.Mode = _cfg.Source;
            _homeNote = $"saved {_cfgPath}";
        };
        var saveGeom = new Button { Text = "Save geometry.json", AutoSize = true };
        saveGeom.Click += (_, _) =>
        {
            PullGeomFromUi();
            PoseConfig.SaveGeometry(_geomPath, _geom);
            _homeNote = $"saved {_geomPath}";
        };
        var row = new FlowLayoutPanel { AutoSize = true };
        row.Controls.Add(savePose);
        row.Controls.Add(saveGeom);
        root.Controls.Add(new Label { Text = "", AutoSize = true }, 0, r);
        root.Controls.Add(row, 1, r);

        void ApplyLive(object? _, EventArgs e)
        {
            PullPoseFromUi();
            _pose.Gains = _cfg.ToGains();
        }
        _invR.CheckedChanged += ApplyLive;
        _invP.CheckedChanged += ApplyLive;
        _invY.CheckedChanged += ApplyLive;
        _swap.CheckedChanged += ApplyLive;

        page.Controls.Add(root);
    }

    static NumericUpDown TinyGain(decimal v) =>
        new()
        {
            DecimalPlaces = 2,
            Increment = 0.05m,
            Minimum = -4,
            Maximum = 4,
            Value = Math.Clamp(v, -4, 4),
            Width = 70
        };

    void PullPoseFromUi()
    {
        _cfg.Source = string.IsNullOrWhiteSpace(_srcBox.Text) ? "witmotion" : _srcBox.Text.Trim();
        _cfg.Witmotion.Port = _portBox.Text.Trim();
        _cfg.Witmotion.Baud = (int)_baudBox.Value;
        _cfg.Invert.Roll = _invR.Checked;
        _cfg.Invert.Pitch = _invP.Checked;
        _cfg.Invert.Yaw = _invY.Checked;
        _cfg.SwapPitchRoll = _swap.Checked;
        _cfg.GainRotDeg.Roll = (double)_gRoll.Value;
        _cfg.GainRotDeg.Pitch = (double)_gPitch.Value;
        _cfg.GainRotDeg.Yaw = (double)_gYaw.Value;
        _cfg.GainTransM.Surge = (double)_gSurge.Value;
        _cfg.GainTransM.Sway = (double)_gSway.Value;
        _cfg.GainTransM.Heave = (double)_gHeave.Value;
        _cfg.SteamVrSeated = _seated.Checked;
        _cfg.PreviewArmed = _previewArmed;
        _cfg.GameProcesses = _gamesBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    void PullGeomFromUi()
    {
        _geom.EyeForwardM = (double)_eyeF.Value;
        _geom.EyeRightM = (double)_eyeR.Value;
        _geom.EyeUpM = (double)_eyeU.Value;
        _geom.Normalize();
    }

    static TextBox MakeBox() => new()
    {
        Multiline = true,
        ReadOnly = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9f),
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        TabStop = false
    };

    static NumericUpDown AddNud(TableLayoutPanel grid, int col, int row, string label, decimal min, decimal max, decimal inc, int places)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, col, row);
        var n = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = inc,
            DecimalPlaces = places,
            Dock = DockStyle.Fill,
            ThousandsSeparator = false
        };
        grid.Controls.Add(n, col + 1, row);
        return n;
    }

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
            _homeNote = "Home refused - disarm preview first (platform at SimTools neutral)";
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

        string engine = _game.Watch.Detected ? $"game engine up ({_game.Watch.Status})" : "game no engine";
        string layer = _game.LayerLive ? "layer live" : "waiting for layer";
        _status.Text =
            $"rig {(_serial.IsLive ? "live " + _cfg.Witmotion.Port : _serial.Status)}   " +
            $"steam {(_steam.IsLive ? "seated" : _steam.Status)}   " +
            $"{engine}, {layer}   " +
            $"IMU {_serial.AngleHz:0} Hz  dropped {_serial.DroppedChecksum}" +
            (string.IsNullOrEmpty(_homeNote) ? "" : "   " + _homeNote);
    }

    string FormatSteam(in RigidPose p)
    {
        var e = p.ToEulerSample();
        return
            "SteamVR (anytime SteamVR is on)\r\n" +
            $"SteamVR  {LiveHold(p.Live)}  {(_steam.IsLive ? _steam.Status : "off")}  space={p.Space}\r\n" +
            FormatRigid(p, e);
    }

    string FormatGame(in RigidPose l, in RigidPose r)
    {
        var el = l.ToEulerSample();
        var er = r.ToEulerSample();
        return
            "Game OpenXR (ACE / AMS2 / any title + layer)\r\n" +
            $"Game L  {LiveHold(l.Live)}  {_game.Status}\r\n" +
            $"space={l.Space}\r\n" +
            FormatRigid(l, el) +
            "\r\n" +
            $"Game R  {LiveHold(r.Live)}  space={r.Space}\r\n" +
            FormatRigid(r, er);
    }

    string FormatAdjusted(in RigidPose p)
    {
        var e = p.ToEulerSample();
        string mode = _previewArmed
            ? "LIVE  T_cor*inv(T_rig)*inv(T_cor)*T_hmd"
            : "LIVE  passthrough (preview OFF = headset)";
        return
            "Adjusted T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd\r\n" +
            $"Adjusted ({mode})  space={p.Space}\r\n" +
            FormatRigid(p, e);
    }

    string FormatDelta(in RigidPose p, in RigidPose steam, in RigidPose adj)
    {
        var e = p.ToEulerSample();
        string hint = _previewArmed
            ? "armed: headset minus adjusted (what inv(T_rig) about CoR removed)"
            : "disarmed: identity, should stay ~0";
        return
            "Delta  headset vs adjusted\r\n" +
            $"Delta  {LiveHold(p.Live)}  {hint}\r\n" +
            FormatRigid(p, e) +
            "\r\n" +
            $"drag m  dx={adj.Px - steam.Px,8:0.0000}  dy={adj.Py - steam.Py,8:0.0000}  dz={adj.Pz - steam.Pz,8:0.0000}";
    }

    string FormatRig(in PoseSample raw, in PoseSample home, in PoseSample rig)
    {
        PoseMath.EulerDegToQuat(rig.RollDeg, rig.PitchDeg, rig.YawDeg, out var qx, out var qy, out var qz, out var qw);
        PoseMath.QuatConjugate(qx, qy, qz, qw, out var iqx, out var iqy, out var iqz, out var iqw);
        double ox = _geom.ImuToEyeM.X, oy = _geom.ImuToEyeM.Y, oz = _geom.ImuToEyeM.Z;
        PoseMath.Rotate(iqx, iqy, iqz, iqw, ox, oy, oz, out var rx, out var ry, out var rz);
        double dx = rx - ox, dy = ry - oy, dz = rz - oz;
        return
            "T_rig (Witmotion and/or virtual numeric)\r\n" +
            $"source  {_serial.Name}  mode={_pose.Mode}\r\n" +
            $"raw   R={raw.RollDeg,7:0.00} P={raw.PitchDeg,7:0.00} Y={raw.YawDeg,7:0.00}  " +
            $"sg={raw.SurgeM:0.000} sw={raw.SwayM:0.000} hv={raw.HeaveM:0.000}\r\n" +
            $"valid={raw.Valid}  home R={home.RollDeg,7:0.00} P={home.PitchDeg,7:0.00} Y={home.YawDeg,7:0.00}\r\n" +
            $"T_rig R={rig.RollDeg,7:0.00} P={rig.PitchDeg,7:0.00} Y={rig.YawDeg,7:0.00}  " +
            $"sg={rig.SurgeM:0.000} sw={rig.SwayM:0.000} hv={rig.HeaveM:0.000}\r\n" +
            $"T_rig quat      x={qx,8:0.0000}  y={qy,8:0.0000}  z={qz,8:0.0000}  w={qw,8:0.0000}\r\n" +
            $"inv(T_rig) quat x={iqx,8:0.0000}  y={iqy,8:0.0000}  z={iqz,8:0.0000}  w={iqw,8:0.0000}\r\n" +
            $"eye offset m    x={ox,7:0.000}  y={oy,7:0.000}  z={oz,7:0.000}  (right, up, -forward)\r\n" +
            $"drag at eye m   dx={dx,8:0.0000}  dy={dy,8:0.0000}  dz={dz,8:0.0000}  (leverage about CoR)\r\n" +
            $"valid={rig.Valid}  wit {_serial.Status}  live={_serial.IsLive}";
    }

    static string FormatRigid(in RigidPose p, in PoseSample e) =>
        $"pos m    x={p.Px,8:0.0000}  y={p.Py,8:0.0000}  z={p.Pz,8:0.0000}\r\n" +
        $"quat     x={p.Qx,8:0.0000}  y={p.Qy,8:0.0000}  z={p.Qz,8:0.0000}  w={p.Qw,8:0.0000}\r\n" +
        $"euler deg roll={e.RollDeg,7:0.00}  pitch={e.PitchDeg,7:0.00}  yaw={e.YawDeg,7:0.00}\r\n" +
        $"valid={p.Valid}";

    static string LiveHold(bool live) => live ? "LIVE" : "HOLD";

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
            catch
            {
            }

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
