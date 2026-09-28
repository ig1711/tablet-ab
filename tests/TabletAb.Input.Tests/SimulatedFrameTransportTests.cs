using TabletAb.Core.Protocol;
using TabletAb.Core.Statistics;

namespace TabletAb.Input.Tests;

public class SimulatedFrameTransportTests
{
    private const double TestRateHz = 1000.0;
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Open_StreamsWellFormedFrames_AndClosesCleanly()
    {
        const int target = 50;
        const double baseline = 690.0;

        var frames = new List<Frame>(target + 8);
        var received = new ManualResetEventSlim(false);
        var transport = new SimulatedFrameTransport(TestRateHz);

        int observed = 0;
        long framesAtStop = 0;
        long bytesAtStop = 0;
        bool openWhileRunning;
        bool openAfterClose;

        transport.FrameReceived += (_, e) =>
        {
            lock (frames)
            {
                if (frames.Count < target + 8)
                    frames.Add(e.Frame);
                if (++observed >= target)
                    received.Set();
            }
        };

        try
        {
            transport.Open();
            openWhileRunning = transport.IsOpen;

            Assert.True(received.Wait(WaitTimeout),
                $"expected at least {target} frames within {WaitTimeout}");

            framesAtStop = transport.FramesReceived;
            bytesAtStop = transport.BytesReceived;

            Assert.True(openWhileRunning, "IsOpen should be true immediately after Open()");
            Assert.True(transport.IsOpen, "IsOpen should remain true while streaming");
            Assert.True(framesAtStop > 0, "FramesReceived should advance");
            Assert.True(bytesAtStop > 0, "BytesReceived should advance");

            // A 64-byte command buffer must be accepted without throwing.
            transport.SendCommand(new byte[ProtocolConstants.CommandLen]);
        }
        finally
        {
            transport.Close();
            openAfterClose = transport.IsOpen;
            transport.Dispose();
        }

        Assert.False(openAfterClose, "IsOpen should be false after Close()");

        Frame[] snapshot;
        lock (frames)
        {
            snapshot = frames.ToArray();
        }

        Assert.NotEmpty(snapshot);

        foreach (Frame frame in snapshot)
        {
            Assert.Equal(ProtocolVersion.Current, frame.Version);
            Assert.Equal(ProtocolConstants.Nx, frame.X.Length);
            Assert.Equal(ProtocolConstants.Ny, frame.Y.Length);
            Assert.InRange(frame.XPeak, 1, ProtocolConstants.Nx);
            Assert.InRange(frame.YPeak, 1, ProtocolConstants.Ny);
            Assert.True(frame.X.Max() > baseline,
                "X peak amplitude should exceed the simulator baseline");
            Assert.True(frame.Y.Max() > baseline,
                "Y peak amplitude should exceed the simulator baseline");
        }
    }

    [Fact]
    public void Open_WithCustomGeometry_EmitsMatchingV7Frames()
    {
        const int target = 5;
        const int nx = 30;
        const int ny = 19;

        var received = new ManualResetEventSlim(false);
        var transport = new SimulatedFrameTransport(TestRateHz, nx, ny);
        var frames = new List<Frame>();

        transport.FrameReceived += (_, e) =>
        {
            lock (frames)
            {
                frames.Add(e.Frame);
                if (frames.Count >= target)
                    received.Set();
            }
        };

        try
        {
            transport.Open();
            Assert.True(received.Wait(WaitTimeout), $"expected {target} frames within {WaitTimeout}");
        }
        finally
        {
            transport.Close();
            transport.Dispose();
        }

        Frame[] snapshot;
        lock (frames)
            snapshot = frames.ToArray();

        Assert.NotEmpty(snapshot);
        Assert.All(snapshot, frame =>
        {
            Assert.Equal(ProtocolVersion.V7, frame.Version);
            Assert.Equal(nx, frame.X.Length);
            Assert.Equal(ny, frame.Y.Length);
            Assert.InRange(frame.XPeak, 1, nx);
            Assert.InRange(frame.YPeak, 1, ny);
        });

        long frameLength = 36L + 2L * (nx + ny);
        Assert.Equal(0, transport.BytesReceived % frameLength);
    }

    [Fact]
    public void SimulatedFrames_DriveLiveStatisticsToConfiguredRate()
    {
        const int target = 60;

        var stats = new LiveStatistics();
        var received = new ManualResetEventSlim(false);
        var transport = new SimulatedFrameTransport(TestRateHz);

        int observed = 0;

        transport.FrameReceived += (_, e) =>
        {
            stats.Add(e.Frame);
            if (Interlocked.Increment(ref observed) >= target)
                received.Set();
        };

        try
        {
            transport.Open();
            Assert.True(received.Wait(WaitTimeout),
                $"expected at least {target} frames within {WaitTimeout}");
        }
        finally
        {
            transport.Close();
            transport.Dispose();
        }

        double rate = stats.ScanRateHz();
        Assert.True(rate > TestRateHz * 0.8 && rate < TestRateHz * 1.2,
            $"scan rate {rate:0.##} Hz should be within 20% of {TestRateHz} Hz");
        Assert.True(stats.Fps() > 0, "Fps should be positive after frames have arrived");
    }
}
