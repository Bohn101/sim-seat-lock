using System.Runtime.InteropServices;

namespace SimSeatLock.Pose.Tracking;

/// <summary>
/// Reads the SteamVR HMD via openvr_api (seated by default). Independent of the
/// title. Holds the last pose when SteamVR exits. Not a pose source for T_rig.
/// </summary>
public sealed class SteamVrPoseSource : IDisposable
{
    readonly bool _seated;
    readonly object _gate = new();
    CancellationTokenSource? _cts;
    Task? _loop;
    RigidPose _latest = RigidPose.Dead with { Space = "steamvr-seated" };
    bool _live;
    string _status = "steamvr off";

    public SteamVrPoseSource(bool seated) => _seated = seated;

    public RigidPose Latest { get { lock (_gate) return _latest; } }
    public bool IsLive { get { lock (_gate) return _live; } }
    public string Status { get { lock (_gate) return _status; } }
    public string Space => _seated ? "steamvr-seated" : "steamvr-standing";

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
            try
            {
                if (!OpenVrNative.TryEnsure(out var err))
                {
                    lock (_gate)
                    {
                        _live = false;
                        _status = string.IsNullOrEmpty(err) ? "steamvr off" : err;
                        _latest = _latest with { Live = false };
                    }
                    token.WaitHandle.WaitOne(250);
                    continue;
                }

                if (!OpenVrNative.TryReadHmd(_seated, out var pose, out var why))
                {
                    lock (_gate)
                    {
                        _live = false;
                        _status = why;
                        _latest = _latest with { Live = false };
                    }
                    token.WaitHandle.WaitOne(16);
                    continue;
                }

                pose = pose with
                {
                    Live = true,
                    Valid = true,
                    Space = Space,
                    TimestampUtc = DateTime.UtcNow
                };
                lock (_gate)
                {
                    _latest = pose;
                    _live = true;
                    _status = _seated ? "seated" : "standing";
                }
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    _live = false;
                    _status = Short(ex.Message);
                    _latest = _latest with { Live = false };
                }
                OpenVrNative.Shutdown();
            }

            token.WaitHandle.WaitOne(8);
        }

        OpenVrNative.Shutdown();
    }

    static string Short(string m)
    {
        m = m.Replace('\n', ' ');
        return m.Length <= 60 ? m : m[..60];
    }
}

static class OpenVrNative
{
    const int ApplicationBackground = 3;
    const int UniverseSeated = 0;
    const int UniverseStanding = 1;
    const int PoseCount = 1;

    static readonly object Gate = new();
    static IntPtr _module;
    static IntPtr _systemFn;
    static bool _inited;
    static GetPoseFn? _getPose;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void GetPoseFn(int origin, float seconds, IntPtr poses, uint count);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate uint InitInternal(ref int peError, int eType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ShutdownInternal();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate IntPtr GetGenericInterface([MarshalAs(UnmanagedType.LPStr)] string name, ref int peError);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate bool IsRuntimeInstalled();

    static InitInternal? _init;
    static ShutdownInternal? _shutdown;
    static GetGenericInterface? _iface;
    static IsRuntimeInstalled? _installed;

    public static bool TryEnsure(out string error)
    {
        lock (Gate)
        {
            error = "";
            if (_inited && _getPose != null)
                return true;

            if (_module == IntPtr.Zero)
            {
                foreach (var path in CandidateDlls())
                {
                    var h = LoadLibrary(path);
                    if (h != IntPtr.Zero)
                    {
                        _module = h;
                        break;
                    }
                }
                if (_module == IntPtr.Zero)
                {
                    error = "openvr_api.dll missing";
                    return false;
                }

                _init = LoadFn<InitInternal>("VR_InitInternal");
                _shutdown = LoadFn<ShutdownInternal>("VR_ShutdownInternal");
                _iface = LoadFn<GetGenericInterface>("VR_GetGenericInterface");
                _installed = LoadFn<IsRuntimeInstalled>("VR_IsRuntimeInstalled");
            }

            if (_init == null || _iface == null)
            {
                error = "openvr exports missing";
                return false;
            }

            if (_installed != null && !_installed())
            {
                error = "steamvr runtime not installed";
                return false;
            }

            int err = 0;
            _init(ref err, ApplicationBackground);
            if (err != 0)
            {
                error = $"vr init {err}";
                return false;
            }

            foreach (var ver in new[]
                     {
                         "FnTable:IVRSystem_022",
                         "FnTable:IVRSystem_021",
                         "FnTable:IVRSystem_020",
                         "FnTable:IVRSystem_019"
                     })
            {
                int ie = 0;
                var table = _iface(ver, ref ie);
                if (table == IntPtr.Zero || ie != 0)
                    continue;
                var slot = Marshal.ReadIntPtr(table, 11 * IntPtr.Size);
                if (slot == IntPtr.Zero)
                    continue;
                _getPose = Marshal.GetDelegateForFunctionPointer<GetPoseFn>(slot);
                _systemFn = table;
                _inited = true;
                error = "";
                return true;
            }

            error = "IVRSystem fntable missing";
            _shutdown?.Invoke();
            _inited = false;
            return false;
        }
    }

    public static bool TryReadHmd(bool seated, out RigidPose pose, out string why)
    {
        pose = RigidPose.Dead;
        why = "no pose";
        GetPoseFn? fn;
        lock (Gate) fn = _getPose;
        if (fn == null)
        {
            why = "steamvr off";
            return false;
        }

        int bytes = Marshal.SizeOf<TrackedDevicePose>();
        var mem = Marshal.AllocHGlobal(bytes * PoseCount);
        try
        {
            fn(seated ? UniverseSeated : UniverseStanding, 0, mem, PoseCount);
            var raw = Marshal.PtrToStructure<TrackedDevicePose>(mem);
            if (!raw.bPoseIsValid || !raw.bDeviceIsConnected)
            {
                why = raw.bDeviceIsConnected ? "hmd pose invalid" : "hmd disconnected";
                return false;
            }

            Matrix34ToPose(in raw.m, out pose);
            return true;
        }
        catch (Exception ex)
        {
            why = ex.GetType().Name;
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            if (!_inited)
                return;
            try { _shutdown?.Invoke(); } catch { /* ignore */ }
            _inited = false;
            _getPose = null;
            _systemFn = IntPtr.Zero;
        }
    }

    static T? LoadFn<T>(string name) where T : Delegate
    {
        var p = GetProcAddress(_module, name);
        return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    static IEnumerable<string> CandidateDlls()
    {
        yield return "openvr_api.dll";
        yield return Path.Combine(AppContext.BaseDirectory, "openvr_api.dll");
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(pf86, @"Steam\steamapps\common\SteamVR\bin\win64\openvr_api.dll");
        yield return Path.Combine(pf86, @"Steam\steamapps\common\SteamVR\bin\win32\openvr_api.dll");
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(pf, @"Steam\steamapps\common\SteamVR\bin\win64\openvr_api.dll");
    }

    static void Matrix34ToPose(in HmdMatrix34 m, out RigidPose pose)
    {
        double r00 = m.M0, r01 = m.M1, r02 = m.M2;
        double r10 = m.M4, r11 = m.M5, r12 = m.M6;
        double r20 = m.M8, r21 = m.M9, r22 = m.M10;
        double trace = r00 + r11 + r22;
        double qx, qy, qz, qw;
        if (trace > 0)
        {
            double s = Math.Sqrt(trace + 1.0) * 2.0;
            qw = 0.25 * s;
            qx = (r21 - r12) / s;
            qy = (r02 - r20) / s;
            qz = (r10 - r01) / s;
        }
        else if (r00 > r11 && r00 > r22)
        {
            double s = Math.Sqrt(1.0 + r00 - r11 - r22) * 2.0;
            qw = (r21 - r12) / s;
            qx = 0.25 * s;
            qy = (r01 + r10) / s;
            qz = (r02 + r20) / s;
        }
        else if (r11 > r22)
        {
            double s = Math.Sqrt(1.0 + r11 - r00 - r22) * 2.0;
            qw = (r02 - r20) / s;
            qx = (r01 + r10) / s;
            qy = 0.25 * s;
            qz = (r12 + r21) / s;
        }
        else
        {
            double s = Math.Sqrt(1.0 + r22 - r00 - r11) * 2.0;
            qw = (r10 - r01) / s;
            qx = (r02 + r20) / s;
            qy = (r12 + r21) / s;
            qz = 0.25 * s;
        }
        PoseMath.Normalize(ref qx, ref qy, ref qz, ref qw);
        pose = new RigidPose
        {
            Px = m.M3,
            Py = m.M7,
            Pz = m.M11,
            Qx = qx, Qy = qy, Qz = qz, Qw = qw,
            Valid = true
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HmdMatrix34
    {
        public float M0, M1, M2, M3;
        public float M4, M5, M6, M7;
        public float M8, M9, M10, M11;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HmdVector3
    {
        public float X, Y, Z;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TrackedDevicePose
    {
        public HmdMatrix34 m;
        public HmdVector3 Velocity;
        public HmdVector3 AngularVelocity;
        public int TrackingResult;
        [MarshalAs(UnmanagedType.I1)] public bool bPoseIsValid;
        [MarshalAs(UnmanagedType.I1)] public bool bDeviceIsConnected;
    }
}
