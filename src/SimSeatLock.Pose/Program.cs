using SimSeatLock.Pose.Config;
using SimSeatLock.Pose.Publish;
using SimSeatLock.Pose.Tracking;
using SimSeatLock.Pose.Witmotion;

namespace SimSeatLock.Pose;

public static class Program
{
    public const string Version = "0.1.0";

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var cli = Cli.Parse(args);
            if (cli.Help)
            {
                PrintHelp();
                return 0;
            }

            string cfgPath = cli.ConfigPath ?? FindFile("pose.json");
            string geomPath = cli.GeometryPath ?? FindFile("geometry.json");
            var cfg = PoseConfig.Load(cfgPath);
            var geom = PoseConfig.LoadGeometry(geomPath);

            if (cli.Port != null) cfg.Witmotion.Port = cli.Port;
            if (cli.Baud is int b) cfg.Witmotion.Baud = b;
            if (cli.Source != null) cfg.Source = cli.Source;

            if (cli.ListPorts)
            {
                var ports = WitmotionSerialPoseSource.ListPorts();
                Console.WriteLine(ports.Length == 0 ? "(no COM ports)" : string.Join(", ", ports));
                return 0;
            }

            var serial = new WitmotionSerialPoseSource(cfg.Serial);
            var virtualSrc = new VirtualPoseSource();
            var pose = new CompositePoseSource(serial, virtualSrc, cfg.ToGains(), cfg.Source);
            var steam = new SteamVrPoseSource(cfg.SteamVrSeated);
            var watch = new EngineWatch(cfg.GameProcesses);
            var game = new GamePoseSource(watch);
            var shm = new PoseSharedMemory();

            pose.Start();
            steam.Start();
            game.Start();

            using var form = new MainForm(cfg, geom, pose, serial, steam, game, shm, cfgPath, geomPath);
            Application.Run(form);

            game.Dispose();
            steam.Dispose();
            pose.Dispose();
            shm.Dispose();
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "SimSeatLock.Pose", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    public static string FindFile(string name)
    {
        var bases = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "config"),
            AppContext.BaseDirectory,
            Path.Combine(Directory.GetCurrentDirectory(), "config"),
            Directory.GetCurrentDirectory()
        };
        foreach (var b in bases)
        {
            var p = Path.Combine(b, name);
            if (File.Exists(p)) return p;
        }
        return Path.Combine(AppContext.BaseDirectory, "config", name);
    }

    static void PrintHelp()
    {
        Console.WriteLine($"SimSeatLock.Pose v{Version}");
        Console.WriteLine("Double-click SimSeatLock.Pose.exe; settings are publish/config/pose.json.");
        Console.WriteLine();
        Console.WriteLine("  --help");
        Console.WriteLine("  --list-ports");
        Console.WriteLine("  --port COM3");
        Console.WriteLine("  --baud 115200");
        Console.WriteLine("  --source witmotion|virtual");
        Console.WriteLine("  --config PATH");
        Console.WriteLine("  --geometry PATH");
    }
}

sealed class Cli
{
    public bool Help;
    public bool ListPorts;
    public int? Baud;
    public string? Port;
    public string? Source;
    public string? ConfigPath;
    public string? GeometryPath;

    public static Cli Parse(string[] args)
    {
        var c = new Cli();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (a)
            {
                case "--help": case "-h": case "/?": c.Help = true; break;
                case "--list-ports": c.ListPorts = true; break;
                case "--port": c.Port = Next(); break;
                case "--baud": c.Baud = int.TryParse(Next(), out var b) ? b : c.Baud; break;
                case "--source": c.Source = Next(); break;
                case "--config": c.ConfigPath = Next(); break;
                case "--geometry": c.GeometryPath = Next(); break;
            }
        }
        return c;
    }
}
