using System.Diagnostics;
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
    readonly StatusStrip _strip;
    readonly ToolStripStatusLabel _status;
    readonly Label _lampImu, _lampSteam, _lampLayer, _lampEngine;
    readonly NumericUpDown _roll, _pitch, _yaw, _surge, _sway, _heave;
    readonly System.Windows.Forms.Timer _ui;
    readonly Thread _pubThread;
    volatile bool _run = true;
    volatile bool _previewArmed;
    string _homeNote = "";

    ComboBox _srcBox = null!;
    ComboBox _portBox = null!;
    NumericUpDown _baudBox = null!;
    CheckBox _invR = null!, _invP = null!, _invY = null!, _swap = null!, _seated = null!;
    NumericUpDown _gRoll = null!, _gPitch = null!, _gYaw = null!;
    NumericUpDown _gSurge = null!, _gSway = null!, _gHeave = null!;
    NumericUpDown _eyeF = null!, _eyeR = null!, _eyeU = null!;
    TextBox _gamesBox = null!;
    Label _openXrEye = null!;
    GroupBox _serialGroup = null!;

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
        ClientSize = new Size(1180, 840);
        MinimumSize = new Size(1040, 740);
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(0, 0, 0, 2);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var live = new TabPage("Live");
        var config = new TabPage("Config");
        var help = new TabPage("Help");
        tabs.TabPages.Add(live);
        tabs.TabPages.Add(config);
        tabs.TabPages.Add(help);

        _strip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false,
            AutoSize = false,
            Height = 44,
            Padding = new Padding(8, 6, 20, 12)
        };
        _status = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoToolTip = true
        };
        _strip.Items.Add(_status);

        Controls.Add(tabs);
        Controls.Add(_strip);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(6, 4, 6, 2)
        };
        _preview = new CheckBox
        {
            AutoSize = true,
            Text = "Arm Layer / Preview inv(T_rig) — Off = identity",
            Checked = _previewArmed
        };
        _preview.CheckedChanged += (_, _) =>
        {
            _previewArmed = _preview.Checked;
            _cfg.PreviewArmed = _previewArmed;
        };
        _home = new Button { Text = "Home IMU (Z)", AutoSize = true };
        _home.Click += (_, _) => TryHome();
        var homeTip = new ToolTip();
        homeTip.SetToolTip(_home, "Zero the IMU offset at SimTools neutral. Not actuator home.");
        _zeroVirtual = new Button { Text = "Zero Virtual", AutoSize = true };
        _zeroVirtual.Click += (_, _) =>
        {
            _pose.ResetVirtual();
            _roll.Value = 0; _pitch.Value = 0; _yaw.Value = 0;
            _surge.Value = 0; _sway.Value = 0; _heave.Value = 0;
        };
        _lampImu = MakeLamp("IMU");
        _lampSteam = MakeLamp("SteamVR");
        _lampLayer = MakeLamp("Layer");
        _lampEngine = MakeLamp("Engine");
        top.Controls.Add(_preview);
        top.Controls.Add(_home);
        top.Controls.Add(_zeroVirtual);
        top.Controls.Add(_lampImu);
        top.Controls.Add(_lampSteam);
        top.Controls.Add(_lampLayer);
        top.Controls.Add(_lampEngine);

        _steamBox = MakeBox();
        _gameBox = MakeBox();
        _adjBox = MakeBox();
        _deltaBox = MakeBox();
        _rigBox = MakeBox();

        var splitLeft = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8 };
        splitLeft.Panel1.Controls.Add(_steamBox);
        splitLeft.Panel2.Controls.Add(_adjBox);

        var splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8 };
        splitRight.Panel1.Controls.Add(_gameBox);
        splitRight.Panel2.Controls.Add(_deltaBox);

        var splitMid = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 8 };
        splitMid.Panel1.Controls.Add(splitLeft);
        splitMid.Panel2.Controls.Add(splitRight);

        var virt = new GroupBox { Text = "Virtual Numeric T_rig Overlay", Dock = DockStyle.Fill };
        var virtGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(8)
        };
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        virtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        virtGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        virtGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        virtGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        virtGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _roll = AddNud(virtGrid, 0, 0, "Roll deg", -180, 180, 0.05m, 2);
        _surge = AddNud(virtGrid, 2, 0, "Surge m", -2, 2, 0.001m, 4);
        _pitch = AddNud(virtGrid, 0, 1, "Pitch deg", -180, 180, 0.05m, 2);
        _sway = AddNud(virtGrid, 2, 1, "Sway m", -2, 2, 0.001m, 4);
        _yaw = AddNud(virtGrid, 0, 2, "Yaw deg", -180, 180, 0.05m, 2);
        _heave = AddNud(virtGrid, 2, 2, "Heave m", -2, 2, 0.001m, 4);
        foreach (var n in new[] { _roll, _pitch, _yaw, _surge, _sway, _heave })
            n.ValueChanged += (_, _) => PushVirtual();
        virt.Controls.Add(virtGrid);

        var splitBot = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 8 };
        splitBot.Panel1.Controls.Add(_rigBox);
        splitBot.Panel2.Controls.Add(virt);

        var splitOuter = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8 };
        splitOuter.Panel1.Controls.Add(splitMid);
        splitOuter.Panel2.Controls.Add(splitBot);

        live.Controls.Add(splitOuter);
        live.Controls.Add(top);

        Shown += (_, _) =>
        {
            try
            {
                splitOuter.SplitterDistance = Math.Max(280, splitOuter.Height - 280);
                splitMid.SplitterDistance = Math.Max(200, splitMid.Width / 2);
                splitLeft.SplitterDistance = Math.Max(120, splitLeft.Height / 2);
                splitRight.SplitterDistance = Math.Max(120, splitRight.Height / 2);
                splitBot.SplitterDistance = Math.Max(200, (int)(splitBot.Width * 0.62));
            }
            catch { }
        };

        BuildConfigTab(config);
        BuildHelpTab(help);

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

    static Label MakeLamp(string name) => new()
    {
        AutoSize = true,
        Text = "\u25cf " + name,
        Padding = new Padding(10, 6, 4, 0),
        ForeColor = Color.Gray
    };

    static void SetLamp(Label lamp, bool on, string extra)
    {
        lamp.ForeColor = on ? Color.ForestGreen : Color.Firebrick;
        lamp.Text = "\u25cf " + extra;
    }

    void BuildHelpTab(TabPage page)
    {
        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9.5f),
            Text = HelpText.Body.Replace("\n", "\r\n"),
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window
        };
        page.Padding = new Padding(8);
        page.Controls.Add(box);
    }

    void BuildConfigTab(TabPage page)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 16,
            Padding = new Padding(10),
            AutoScroll = true,
            AutoSize = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 16; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        int r = 0;
        void LabelRow(string text)
        {
            root.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 4) }, 0, r);
        }

        LabelRow("Source Mode");
        _srcBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
            Anchor = AnchorStyles.Left
        };
        _srcBox.Items.AddRange(["WitMotion", "Virtual Numeric"]);
        var src = (_cfg.Source ?? "witmotion").Trim().ToLowerInvariant();
        _srcBox.SelectedItem = src == "virtual" ? "Virtual Numeric" : "WitMotion";
        _srcBox.SelectedIndexChanged += (_, _) => ApplySourceMode(restartSerial: true);
        root.Controls.Add(_srcBox, 1, r++);

        LabelRow("Serial");
        _serialGroup = new GroupBox { Text = "Used when Source Mode is WitMotion", AutoSize = true, Dock = DockStyle.Top };
        var serialGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 2, Padding = new Padding(8), Dock = DockStyle.Fill };
        serialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        serialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        serialGrid.Controls.Add(new Label { Text = "Port", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _portBox = new ComboBox { Width = 160 };
        RefreshPorts();
        if (!string.IsNullOrWhiteSpace(_cfg.Witmotion.Port))
        {
            if (!_portBox.Items.Contains(_cfg.Witmotion.Port))
                _portBox.Items.Insert(0, _cfg.Witmotion.Port);
            _portBox.Text = _cfg.Witmotion.Port;
        }
        var refreshPorts = new Button { Text = "Refresh", AutoSize = true };
        refreshPorts.Click += (_, _) => RefreshPorts();
        var applyPort = new Button { Text = "Reconnect", AutoSize = true };
        applyPort.Click += (_, _) => ApplySourceMode(restartSerial: true);
        var portRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        portRow.Controls.Add(_portBox);
        portRow.Controls.Add(refreshPorts);
        portRow.Controls.Add(applyPort);
        serialGrid.Controls.Add(portRow, 1, 0);
        serialGrid.Controls.Add(new Label { Text = "Baud", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _baudBox = new NumericUpDown { Minimum = 9600, Maximum = 921600, Value = Math.Clamp(_cfg.Witmotion.Baud, 9600, 921600), Width = 120 };
        serialGrid.Controls.Add(_baudBox, 1, 1);
        _serialGroup.Controls.Add(serialGrid);
        root.Controls.Add(_serialGroup, 1, r++);
        UpdateSerialGroupEnabled();

        LabelRow("Invert");
        var inv = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _invR = new CheckBox { Text = "Roll", AutoSize = true, Checked = _cfg.Invert.Roll };
        _invP = new CheckBox { Text = "Pitch", AutoSize = true, Checked = _cfg.Invert.Pitch };
        _invY = new CheckBox { Text = "Yaw", AutoSize = true, Checked = _cfg.Invert.Yaw };
        inv.Controls.AddRange(new Control[] { _invR, _invP, _invY });
        root.Controls.Add(inv, 1, r++);

        LabelRow("Swap Pitch / Roll");
        _swap = new CheckBox { AutoSize = true, Checked = _cfg.SwapPitchRoll };
        root.Controls.Add(_swap, 1, r++);

        LabelRow("Gain Rotation (deg)");
        var gr = new FlowLayoutPanel { AutoSize = true };
        _gRoll = TinyGain((decimal)_cfg.GainRotDeg.Roll);
        _gPitch = TinyGain((decimal)_cfg.GainRotDeg.Pitch);
        _gYaw = TinyGain((decimal)_cfg.GainRotDeg.Yaw);
        gr.Controls.AddRange(new Control[] { new Label { Text = "R", AutoSize = true }, _gRoll, new Label { Text = "P", AutoSize = true }, _gPitch, new Label { Text = "Y", AutoSize = true }, _gYaw });
        root.Controls.Add(gr, 1, r++);

        LabelRow("Gain Translation (m)");
        var gt = new FlowLayoutPanel { AutoSize = true };
        _gSurge = TinyGain((decimal)_cfg.GainTransM.Surge);
        _gSway = TinyGain((decimal)_cfg.GainTransM.Sway);
        _gHeave = TinyGain((decimal)_cfg.GainTransM.Heave);
        gt.Controls.AddRange(new Control[] { new Label { Text = "Surge", AutoSize = true }, _gSurge, new Label { Text = "Sway", AutoSize = true }, _gSway, new Label { Text = "Heave", AutoSize = true }, _gHeave });
        root.Controls.Add(gt, 1, r++);

        LabelRow("SteamVR Seated");
        _seated = new CheckBox { AutoSize = true, Checked = _cfg.SteamVrSeated };
        root.Controls.Add(_seated, 1, r++);

        LabelRow("Game Processes");
        _gamesBox = new TextBox
        {
            Text = string.Join(Environment.NewLine, _cfg.GameProcesses),
            Multiline = true,
            Height = 96,
            Width = 360,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };
        var addRunning = new Button { Text = "Add Running…", AutoSize = true };
        addRunning.Click += (_, _) => AddRunningProcess();
        var gamesCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        gamesCol.Controls.Add(_gamesBox);
        gamesCol.Controls.Add(addRunning);
        root.Controls.Add(gamesCol, 1, r++);

        LabelRow("Eye Forward m (+front / −aft)");
        _eyeF = new NumericUpDown { DecimalPlaces = 3, Increment = 0.01m, Minimum = -2, Maximum = 2, Value = (decimal)_geom.EyeForwardM, Width = 120 };
        root.Controls.Add(_eyeF, 1, r++);

        LabelRow("Eye Right m (+right)");
        _eyeR = new NumericUpDown { DecimalPlaces = 3, Increment = 0.01m, Minimum = -2, Maximum = 2, Value = (decimal)_geom.EyeRightM, Width = 120 };
        root.Controls.Add(_eyeR, 1, r++);

        LabelRow("Eye Up m (+up)");
        _eyeU = new NumericUpDown { DecimalPlaces = 3, Increment = 0.01m, Minimum = 0.2m, Maximum = 2.5m, Value = (decimal)(_geom.EyeUpM <= 0 ? 1.10 : _geom.EyeUpM), Width = 120 };
        root.Controls.Add(_eyeU, 1, r++);

        LabelRow("OpenXR imu_to_eye");
        _openXrEye = new Label { AutoSize = true, Font = new Font("Consolas", 9f) };
        UpdateOpenXrEyeLabel();
        _eyeF.ValueChanged += (_, _) => UpdateOpenXrEyeLabel();
        _eyeR.ValueChanged += (_, _) => UpdateOpenXrEyeLabel();
        _eyeU.ValueChanged += (_, _) => UpdateOpenXrEyeLabel();
        root.Controls.Add(_openXrEye, 1, r++);

        var savePose = new Button { Text = "Save pose.json", AutoSize = true };
        savePose.Click += (_, _) =>
        {
            ApplySourceMode(restartSerial: true);
            _cfg.Save(_cfgPath);
            _game.Watch.SetNames(_cfg.GameProcesses);
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

    void UpdateSerialGroupEnabled()
    {
        if (_serialGroup != null)
            _serialGroup.Enabled = !string.Equals(SelectedSource(), "virtual", StringComparison.OrdinalIgnoreCase);
    }

    string SelectedSource() =>
        string.Equals(_srcBox.Text, "Virtual Numeric", StringComparison.OrdinalIgnoreCase) ? "virtual" : "witmotion";

    void ApplySourceMode(bool restartSerial)
    {
        PullPoseFromUi();
        _pose.Gains = _cfg.ToGains();
        _pose.Mode = _cfg.Source;
        UpdateSerialGroupEnabled();
        if (restartSerial && !string.Equals(_cfg.Source, "virtual", StringComparison.OrdinalIgnoreCase))
        {
            _serial.Start();
            _homeNote = $"WitMotion reconnect {_cfg.Witmotion.Port}";
        }
    }

    void RefreshPorts()
    {
        var keep = _portBox.Text;
        _portBox.Items.Clear();
        foreach (var p in WitmotionSerialPoseSource.ListPorts())
            _portBox.Items.Add(p);
        if (!string.IsNullOrWhiteSpace(keep))
        {
            if (!_portBox.Items.Contains(keep))
                _portBox.Items.Insert(0, keep);
            _portBox.Text = keep;
        }
    }

    void AddRunningProcess()
    {
        var names = Process.GetProcesses()
            .Select(p =>
            {
                try { return p.ProcessName; }
                catch { return ""; }
            })
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        using var pick = new Form
        {
            Text = "Add Running Process",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 480),
            MinimizeBox = false,
            MaximizeBox = false
        };
        var list = new ListBox { Dock = DockStyle.Fill, DataSource = names };
        var ok = new Button { Text = "Add", Dock = DockStyle.Bottom, Height = 32 };
        ok.Click += (_, _) =>
        {
            if (list.SelectedItem is string n && n.Length > 0)
            {
                var cur = _gamesBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (cur.Add(n))
                    _gamesBox.Text = string.Join(Environment.NewLine, cur.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            }
            pick.Close();
        };
        pick.Controls.Add(list);
        pick.Controls.Add(ok);
        pick.ShowDialog(this);
    }

    void UpdateOpenXrEyeLabel()
    {
        double f = (double)_eyeF.Value, ri = (double)_eyeR.Value, u = (double)_eyeU.Value;
        _openXrEye.Text = $"X={ri:0.000} (Right)  Y={u:0.000} (Up)  Z={-f:0.000} (−Forward)";
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
        _cfg.Source = SelectedSource();
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
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 9f),
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        TabStop = false
    };

    static NumericUpDown AddNud(TableLayoutPanel grid, int col, int row, string label, decimal min, decimal max, decimal inc, int places)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 6, 0) }, col, row);
        var n = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = inc,
            DecimalPlaces = places,
            Dock = DockStyle.Fill,
            ThousandsSeparator = false,
            Margin = new Padding(0, 4, 8, 4)
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
            : $"Source Mode: WitMotion " + port;
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
