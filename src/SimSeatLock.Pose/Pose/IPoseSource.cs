namespace SimSeatLock.Pose;

/// <summary>
/// Live 6-DoF chassis pose. v0 ships Witmotion serial + virtual numeric.
/// Predictive command feed later implements this same interface.
/// </summary>
public interface IPoseSource : IDisposable
{
    string Name { get; }
    bool IsLive { get; }
    string Status { get; }
    PoseSample Latest { get; }
    void Start();
    void Stop();
}
