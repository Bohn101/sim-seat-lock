namespace SimSeatLock.Pose;

/// <summary>
/// T_view = T_cor * inv(T_rig) * inv(T_cor) * T_hmd
/// T_cor is the IMU-to-eye translation from geometry.json.
/// </summary>
public static class PoseMath
{
    /// <summary>
    /// Vehicle RPY (deg) → OpenXR Y-up, -Z forward.
    /// +roll = lean right → rot Z by -roll. +pitch = nose up → rot X by -pitch.
    /// +yaw = nose right → rot Y by -yaw. Do not use Config swap to fix this pack.
    /// </summary>
    public static void EulerDegToQuat(double rollDeg, double pitchDeg, double yawDeg,
        out double qx, out double qy, out double qz, out double qw)
    {
        double x = -pitchDeg * Math.PI / 180.0;
        double y = -yawDeg * Math.PI / 180.0;
        double z = -rollDeg * Math.PI / 180.0;
        double cx = Math.Cos(x * 0.5), sx = Math.Sin(x * 0.5);
        double cy = Math.Cos(y * 0.5), sy = Math.Sin(y * 0.5);
        double cz = Math.Cos(z * 0.5), sz = Math.Sin(z * 0.5);
        qw = cx * cy * cz + sx * sy * sz;
        qx = sx * cy * cz - cx * sy * sz;
        qy = cx * sy * cz + sx * cy * sz;
        qz = cx * cy * sz - sx * sy * cz;
        Normalize(ref qx, ref qy, ref qz, ref qw);
    }

    public static void QuatToYprDeg(double qx, double qy, double qz, double qw,
        out double yawDeg, out double pitchDeg, out double rollDeg)
    {
        Normalize(ref qx, ref qy, ref qz, ref qw);
        double sinp = 2.0 * (qw * qy - qz * qx);
        sinp = Math.Clamp(sinp, -1.0, 1.0);
        double pitch = Math.Asin(sinp);
        double yaw = Math.Atan2(2.0 * (qw * qz + qx * qy), 1.0 - 2.0 * (qy * qy + qz * qz));
        double roll = Math.Atan2(2.0 * (qw * qx + qy * qz), 1.0 - 2.0 * (qx * qx + qy * qy));
        yawDeg = yaw * 180.0 / Math.PI;
        pitchDeg = pitch * 180.0 / Math.PI;
        rollDeg = roll * 180.0 / Math.PI;
    }

    public static void QuatMul(
        double ax, double ay, double az, double aw,
        double bx, double by, double bz, double bw,
        out double x, out double y, out double z, out double w)
    {
        w = aw * bw - ax * bx - ay * by - az * bz;
        x = aw * bx + ax * bw + ay * bz - az * by;
        y = aw * by - ax * bz + ay * bw + az * bx;
        z = aw * bz + ax * by - ay * bx + az * bw;
    }

    public static void QuatConjugate(double x, double y, double z, double w,
        out double cx, out double cy, out double cz, out double cw)
    {
        cx = -x; cy = -y; cz = -z; cw = w;
    }

    public static void Rotate(double qx, double qy, double qz, double qw,
        double vx, double vy, double vz,
        out double ox, out double oy, out double oz)
    {
        QuatMul(qx, qy, qz, qw, vx, vy, vz, 0, out var ix, out var iy, out var iz, out var iw);
        QuatConjugate(qx, qy, qz, qw, out var cx, out var cy, out var cz, out var cw);
        QuatMul(ix, iy, iz, iw, cx, cy, cz, cw, out ox, out oy, out oz, out _);
    }

    public static RigidPose Compose(in RigidPose a, in RigidPose b)
    {
        QuatMul(a.Qx, a.Qy, a.Qz, a.Qw, b.Qx, b.Qy, b.Qz, b.Qw, out var qx, out var qy, out var qz, out var qw);
        Rotate(a.Qx, a.Qy, a.Qz, a.Qw, b.Px, b.Py, b.Pz, out var rx, out var ry, out var rz);
        return new RigidPose
        {
            Px = a.Px + rx,
            Py = a.Py + ry,
            Pz = a.Pz + rz,
            Qx = qx, Qy = qy, Qz = qz, Qw = qw,
            TimestampUtc = a.TimestampUtc > b.TimestampUtc ? a.TimestampUtc : b.TimestampUtc,
            Valid = a.Valid && b.Valid,
            Live = a.Live && b.Live,
            Space = a.Space
        };
    }

    public static RigidPose Inverse(in RigidPose t)
    {
        QuatConjugate(t.Qx, t.Qy, t.Qz, t.Qw, out var qx, out var qy, out var qz, out var qw);
        Rotate(qx, qy, qz, qw, t.Px, t.Py, t.Pz, out var px, out var py, out var pz);
        return new RigidPose
        {
            Px = -px, Py = -py, Pz = -pz,
            Qx = qx, Qy = qy, Qz = qz, Qw = qw,
            TimestampUtc = t.TimestampUtc,
            Valid = t.Valid,
            Live = t.Live,
            Space = t.Space
        };
    }

    public static RigidPose FromRig(in PoseSample rig)
    {
        EulerDegToQuat(rig.RollDeg, rig.PitchDeg, rig.YawDeg, out var qx, out var qy, out var qz, out var qw);
        return new RigidPose
        {
            Px = rig.SwayM,
            Py = rig.HeaveM,
            Pz = -rig.SurgeM,
            Qx = qx, Qy = qy, Qz = qz, Qw = qw,
            TimestampUtc = rig.TimestampUtc,
            Valid = rig.Valid,
            Live = rig.Valid,
            Space = "rig"
        };
    }

    public static RigidPose Compensate(in RigidPose hmd, in PoseSample rig, double eyeX, double eyeY, double eyeZ, bool armed)
    {
        if (!armed || !hmd.Valid)
            return hmd;

        var cor = new RigidPose
        {
            Px = eyeX, Py = eyeY, Pz = eyeZ,
            Qw = 1, Valid = true, Live = true, Space = hmd.Space, TimestampUtc = hmd.TimestampUtc
        };
        var tRig = FromRig(rig);
        var invRig = Inverse(tRig);
        var invCor = Inverse(cor);
        var tmp = Compose(invCor, hmd);
        tmp = Compose(invRig, tmp);
        var view = Compose(cor, tmp);
        return view with { Live = hmd.Live, Space = hmd.Space, TimestampUtc = hmd.TimestampUtc };
    }

    public static RigidPose Delta(in RigidPose expected, in RigidPose adjusted)
    {
        if (!expected.Valid)
            return RigidPose.Dead;
        if (!adjusted.Valid)
            return expected with { Space = "delta" };
        var d = Compose(Inverse(adjusted), expected);
        return d with { Live = expected.Live, Space = "delta", TimestampUtc = expected.TimestampUtc };
    }

    public static void Normalize(ref double x, ref double y, ref double z, ref double w)
    {
        double n = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (n < 1e-12) { x = 0; y = 0; z = 0; w = 1; return; }
        x /= n; y /= n; z /= n; w /= n;
    }
}
