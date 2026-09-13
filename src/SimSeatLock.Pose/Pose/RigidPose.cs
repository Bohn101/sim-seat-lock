namespace SimSeatLock.Pose;

/// <summary>
/// OpenXR-shaped rigid pose: unit quaternion + Cartesian metres.
/// SteamVR and the future layer both collapse into this for the viz.
/// </summary>
public readonly struct RigidPose
{
    public double Px { get; init; }
    public double Py { get; init; }
    public double Pz { get; init; }
    public double Qx { get; init; }
    public double Qy { get; init; }
    public double Qz { get; init; }
    public double Qw { get; init; }
    public DateTime TimestampUtc { get; init; }
    public bool Valid { get; init; }
    public bool Live { get; init; }
    public string Space { get; init; }

    public static RigidPose Dead { get; } = new()
    {
        Qw = 1,
        Valid = false,
        Live = false,
        Space = "",
        TimestampUtc = DateTime.UnixEpoch
    };

    public static RigidPose Identity { get; } = new()
    {
        Qw = 1,
        Valid = true,
        Live = false,
        Space = "identity",
        TimestampUtc = DateTime.UnixEpoch
    };

    public PoseSample ToEulerSample()
    {
        PoseMath.QuatToYprDeg(Qx, Qy, Qz, Qw, out var yaw, out var pitch, out var roll);
        return new PoseSample
        {
            RollDeg = roll,
            PitchDeg = pitch,
            YawDeg = yaw,
            SurgeM = -Pz,
            SwayM = Px,
            HeaveM = Py,
            TimestampUtc = TimestampUtc,
            Valid = Valid
        };
    }
}
