using TabletAb.Core.Protocol;
using TabletAb.Core.Statistics;

namespace TabletAb.Core.Tests;

public class LiveStatisticsTests
{
    private static Frame MakeFrame(
        uint deviceTimeUs = 0,
        double hostTimeMs = 0,
        int xPeak = 0,
        int yPeak = 0,
        int flags = 0,
        bool reacquired = false,
        uint scanUs = 0,
        uint buildUs = 0,
        uint sendUs = 0,
        int frequency = 9,
        int adcClock = 2,
        bool followPeak = true) => new()
        {
            Version = ProtocolVersion.V6,
            DeviceTimeUs = deviceTimeUs,
            HostTimeMs = hostTimeMs,
            XPeak = xPeak,
            YPeak = yPeak,
            Flags = flags
                    | (reacquired ? FrameFlags.Reacquired : 0)
                    | (followPeak ? FrameFlags.FollowPeak : 0),
            Backend = Backend.Hardware,
            ScanUs = scanUs,
            BuildUs = buildUs,
            SendUs = sendUs,
            Frequency = frequency,
            AdcClock = adcClock,
            X = new ushort[ProtocolConstants.Nx],
            Y = new ushort[ProtocolConstants.Ny],
        };

    [Fact]
    public void Add_TracksFrameCountAndReacquires()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame());
        stats.Add(MakeFrame(reacquired: true));
        stats.Add(MakeFrame());

        Assert.Equal(3, stats.FrameCount);
        Assert.Equal(1, stats.Reacquires);
    }

    [Fact]
    public void FramePeriod_AndScanRate_FromDeviceTimestamps()
    {
        var stats = new LiveStatistics();
        for (uint i = 0; i < 11; i++)
            stats.Add(MakeFrame(deviceTimeUs: i * 1000));

        Assert.Equal(1000.0, stats.FramePeriodUs(), 6);
        Assert.Equal(1000.0, stats.ScanRateHz(), 6);
        Assert.Equal(1000.0, stats.ScanPeriodMinUs(), 6);
        Assert.Equal(1000.0, stats.ScanPeriodMaxUs(), 6);
    }

    [Fact]
    public void ScanJitter_IsSampleStdDevOfPeriods()
    {
        var stats = new LiveStatistics();

        // Deltas alternate 900 / 1100 -> mean 1000, 5 of each over 10 periods.
        uint t = 0;
        stats.Add(MakeFrame(deviceTimeUs: t));
        for (int i = 0; i < 10; i++)
        {
            t += (uint)(i % 2 == 0 ? 900 : 1100);
            stats.Add(MakeFrame(deviceTimeUs: t));
        }

        Assert.Equal(1000.0, stats.FramePeriodUs(), 6);
        Assert.Equal(105.409, stats.ScanJitterUs(), 2);
    }

    [Fact]
    public void DevicePeriod_HandlesTimestampWrap()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame(deviceTimeUs: 0xFFFF_FF00));
        stats.Add(MakeFrame(deviceTimeUs: 0x0000_0100)); // +512 across the wrap

        Assert.Equal(512.0, stats.FramePeriodUs(), 6);
    }

    [Fact]
    public void DevicePeriod_IgnoresNonPositiveAndHugeDeltas()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame(deviceTimeUs: 1000));
        stats.Add(MakeFrame(deviceTimeUs: 1000));          // zero delta: ignored
        stats.Add(MakeFrame(deviceTimeUs: 2000));          // +1000 valid
        stats.Add(MakeFrame(deviceTimeUs: 5_000_000));     // > 1 s: ignored
        stats.Add(MakeFrame(deviceTimeUs: 5_001_000));     // +1000 valid

        Assert.Equal(1000.0, stats.FramePeriodUs(), 6);
    }

    [Fact]
    public void FlipRate_TracksPeakCoilChanges()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame(xPeak: 1, yPeak: 5));
        stats.Add(MakeFrame(xPeak: 2, yPeak: 5));
        stats.Add(MakeFrame(xPeak: 1, yPeak: 5));
        stats.Add(MakeFrame(xPeak: 2, yPeak: 5));

        // 3 changes over 3 intervals.
        Assert.Equal(1.0, stats.XFlipRate(), 6);
        Assert.Equal(0.0, stats.YFlipRate(), 6);
    }

    [Fact]
    public void SingleFrame_HasZeroFlipRate()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame(xPeak: 7));

        Assert.Equal(0.0, stats.XFlipRate(), 6);
        Assert.Equal(0.0, stats.YFlipRate(), 6);
    }

    [Fact]
    public void Fps_UsesHostArrivalWindow()
    {
        var stats = new LiveStatistics();
        for (int i = 0; i < 6; i++)
            stats.Add(MakeFrame(hostTimeMs: i * 10));

        // 5 intervals over 50 ms -> 100 fps.
        Assert.Equal(100.0, stats.Fps(), 6);
    }

    [Fact]
    public void ScanBuildSend_TrackLastAndMean()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame(scanUs: 100, buildUs: 40, sendUs: 10));
        stats.Add(MakeFrame(scanUs: 200, buildUs: 60, sendUs: 30));

        Assert.Equal(200.0, stats.ScanTimeUs(), 6);
        Assert.Equal(150.0, stats.ScanTimeMeanUs(), 6);
        Assert.Equal(60.0, stats.BuildTimeUs(), 6);
        Assert.Equal(50.0, stats.BuildTimeMeanUs(), 6);
        Assert.Equal(30.0, stats.SendTimeUs(), 6);
        Assert.Equal(20.0, stats.SendTimeMeanUs(), 6);
    }

    [Fact]
    public void Snapshot_DefaultsBeforeAnyFrame()
    {
        var stats = new LiveStatistics();
        LiveStatsSnapshot snapshot = stats.Snapshot(frames: 0, errors: 0, bytes: 0);

        Assert.Equal(0.0, snapshot.Fps, 6);
        Assert.Equal(9, snapshot.Frequency);
        Assert.Equal(2, snapshot.AdcClock);
        Assert.True(snapshot.FollowPeak);
        Assert.False(snapshot.Pen);
        Assert.Equal(0, snapshot.XMax);
        Assert.Equal(0, snapshot.YMax);
    }

    [Fact]
    public void Snapshot_ReflectsLatestFrame()
    {
        var stats = new LiveStatistics();
        Frame frame = MakeFrame(flags: 0x01, frequency: 6, adcClock: 1, followPeak: false);
        frame.X[3] = 1234;
        frame.Y[5] = 999;
        stats.Add(frame);

        LiveStatsSnapshot snapshot = stats.Snapshot(frames: 10, errors: 2, bytes: 1680);

        Assert.Equal(10, snapshot.Frames);
        Assert.Equal(2, snapshot.Errors);
        Assert.Equal(1680, snapshot.Bytes);
        Assert.Equal(6, snapshot.Frequency);
        Assert.Equal(1, snapshot.AdcClock);
        Assert.False(snapshot.FollowPeak);
        Assert.True(snapshot.Pen);
        Assert.Equal(1234, snapshot.XMax);
        Assert.Equal(999, snapshot.YMax);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var stats = new LiveStatistics();
        stats.Add(MakeFrame(deviceTimeUs: 0, hostTimeMs: 0, reacquired: true));
        stats.Add(MakeFrame(deviceTimeUs: 1000, hostTimeMs: 10));

        stats.Reset();

        Assert.Null(stats.Latest);
        Assert.Equal(0, stats.FrameCount);
        Assert.Equal(0, stats.Reacquires);
        Assert.Equal(0.0, stats.Fps(), 6);
        Assert.Equal(0.0, stats.FramePeriodUs(), 6);
        Assert.Equal(0.0, stats.XFlipRate(), 6);
    }
}
