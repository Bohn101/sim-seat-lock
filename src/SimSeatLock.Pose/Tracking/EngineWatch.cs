using System.Diagnostics;

namespace SimSeatLock.Pose.Tracking;

/// <summary>
/// Detects ACE / AMS2 / LMU / other configured executables. Presence only —
/// it does not invent an OpenXR pose. Match is Process.ProcessName
/// (no .exe). Spaces matter: "Le Mans Ultimate" is the LMU name.
/// </summary>
public sealed class EngineWatch
{
    string[] _names;
    readonly object _gate = new();
    bool _detected;
    string _status = "no engine";

    public EngineWatch(IEnumerable<string> names) => _names = Normalize(names);

    public bool Detected { get { lock (_gate) return _detected; } }
    public string Status { get { lock (_gate) return _status; } }

    public void SetNames(IEnumerable<string> names)
    {
        lock (_gate) _names = Normalize(names);
    }

    static string[] Normalize(IEnumerable<string> names) =>
        names.Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .ToArray();

    public void Poll()
    {
        string[] want;
        lock (_gate) want = _names;
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                string name;
                try { name = p.ProcessName; }
                catch { continue; }
                foreach (var w in want)
                {
                    if (name.Equals(w, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(w, StringComparison.OrdinalIgnoreCase))
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
