using System.IO.Ports;

namespace SimSeatLock.Pose.Witmotion;

/// <summary>
/// Background reader for a Witmotion 0x55 IMU on RS-232/USB. Survives unplug.
/// Does not integrate accelerometer packets into position.
/// Dropped = checksum/sync failures only.
/// </summary>
public sealed class WitmotionSerialPoseSource : IPoseSource
{
    readonly SerialSettings _cfg;
    readonly object _gate = new();
    readonly WitmotionStreamParser _parser = new();
    CancellationTokenSource? _cts;
    Task? _loop;
    PoseSample _latest = PoseSample.Zero with { Valid = false };
    bool _live;
    string _status = "idle";
    double _axG, _ayG, _azG;
    long _angleCount;
    long _angleCountWindow;
    DateTime _hzAnchorUtc = DateTime.UtcNow;
    double _angleHz;

    public WitmotionSerialPoseSource(SerialSettings cfg) => _cfg = cfg;

    public string Name => string.IsNullOrWhiteSpace(_cfg.Port) ? "Witmotion (no port)" : $"Witmotion {_cfg.Port}";
    public bool IsLive { get { lock (_gate) return _live; } }
    public string Status { get { lock (_gate) return _status; } }
    public PoseSample Latest { get { lock (_gate) return _latest; } }
    public (double Ax, double Ay, double Az) AccelG { get { lock (_gate) return (_axG, _ayG, _azG); } }
    public SerialSettings Settings => _cfg;
    public long DroppedChecksum => _parser.DroppedChecksum;
    public double AngleHz { get { lock (_gate) return _angleHz; } }
    public long AngleCount { get { lock (_gate) return _angleCount; } }

    public static string[] ListPorts()
    {
        try { return SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(); }
        catch { return []; }
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
        try { _loop?.Wait(500); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        lock (_gate)
        {
            _live = false;
            _status = "stopped";
        }
    }

    public void Dispose() => Stop();

    void Run(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_cfg.Port))
        {
            SetStatus(false, "no COM port configured");
            return;
        }

        while (!token.IsCancellationRequested)
        {
            SerialPort? port = null;
            try
            {
                port = Open();
                SetStatus(true, $"open {_cfg.Port} @{_cfg.Baud}");
                var parserSink = new List<WitPacket>(16);
                var scratch = new byte[256];
                while (!token.IsCancellationRequested)
                {
                    int n;
                    try
                    {
                        if (port.BytesToRead <= 0)
                        {
                            token.WaitHandle.WaitOne(4);
                            continue;
                        }
                        n = port.Read(scratch, 0, scratch.Length);
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }

                    if (n <= 0) continue;
                    parserSink.Clear();
                    _parser.Feed(scratch.AsSpan(0, n), parserSink);
                    foreach (var pkt in parserSink)
                        Apply(pkt);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SetStatus(false, $"unplugged/retry: {Short(ex)}");
            }
            finally
            {
                try { port?.Close(); } catch { /* ignore */ }
                try { port?.Dispose(); } catch { /* ignore */ }
            }

            if (token.IsCancellationRequested) break;
            var delay = TimeSpan.FromSeconds(Math.Max(0.4, _cfg.AutoReconnectSeconds));
            token.WaitHandle.WaitOne(delay);
        }
    }

    SerialPort Open()
    {
        var p = new SerialPort
        {
            PortName = _cfg.Port,
            BaudRate = _cfg.Baud,
            DataBits = _cfg.DataBits,
            Parity = ParseParity(_cfg.Parity),
            StopBits = ParseStopBits(_cfg.StopBits),
            Handshake = Handshake.None,
            ReadTimeout = 200,
            WriteTimeout = 200,
            DtrEnable = true,
            RtsEnable = true
        };
        p.Open();
        p.DiscardInBuffer();
        return p;
    }

    void Apply(in WitPacket pkt)
    {
        if (pkt.Type == WitmotionProtocol.TypeAngle)
        {
            var sample = new PoseSample
            {
                RollDeg = WitmotionProtocol.RawToAngleDeg(pkt.D0),
                PitchDeg = WitmotionProtocol.RawToAngleDeg(pkt.D1),
                YawDeg = WitmotionProtocol.RawToAngleDeg(pkt.D2),
                TimestampUtc = DateTime.UtcNow,
                Valid = true
            };
            lock (_gate)
            {
                _latest = sample;
                _live = true;
                _status = $"live {_cfg.Port}";
                _angleCount++;
                _angleCountWindow++;
                var now = DateTime.UtcNow;
                var dt = (now - _hzAnchorUtc).TotalSeconds;
                if (dt >= 0.5)
                {
                    _angleHz = _angleCountWindow / dt;
                    _angleCountWindow = 0;
                    _hzAnchorUtc = now;
                }
            }
        }
        else if (pkt.Type == WitmotionProtocol.TypeAccel)
        {
            lock (_gate)
            {
                _axG = WitmotionProtocol.RawToAccelG(pkt.D0);
                _ayG = WitmotionProtocol.RawToAccelG(pkt.D1);
                _azG = WitmotionProtocol.RawToAccelG(pkt.D2);
            }
        }
    }

    void SetStatus(bool live, string status)
    {
        lock (_gate)
        {
            _live = live;
            _status = status;
            if (!live)
                _latest = _latest with { Valid = false };
        }
    }

    static string Short(Exception ex)
    {
        var m = ex.Message.Replace('\n', ' ');
        return m.Length <= 80 ? m : m[..80];
    }

    static Parity ParseParity(string s) => s.ToLowerInvariant() switch
    {
        "even" => Parity.Even,
        "odd" => Parity.Odd,
        "mark" => Parity.Mark,
        "space" => Parity.Space,
        _ => Parity.None
    };

    static StopBits ParseStopBits(string s) => s.ToLowerInvariant() switch
    {
        "two" => StopBits.Two,
        "onepointfive" => StopBits.OnePointFive,
        _ => StopBits.One
    };
}

public sealed class SerialSettings
{
    public string Port { get; set; } = "COM8";
    public int Baud { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Protocol { get; set; } = "Witmotion0x55";
    public double AutoReconnectSeconds { get; set; } = 1.5;
}
