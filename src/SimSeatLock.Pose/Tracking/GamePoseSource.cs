using SimSeatLock.Pose.Publish;

namespace SimSeatLock.Pose.Tracking;

/// <summary>
/// Reads Local\SimSeatLock.Game.v1 written by SimSeatLock.Layer.
/// Holds last / zero and stays !Valid until the layer is loaded.
/// Does not invent a pose from the engine process.
/// </summary>
public sealed class GamePoseSource : IDisposable
{
    readonly EngineWatch _watch;
    readonly object _gate = new();
    CancellationTokenSource? _cts;
    Task? _loop;
    RigidPose _left = RigidPose.Dead with { Space = "game" };
    RigidPose _right = RigidPose.Dead with { Space = "game-right" };
    string _detail = "waiting for layer";
    bool _layerLive;
    uint _lastSeq;
    DateTime _lastPollUtc = DateTime.UnixEpoch;

    public GamePoseSource(EngineWatch watch) => _watch = watch;

    public EngineWatch Watch => _watch;
    public RigidPose Left { get { lock (_gate) return _left; } }
    public RigidPose Right { get { lock (_gate) return _right; } }
    public bool LayerLive { get { lock (_gate) return _layerLive; } }
    public string Detail { get { lock (_gate) return _detail; } }

    public string Status
    {
        get
        {
            _watch.Poll();
            if (_watch.Detected)
                return LayerLive
                    ? $"engine up ({_watch.Status}), layer live"
                    : $"engine up ({_watch.Status}), waiting for layer";
            return LayerLive ? "layer live, no engine" : "waiting for layer";
        }
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => Run(token), token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _loop?.Wait(400); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public void Dispose() => Stop();

    void Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if ((DateTime.UtcNow - _lastPollUtc).TotalMilliseconds >= 500)
            {
                _watch.Poll();
                _lastPollUtc = DateTime.UtcNow;
            }

            if (PoseSharedMemory.TryReadGame(out var block))
            {
                var now = DateTime.UtcNow;
                var left = new RigidPose
                {
                    Px = block.Lpx, Py = block.Lpy, Pz = block.Lpz,
                    Qx = block.Lqx, Qy = block.Lqy, Qz = block.Lqz,
                    Qw = block.Lqw == 0 && block.Lqx == 0 && block.Lqy == 0 && block.Lqz == 0 ? 1 : block.Lqw,
                    TimestampUtc = now,
                    Valid = true,
                    Live = true,
                    Space = "game"
                };
                var right = new RigidPose
                {
                    Px = block.Rpx, Py = block.Rpy, Pz = block.Rpz,
                    Qx = block.Rqx, Qy = block.Rqy, Qz = block.Rqz,
                    Qw = block.Rqw == 0 && block.Rqx == 0 && block.Rqy == 0 && block.Rqz == 0 ? 1 : block.Rqw,
                    TimestampUtc = now,
                    Valid = true,
                    Live = true,
                    Space = "game-right"
                };
                lock (_gate)
                {
                    _left = left;
                    _right = right;
                    _layerLive = true;
                    _lastSeq = block.Sequence;
                    _detail = $"layer seq {block.Sequence}";
                }
            }
            else
            {
                lock (_gate)
                {
                    _layerLive = false;
                    _left = _left with { Live = false, Valid = false };
                    _right = _right with { Live = false, Valid = false };
                    _detail = "waiting for layer";
                }
            }

            token.WaitHandle.WaitOne(8);
        }
    }
}
