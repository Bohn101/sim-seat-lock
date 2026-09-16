using System.Diagnostics;

namespace SimSeatLock.Pose.Tracking;

/// <summary>
/// Detects ACE / AMS2 / LMU / other configured executables. Presence only —
/// it does not invent an OpenXR pose.
/// </summary>
public sealed class EngineWatch
{
    readonly string[] _names;
    readonly object _gate = new();
    bool _detected;
    string _status = "no engine";

    public EngineWatch(IEnumerable<string> names)
    {
        _names = names.Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .ToArray();
    }

    public bool Detected { get { lock (_gate) return _detected; } }
    public string Status { get { lock (_gate) return _status; } }

    public void Poll()
    {
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                string name;
                try { name = p.ProcessName; }
                catch { continue; }
                foreach (var want in _names)
                {
                    if (name.Equals(want, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(want, StringComparison.OrdinalIgnoreCase))
                    {
                        lock (_gate)
                        {
                            _detected = true;
                            _status = name;
                        }
                        return;
                    }
                }
            }
        }
        catch { /* ignore */ }

        lock (_gate)
        {
            _detected = false;
            _status = "no engine";
        }
    }
}
