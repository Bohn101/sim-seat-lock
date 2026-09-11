namespace SimSeatLock.Pose;

public sealed class CompositePoseSource : IPoseSource
{
    readonly IPoseSource _live;
    readonly VirtualPoseSource _virtual;
    readonly object _gate = new();
    PoseSample _home = PoseSample.Zero;
    Gains _gains;
    string _mode;

    public CompositePoseSource(IPoseSource live, VirtualPoseSource virtualSource, Gains gains, string mode)
    {
        _live = live;
        _virtual = virtualSource;
        _gains = gains;
        _mode = string.IsNullOrWhiteSpace(mode) ? "witmotion" : mode;
    }

    public string Name => Mode == "virtual" ? _virtual.Name : _live.Name;
    public bool IsLive => Mode == "virtual" || _live.IsLive;
    public string Status => Mode == "virtual" ? _virtual.Status : _live.Status;
    public VirtualPoseSource Virtual => _virtual;
    public IPoseSource Inner => _live;
    public string Mode { get { lock (_gate) return _mode; } set { lock (_gate) _mode = value; } }

    public Gains Gains
    {
        get { lock (_gate) return _gains; }
        set { lock (_gate) _gains = value; }
    }

    public PoseSample RawLive => Mode == "virtual" ? _virtual.Latest : _live.Latest;

    public PoseSample Home
    {
        get { lock (_gate) return _home; }
    }

    public PoseSample RemappedLive
    {
        get
        {
            var live = RawLive;
            Gains g;
            PoseSample home;
            lock (_gate) { g = _gains; home = _home; }
            var rel = live.Valid ? live.Subtract(home) : PoseSample.Zero;
            if (g.SwapPitchRoll)
                rel = rel with { RollDeg = rel.PitchDeg, PitchDeg = rel.RollDeg };
            return new PoseSample
            {
                RollDeg = g.ApplyRoll(rel.RollDeg),
                PitchDeg = g.ApplyPitch(rel.PitchDeg),
                YawDeg = g.ApplyYaw(rel.YawDeg),
                SurgeM = g.ApplySurge(rel.SurgeM),
                SwayM = g.ApplySway(rel.SwayM),
                HeaveM = g.ApplyHeave(rel.HeaveM),
                TimestampUtc = rel.TimestampUtc,
                Valid = rel.Valid
            };
        }
    }

    public PoseSample Latest
    {
        get
        {
            if (Mode == "virtual")
                return RemappedLive;
            var imu = RemappedLive;
            var overlay = ApplyInvertOnly(_virtual.Latest);
            var sum = imu.Add(overlay);
            return sum with { Valid = imu.Valid || overlay.Valid };
        }
    }

    PoseSample ApplyInvertOnly(in PoseSample kb)
    {
        Gains g;
        lock (_gate) g = _gains;
        return kb with
        {
            RollDeg = g.InvertRoll ? -kb.RollDeg : kb.RollDeg,
            PitchDeg = g.InvertPitch ? -kb.PitchDeg : kb.PitchDeg,
            YawDeg = g.InvertYaw ? -kb.YawDeg : kb.YawDeg,
            SurgeM = g.InvertSurge ? -kb.SurgeM : kb.SurgeM,
            SwayM = g.InvertSway ? -kb.SwayM : kb.SwayM,
            HeaveM = g.InvertHeave ? -kb.HeaveM : kb.HeaveM
        };
    }

    public void CaptureHome()
    {
        var live = RawLive;
        lock (_gate)
            _home = live.Valid ? live : PoseSample.Zero with { TimestampUtc = DateTime.UtcNow };
    }

    public void ResetVirtual() => _virtual.Reset();

    public void Start()
    {
        _live.Start();
        _virtual.Start();
    }

    public void Stop()
    {
        _virtual.Stop();
        _live.Stop();
    }

    public void Dispose()
    {
        _virtual.Dispose();
        _live.Dispose();
    }
}

public sealed class Gains
{
    public double Roll { get; set; } = 1;
    public double Pitch { get; set; } = 1;
    public double Yaw { get; set; }
    public double Surge { get; set; }
    public double Sway { get; set; }
    public double Heave { get; set; }
    public bool InvertRoll { get; set; }
    public bool InvertPitch { get; set; }
    public bool InvertYaw { get; set; }
    public bool InvertSurge { get; set; }
    public bool InvertSway { get; set; }
    public bool InvertHeave { get; set; }
    public bool SwapPitchRoll { get; set; }

    public double ApplyRoll(double v) => v * Roll * (InvertRoll ? -1 : 1);
    public double ApplyPitch(double v) => v * Pitch * (InvertPitch ? -1 : 1);
    public double ApplyYaw(double v) => v * Yaw * (InvertYaw ? -1 : 1);
    public double ApplySurge(double v) => v * Surge * (InvertSurge ? -1 : 1);
    public double ApplySway(double v) => v * Sway * (InvertSway ? -1 : 1);
    public double ApplyHeave(double v) => v * Heave * (InvertHeave ? -1 : 1);
}
