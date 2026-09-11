using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace SimSeatLock.Pose.Publish;

/// <summary>
/// Process-local shared memory the OpenXR layer will map.
/// Game block is written by the layer when xrLocateViews runs; we only publish
/// T_rig + a snapshot of what we last saw. Stale game/SteamVR fields keep last value.
/// </summary>
public sealed class PoseSharedMemory : IDisposable
{
    public const string RigMapName = "Local\\SimSeatLock.Rig.v1";
    public const string GameMapName = "Local\\SimSeatLock.Game.v1";
    public const uint Magic = 0x314C5353; // SSL1
    public const uint Version = 1;

    readonly MemoryMappedFile _rig;
    readonly MemoryMappedViewAccessor _rigView;
    readonly object _gate = new();
    uint _seq;

    public PoseSharedMemory()
    {
        _rig = MemoryMappedFile.CreateOrOpen(RigMapName, Marshal.SizeOf<RigBlock>());
        _rigView = _rig.CreateViewAccessor();
    }

    public void Write(in RigBlock block)
    {
        lock (_gate)
        {
            var b = block;
            b.Magic = Magic;
            b.Version = Version;
            b.Sequence = ++_seq;
            _rigView.Write(0, ref b);
        }
    }

    public static bool TryReadGame(out GameBlock block)
    {
        block = default;
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(GameMapName);
            using var view = mmf.CreateViewAccessor();
            view.Read(0, out block);
            return block.Magic == Magic && block.Version == Version;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _rigView.Dispose();
        _rig.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RigBlock
{
    public uint Magic;
    public uint Version;
    public uint Sequence;
    public uint Flags;
    public long FileTimeUtc;
    public float RigHz;
    public uint RigDropped;
    public float RollDeg, PitchDeg, YawDeg;
    public float SurgeM, SwayM, HeaveM;
    public float Qx, Qy, Qz, Qw;
    public float EyeX, EyeY, EyeZ;
    public int Armed;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GameBlock
{
    public uint Magic;
    public uint Version;
    public uint Sequence;
    public uint Flags;
    public long FileTimeUtc;
    public int SpaceType;
    public int ViewCount;
    public float Lpx, Lpy, Lpz, Lqx, Lqy, Lqz, Lqw;
    public float Rpx, Rpy, Rpz, Rqx, Rqy, Rqz, Rqw;
}

[Flags]
public enum RigFlags : uint
{
    None = 0,
    RigLive = 1,
    SteamVrLive = 2,
    GameLive = 4,
    Armed = 8
}
