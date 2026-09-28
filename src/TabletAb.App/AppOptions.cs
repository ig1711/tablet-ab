using System.Globalization;
using TabletAb.App.Rendering;
using Veldrid;

namespace TabletAb.App;

/// <summary>Which device source the app connects to.</summary>
public enum InputSource
{
    None,
    VendorUsb,
    Simulated,
}

/// <summary>Cursor-point input mode (the two lazer-style options plus the debug adapter).</summary>
public enum PointerMode
{
    /// <summary>No cursor points.</summary>
    Off,

    /// <summary>Internal debug: sub-pixel points from the vendor frame stream.</summary>
    Debug,

    /// <summary>Internal release HID via the OpenTabletDriver library (lazer-style).</summary>
    Otd,

    /// <summary>External driver: sample the OS pointer.</summary>
    External,
}

/// <summary>Which command protocol to speak to the vendor firmware.</summary>
public enum ProtocolPreference
{
    /// <summary>Detect from the first frame's version (v6 currently, v5 legacy).</summary>
    Auto,

    /// <summary>Force the legacy v5 command set (previous DEBUG_MIN firmware).</summary>
    V5,

    /// <summary>Force the v6 command set (hs611-min-ab).</summary>
    V6,
}

/// <summary>
/// Startup options parsed from the command line. Everything here is also
/// changeable at runtime from the UI (backend changes are experimental).
/// </summary>
public sealed class AppOptions
{
    public GraphicsBackend? Backend { get; private set; }

    public FrameSyncMode FrameSync { get; set; } = FrameSyncMode.VSync;

    /// <summary>Display refresh rate used for the LimitNx modes.</summary>
    public int RefreshHz { get; set; } = 60;

    public int Width { get; set; } = 1560;

    public int Height { get; set; } = 1000;

    /// <summary>Run this many frames then exit (0 = until the window is closed). For smoke tests.</summary>
    public int Frames { get; set; }

    /// <summary>Device source to connect at startup (None = stay disconnected).</summary>
    public InputSource Source { get; set; } = InputSource.None;

    /// <summary>Frame rate for the simulated source.</summary>
    public double SimulatedRateHz { get; set; } = 1000.0;

    /// <summary>Headless: read N frames, print statistics, exit.</summary>
    public int DumpFrames { get; set; }

    /// <summary>Headless: list matching USB devices and exit.</summary>
    public bool ListDevices { get; set; }

    /// <summary>Cursor-point input mode to activate at startup.</summary>
    public PointerMode Pointer { get; set; } = PointerMode.Off;

    /// <summary>Command protocol to use (auto-detect by default).</summary>
    public ProtocolPreference Protocol { get; set; } = ProtocolPreference.Auto;

    public static AppOptions Parse(string[] args)
    {
        var options = new AppOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--backend" when i + 1 < args.Length:
                    if (Enum.TryParse(args[++i], ignoreCase: true, out GraphicsBackend backend))
                        options.Backend = backend;
                    break;

                case "--frame-sync" when i + 1 < args.Length:
                    options.FrameSync = ParseFrameSync(args[++i]);
                    break;

                case "--refresh-hz" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hz) && hz > 0)
                        options.RefreshHz = hz;
                    break;

                case "--width" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int w) && w > 0)
                        options.Width = w;
                    break;

                case "--height" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int h) && h > 0)
                        options.Height = h;
                    break;

                case "--frames" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int frames) && frames > 0)
                        options.Frames = frames;
                    break;

                case "--source" when i + 1 < args.Length:
                    options.Source = ParseSource(args[++i]);
                    break;

                case "--sim-rate" when i + 1 < args.Length:
                    if (double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out double rate) && rate > 0)
                        options.SimulatedRateHz = rate;
                    break;

                case "--dump-frames" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out int dump) && dump > 0)
                        options.DumpFrames = dump;
                    break;

                case "--list-devices":
                    options.ListDevices = true;
                    break;

                case "--pointer" when i + 1 < args.Length:
                    options.Pointer = ParsePointer(args[++i]);
                    break;

                case "--protocol" when i + 1 < args.Length:
                    options.Protocol = ParseProtocol(args[++i]);
                    break;

                case "-h":
                case "--help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    private static FrameSyncMode ParseFrameSync(string value) => value.ToLowerInvariant() switch
    {
        "unlimited" => FrameSyncMode.Unlimited,
        "vsync" => FrameSyncMode.VSync,
        "2x" => FrameSyncMode.Limit2x,
        "4x" => FrameSyncMode.Limit4x,
        "8x" => FrameSyncMode.Limit8x,
        _ => FrameSyncMode.VSync,
    };

    private static InputSource ParseSource(string value) => value.ToLowerInvariant() switch
    {
        "vendor" or "usb" or "vendor-usb" => InputSource.VendorUsb,
        "sim" or "simulated" => InputSource.Simulated,
        "none" => InputSource.None,
        _ => InputSource.None,
    };

    private static PointerMode ParsePointer(string value) => value.ToLowerInvariant() switch
    {
        "debug" or "frames" => PointerMode.Debug,
        "otd" or "internal" or "hid" => PointerMode.Otd,
        "external" or "os" => PointerMode.External,
        _ => PointerMode.Off,
    };

    private static ProtocolPreference ParseProtocol(string value) => value.ToLowerInvariant() switch
    {
        "v5" or "5" or "legacy" => ProtocolPreference.V5,
        "v6" or "6" => ProtocolPreference.V6,
        _ => ProtocolPreference.Auto,
    };

    private static void PrintUsage()
    {
        Console.WriteLine($"{Core.ProductInfo.DisplayName} {Core.ProductInfo.Version}");
        Console.WriteLine();
        Console.WriteLine("Usage: tablet-ab [options]");
        Console.WriteLine();
        Console.WriteLine("  --backend <vulkan|opengl|opengles|d3d11>  preferred graphics backend");
        Console.WriteLine("  --frame-sync <unlimited|vsync|2x|4x|8x>  frame pacing mode (default: vsync)");
        Console.WriteLine("  --refresh-hz <n>                         display refresh, for the Nx modes (default: 60)");
        Console.WriteLine("  --width <n> --height <n>                 window size (default: 1560x1000)");
        Console.WriteLine("  --frames <n>                             run n frames then exit (smoke test)");
        Console.WriteLine("  --source <none|vendor|sim>               connect a device at startup");
        Console.WriteLine("  --sim-rate <hz>                          simulated source rate (default: 1000)");
        Console.WriteLine("  --dump-frames <n>                        headless: read n frames, print stats, exit");
        Console.WriteLine("  --list-devices                           headless: list 256c:6111 devices, exit");
        Console.WriteLine("  --pointer <off|debug|otd|external>       cursor-point input mode (default: off)");
        Console.WriteLine("  --protocol <auto|v5|v6>                  firmware command protocol (default: auto)");
        Console.WriteLine("  -h, --help                               show this help");
    }
}
