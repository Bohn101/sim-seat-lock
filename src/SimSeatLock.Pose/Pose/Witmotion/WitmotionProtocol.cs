namespace SimSeatLock.Pose.Witmotion;

/// <summary>
/// Witmotion WT901 / JY901 standard UART protocol (public docs):
/// https://wit-motion.gitbook.io/witmotion-sdk/wit-standard-protocol/wit-standard-communication-protocol
///
/// Each packet is 11 bytes:
///   [0]    0x55 header
///   [1]    TYPE (0x50 time, 0x51 accel, 0x52 gyro, 0x53 angle, 0x54 mag,
///                0x55 port, 0x56 baro, 0x57 lat/lon, 0x58 gps speed,
///                0x59 quat, 0x5A GPS accuracy, 0x5F register read)
///   [2..9] four signed int16 little-endian fields (lo, hi)
///   [10]   SUMCRC = (sum of bytes 0..9) &amp; 0xFF
///
/// Angle (TYPE 0x53):
///   Roll  = Int16 / 32768 * 180  degrees  (X)
///   Pitch = Int16 / 32768 * 180  degrees  (Y)
///   Yaw   = Int16 / 32768 * 180  degrees  (Z)
/// </summary>
public static class WitmotionProtocol
{
    public const byte Header = 0x55;
    public const int PacketLength = 11;
    public const byte TypeTime = 0x50;
    public const byte TypeAccel = 0x51;
    public const byte TypeGyro = 0x52;
    public const byte TypeAngle = 0x53;
    public const byte TypeMag = 0x54;
    public const byte TypePort = 0x55;
    public const byte TypeQuat = 0x59;
    public const byte TypeRegRead = 0x5F;

    public const double AngleScale = 180.0 / 32768.0;
    public const double AccelGScale = 16.0 / 32768.0;
    public const double GyroDpsScale = 2000.0 / 32768.0;
    public const double QuatScale = 1.0 / 32768.0;

    public static bool IsKnownType(byte type) =>
        type is >= TypeTime and <= 0x5A or TypeRegRead;

    public static byte Checksum(ReadOnlySpan<byte> packet11)
    {
        int s = 0;
        for (int i = 0; i < 10; i++)
            s += packet11[i];
        return (byte)s;
    }

    public static bool TryParse(ReadOnlySpan<byte> buf, out WitPacket packet)
    {
        packet = default;
        if (buf.Length < PacketLength)
            return false;
        if (buf[0] != Header)
            return false;
        if (!IsKnownType(buf[1]))
            return false;
        if (Checksum(buf) != buf[10])
            return false;
        packet = new WitPacket
        {
            Type = buf[1],
            D0 = ReadInt16Le(buf, 2),
            D1 = ReadInt16Le(buf, 4),
            D2 = ReadInt16Le(buf, 6),
            D3 = ReadInt16Le(buf, 8)
        };
        return true;
    }

    public static short ReadInt16Le(ReadOnlySpan<byte> buf, int offset)
    {
        unchecked
        {
            return (short)(buf[offset] | (buf[offset + 1] << 8));
        }
    }

    public static double RawToAngleDeg(short raw) => raw * AngleScale;
    public static double RawToAccelG(short raw) => raw * AccelGScale;
    public static double RawToGyroDps(short raw) => raw * GyroDpsScale;
    public static double RawToQuat(short raw) => raw * QuatScale;

    public static byte[] BuildPacket(byte type, short d0, short d1, short d2, short d3)
    {
        var p = new byte[PacketLength];
        p[0] = Header;
        p[1] = type;
        WriteInt16Le(p, 2, d0);
        WriteInt16Le(p, 4, d1);
        WriteInt16Le(p, 6, d2);
        WriteInt16Le(p, 8, d3);
        p[10] = Checksum(p);
        return p;
    }

    public static void WriteInt16Le(byte[] buf, int offset, short value)
    {
        unchecked
        {
            buf[offset] = (byte)value;
            buf[offset + 1] = (byte)(value >> 8);
        }
    }

    public static short AngleDegToRaw(double deg) =>
        (short)Math.Clamp(Math.Round(deg / AngleScale), short.MinValue, short.MaxValue);
}

public readonly struct WitPacket
{
    public byte Type { get; init; }
    public short D0 { get; init; }
    public short D1 { get; init; }
    public short D2 { get; init; }
    public short D3 { get; init; }
}

/// <summary>
/// Sliding-window reassembler. DroppedChecksum increments only when a 0x55 +
/// known-type candidate fails the SUMCRC (sync/checksum). Valid accel/gyro/port
/// packets are not drops.
/// </summary>
public sealed class WitmotionStreamParser
{
    readonly List<byte> _buf = new(256);

    public long DroppedChecksum { get; private set; }

    public int Feed(ReadOnlySpan<byte> incoming, List<WitPacket> sink)
    {
        for (int i = 0; i < incoming.Length; i++)
            _buf.Add(incoming[i]);

        int parsed = 0;
        int offset = 0;
        Span<byte> probe = stackalloc byte[WitmotionProtocol.PacketLength];
        while (_buf.Count - offset >= WitmotionProtocol.PacketLength)
        {
            if (_buf[offset] != WitmotionProtocol.Header)
            {
                offset++;
                continue;
            }

            for (int i = 0; i < probe.Length; i++)
                probe[i] = _buf[offset + i];

            byte type = probe[1];
            if (!WitmotionProtocol.IsKnownType(type))
            {
                offset++;
                continue;
            }

            if (WitmotionProtocol.TryParse(probe, out var pkt))
            {
                sink.Add(pkt);
                parsed++;
                offset += WitmotionProtocol.PacketLength;
            }
            else
            {
                DroppedChecksum++;
                offset++;
            }
        }
        if (offset > 0)
            _buf.RemoveRange(0, offset);
        if (_buf.Count > 2048)
            _buf.Clear();
        return parsed;
    }
}
