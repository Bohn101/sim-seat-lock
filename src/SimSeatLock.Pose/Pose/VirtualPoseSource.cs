namespace SimSeatLock.Pose;

/// <summary>
/// Numeric / slider chassis pose. Used as the sole T_rig source when
/// pose.source = virtual, or as an overlay add-on on top of Witmotion.
/// </summary>
public sealed class VirtualPoseSource : IPoseSource
{
    readonly object _gate = new();
    PoseSample _pose = PoseSample.Zero;

    public string Name => "Virtual";
    public bool IsLive => true;
    public string Status => "numeric";
    public PoseSample Latest { get { lock (_gate) return _pose; } }

    public void Start() { }
    public void Stop() { }
    public void Dispose() { }

    public void Set(PoseSample sample)
    {
        lock (_gate)
            _pose = sample with { TimestampUtc = DateTime.UtcNow, Valid = true };
    }

    public void Reset()
    {
        lock (_gate)
            _pose = PoseSample.Zero with { TimestampUtc = DateTime.UtcNow, Valid = true };
    }
}
