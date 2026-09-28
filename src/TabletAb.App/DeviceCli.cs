using System.Diagnostics;
using TabletAb.Core.Statistics;
using TabletAb.Input;

namespace TabletAb.App;

/// <summary>
/// Headless device commands: list matching USB devices, or stream frames and
/// print live statistics without opening a window. Useful for verifying the
/// transport against real hardware over SSH/CI.
/// </summary>
public static class DeviceCli
{
    private const double DumpTimeoutSeconds = 15.0;

    /// <summary>List devices matching 256c:6111.</summary>
    public static int ListDevices()
    {
        IReadOnlyList<VendorUsbDeviceInfo> devices = VendorUsbLocator.Find();

        if (devices.Count == 0)
        {
            Console.Error.WriteLine("no 256c:6111 device found (is the debug firmware running and the tablet plugged in?)");
            return 1;
        }

        foreach (VendorUsbDeviceInfo device in devices)
        {
            Console.WriteLine(
                $"{device.VendorId:X4}:{device.ProductId:X4}  " +
                $"product=\"{device.Product}\" serial=\"{device.Serial}\" openable={device.Openable}");
        }

        return 0;
    }

    /// <summary>Open a source, stream frames, print statistics, and exit.</summary>
    public static int DumpFrames(AppOptions options)
    {
        using IFrameSource source = CreateSource(options);

        var statistics = new LiveStatistics();
        var gate = new object();
        var done = new ManualResetEventSlim(false);
        long seen = 0;
        string? lostReason = null;

        source.FrameReceived += (_, e) =>
        {
            lock (gate)
                statistics.Add(e.Frame);

            if (Interlocked.Increment(ref seen) >= options.DumpFrames)
                done.Set();
        };

        source.Lost += (_, e) =>
        {
            lostReason = e.Reason;
            done.Set();
        };

        Console.WriteLine($"opening {source.DisplayName}...");
        try
        {
            source.Open();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"open failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"streaming; target {options.DumpFrames} frames (timeout {DumpTimeoutSeconds:0}s)");

        var clock = Stopwatch.StartNew();
        int lastSecond = -1;
        while (!done.Wait(100))
        {
            int second = (int)clock.Elapsed.TotalSeconds;
            if (second != lastSecond)
            {
                lastSecond = second;
                PrintLine(statistics, source, gate);
            }

            if (clock.Elapsed.TotalSeconds > DumpTimeoutSeconds)
                break;
        }

        PrintLine(statistics, source, gate, prefix: "final ");

        if (lostReason is not null)
        {
            Console.Error.WriteLine($"transport lost: {lostReason}");
            return 2;
        }

        return seen >= options.DumpFrames ? 0 : 1;
    }

    private static void PrintLine(LiveStatistics statistics, IFrameSource source, object gate, string prefix = "")
    {
        LiveStatsSnapshot snapshot;
        lock (gate)
            snapshot = statistics.Snapshot(source.FramesReceived, source.Errors, source.BytesReceived);

        Console.WriteLine(
            $"{prefix}frames={snapshot.Frames} errors={snapshot.Errors} " +
            $"{snapshot.ScanRateHz,8:F1}Hz host={snapshot.Fps,7:F1}fps " +
            $"scan={snapshot.ScanTimeMeanUs,6:F0}us " +
            $"pen={(snapshot.Pen ? "yes" : "no ")} freq={snapshot.Frequency} " +
            $"xpeak={snapshot.XPeak} ypeak={snapshot.YPeak} xa={snapshot.XMax} ya={snapshot.YMax}");
    }

    private static IFrameSource CreateSource(AppOptions options) => options.Source switch
    {
        InputSource.Simulated => new SimulatedFrameTransport(options.SimulatedRateHz),
        _ => new VendorUsbTransport(),
    };
}
