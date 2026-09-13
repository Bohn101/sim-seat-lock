using System.Text.Json;
using System.Text.Json.Serialization;
using SimSeatLock.Pose.Witmotion;

namespace SimSeatLock.Pose.Config;

public sealed class PoseConfig
{
    static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Source { get; set; } = "witmotion";
    public SerialSettings Witmotion { get; set; } = new() { Port = "COM8", Baud = 115200 };
    public string HomeKey { get; set; } = "Z";
    public InvertFlags Invert { get; set; } = new();
    public bool SwapPitchRoll { get; set; }
    public AxisGains GainRotDeg { get; set; } = new() { Roll = 1, Pitch = 1, Yaw = 0 };
    public AxisGains GainTransM { get; set; } = new();
    public bool SteamVrSeated { get; set; } = true;
    public bool PreviewArmed { get; set; }
    public List<string> GameProcesses { get; set; } =
    [
        "AssettoCorsaEVO",
        "assetto_corsa_evo",
        "AMS2AVX",
        "AMS2",
        "iRacingSim64DX11"
    ];

    [JsonIgnore]
    public SerialSettings Serial => Witmotion;

    public Gains ToGains() => new()
    {
        Roll = GainRotDeg.Roll,
        Pitch = GainRotDeg.Pitch,
        Yaw = GainRotDeg.Yaw,
        Surge = GainTransM.Surge,
        Sway = GainTransM.Sway,
        Heave = GainTransM.Heave,
        InvertRoll = Invert.Roll,
        InvertPitch = Invert.Pitch,
        InvertYaw = Invert.Yaw,
        SwapPitchRoll = SwapPitchRoll
    };

    public void ApplyGains(Gains g)
    {
        GainRotDeg.Roll = g.Roll;
        GainRotDeg.Pitch = g.Pitch;
        GainRotDeg.Yaw = g.Yaw;
        GainTransM.Surge = g.Surge;
        GainTransM.Sway = g.Sway;
        GainTransM.Heave = g.Heave;
        Invert.Roll = g.InvertRoll;
        Invert.Pitch = g.InvertPitch;
        Invert.Yaw = g.InvertYaw;
        SwapPitchRoll = g.SwapPitchRoll;
    }

    public static PoseConfig Load(string path)
    {
        if (!File.Exists(path))
            return new PoseConfig();
        var cfg = JsonSerializer.Deserialize<PoseConfig>(File.ReadAllText(path), JsonOpt) ?? new PoseConfig();
        cfg.Witmotion ??= new SerialSettings { Port = "COM8", Baud = 115200 };
        if (string.IsNullOrWhiteSpace(cfg.Witmotion.Port))
            cfg.Witmotion.Port = "COM8";
        if (cfg.Witmotion.Baud <= 0)
            cfg.Witmotion.Baud = 115200;
        if (string.IsNullOrWhiteSpace(cfg.Source))
            cfg.Source = "witmotion";
        cfg.GameProcesses ??= [];
        return cfg;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpt) + Environment.NewLine);
    }

    public static GeometryConfig LoadGeometry(string path)
    {
        if (!File.Exists(path))
            return new GeometryConfig();
        var g = JsonSerializer.Deserialize<GeometryConfig>(File.ReadAllText(path), JsonOpt) ?? new GeometryConfig();
        g.Normalize();
        return g;
    }

    public static void SaveGeometry(string path, GeometryConfig geom)
    {
        geom.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(geom, JsonOpt) + Environment.NewLine);
    }
}

public sealed class InvertFlags
{
    public bool Roll { get; set; }
    public bool Pitch { get; set; }
    public bool Yaw { get; set; }
}

public sealed class AxisGains
{
    public double Roll { get; set; }
    public double Pitch { get; set; }
    public double Yaw { get; set; }
    public double Surge { get; set; }
    public double Sway { get; set; }
    public double Heave { get; set; }
}

public sealed class GeometryConfig
{
    public string Comment { get; set; } =
        "CoR is the IMU (platform center). Eyes ~1.0-1.2 m above, 0.27 m aft of CoR. No TV canvas.";

    public double EyeForwardM { get; set; } = -0.27;
    public double EyeRightM { get; set; }
    public double EyeUpM { get; set; } = 1.10;
    public Xyz ImuToEyeM { get; set; } = new() { X = 0, Y = 1.10, Z = 0.27 };

    public void Normalize()
    {
        if (EyeUpM <= 0) EyeUpM = 1.10;
        ImuToEyeM ??= new Xyz();
        ImuToEyeM.X = EyeRightM;
        ImuToEyeM.Y = EyeUpM;
        ImuToEyeM.Z = -EyeForwardM;
    }

    public void ApplyDerivedLabels()
    {
        ImuToEyeM ??= new Xyz();
        EyeRightM = ImuToEyeM.X;
        EyeUpM = ImuToEyeM.Y != 0 ? ImuToEyeM.Y : 1.10;
        EyeForwardM = -ImuToEyeM.Z;
        Normalize();
    }
}

public sealed class Xyz
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
