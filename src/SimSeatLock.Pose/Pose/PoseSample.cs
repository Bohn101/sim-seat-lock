namespace SimSeatLock.Pose;

/// <summary>
/// Platform pose. Rotations in degrees, translations in meters.
/// Sign convention (before invert flags): +roll right-ear down, +pitch nose up,
/// +yaw nose right, +surge forward, +sway driver's right, +heave up.
/// Do not integrate accelerometer packets into these translations.
/// </summary>
public readonly struct PoseSample
{
    public double RollDeg { get; init; }
    public double PitchDeg { get; init; }
    public double YawDeg { get; init; }
    public double SurgeM { get; init; }
    public double SwayM { get; init; }
    public double HeaveM { get; init; }
    public DateTime TimestampUtc { get; init; }
    public bool Valid { get; init; }

    public static PoseSample Zero { get; } = new() { Valid = true, TimestampUtc = DateTime.UnixEpoch };

    public PoseSample Add(in PoseSample other) => new()
    {
        RollDeg = RollDeg + other.RollDeg,
        PitchDeg = PitchDeg + other.PitchDeg,
        YawDeg = YawDeg + other.YawDeg,
        SurgeM = SurgeM + other.SurgeM,
        SwayM = SwayM + other.SwayM,
        HeaveM = HeaveM + other.HeaveM,
        TimestampUtc = TimestampUtc > other.TimestampUtc ? TimestampUtc : other.TimestampUtc,
        Valid = Valid || other.Valid
    };

    public PoseSample Subtract(in PoseSample home) => new()
    {
        RollDeg = RollDeg - home.RollDeg,
        PitchDeg = PitchDeg - home.PitchDeg,
        YawDeg = YawDeg - home.YawDeg,
        SurgeM = SurgeM - home.SurgeM,
        SwayM = SwayM - home.SwayM,
        HeaveM = HeaveM - home.HeaveM,
        TimestampUtc = TimestampUtc,
        Valid = Valid
    };
}
