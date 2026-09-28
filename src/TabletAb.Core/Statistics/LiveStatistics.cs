using TabletAb.Core.Protocol;

namespace TabletAb.Core.Statistics;

/// <summary>
/// Rolling statistics over the frame stream. Ported from the scalar portions of
/// <c>web/src/lib/sensor.ts</c> (<c>Sensor.push</c> plus the derived rates). The
/// per-coil history ring buffer and coil statistics are not here; they belong
/// with the amplitude visualizer.
/// </summary>
public sealed class LiveStatistics
{
    /// <summary>Host arrival timestamps kept for the FPS estimate.</summary>
    private const int ArrivalWindow = 90;

    /// <summary>Device-side samples kept for period / scan / build / send means.</summary>
    private const int PeriodWindow = 120;

    private readonly Queue<double> _arrival = new();
    private readonly Queue<double> _devicePeriods = new();
    private readonly Queue<double> _scanTimes = new();
    private readonly Queue<double> _buildTimes = new();
    private readonly Queue<double> _sendTimes = new();

    private uint _previousDeviceTime;
    private bool _hasPreviousFrame;

    private int _previousXPeak;
    private int _previousYPeak;
    private bool _hasPeakBaseline;
    private long _peakFrames;
    private long _xFlips;
    private long _yFlips;

    /// <summary>Most recent frame, or null before the first <see cref="Add"/>.</summary>
    public Frame? Latest { get; private set; }

    /// <summary>Frames added since construction or <see cref="Reset"/>.</summary>
    public long FrameCount { get; private set; }

    /// <summary>Frames flagged as full re-acquires since construction or <see cref="Reset"/>.</summary>
    public long Reacquires { get; private set; }

    public void Add(Frame frame)
    {
        Latest = frame;
        FrameCount++;

        if (frame.Reacquired)
            Reacquires++;

        if (_hasPeakBaseline)
        {
            if (frame.XPeak != _previousXPeak) _xFlips++;
            if (frame.YPeak != _previousYPeak) _yFlips++;
        }

        _previousXPeak = frame.XPeak;
        _previousYPeak = frame.YPeak;
        _hasPeakBaseline = true;
        _peakFrames++;

        if (_hasPreviousFrame)
        {
            int delta = DeviceTime.DeltaUs(_previousDeviceTime, frame.DeviceTimeUs);
            if (delta > 0 && delta < 1_000_000)
                Push(_devicePeriods, delta, PeriodWindow);
        }

        _previousDeviceTime = frame.DeviceTimeUs;
        _hasPreviousFrame = true;

        if (frame.ScanUs > 0)
            Push(_scanTimes, frame.ScanUs, PeriodWindow);
        if (frame.BuildUs > 0)
            Push(_buildTimes, frame.BuildUs, PeriodWindow);
        if (frame.SendUs > 0)
            Push(_sendTimes, frame.SendUs, PeriodWindow);

        Push(_arrival, frame.HostTimeMs, ArrivalWindow);
    }

    /// <summary>Host-side arrival rate (frames/second) over the recent window.</summary>
    public double Fps()
    {
        if (_arrival.Count < 2)
            return 0;

        double[] a = _arrival.ToArray();
        double seconds = (a[^1] - a[0]) / 1000.0;
        return seconds > 0 ? (a.Length - 1) / seconds : 0;
    }

    public double FramePeriodUs() => Mean(_devicePeriods);

    public double ScanRateHz()
    {
        double period = FramePeriodUs();
        return period > 0 ? 1_000_000.0 / period : 0;
    }

    public double ScanJitterUs()
    {
        int n = _devicePeriods.Count;
        if (n < 2)
            return 0;

        double mean = Mean(_devicePeriods);
        double acc = 0;
        foreach (double d in _devicePeriods)
            acc += (d - mean) * (d - mean);

        return Math.Sqrt(acc / (n - 1));
    }

    public double ScanPeriodMinUs() => _devicePeriods.Count > 0 ? _devicePeriods.Min() : 0;

    public double ScanPeriodMaxUs() => _devicePeriods.Count > 0 ? _devicePeriods.Max() : 0;

    public double ScanTimeUs() => Last(_scanTimes);

    public double ScanTimeMeanUs() => Mean(_scanTimes);

    public double BuildTimeUs() => Last(_buildTimes);

    public double BuildTimeMeanUs() => Mean(_buildTimes);

    public double SendTimeUs() => Last(_sendTimes);

    public double SendTimeMeanUs() => Mean(_sendTimes);

    /// <summary>Fraction of recent frames whose X peak coil changed (lower = steadier).</summary>
    public double XFlipRate() => _peakFrames > 1 ? (double)_xFlips / (_peakFrames - 1) : 0;

    /// <summary>Fraction of recent frames whose Y peak coil changed.</summary>
    public double YFlipRate() => _peakFrames > 1 ? (double)_yFlips / (_peakFrames - 1) : 0;

    /// <summary>Build a snapshot. <paramref name="frames"/>/<paramref name="errors"/>/<paramref name="bytes"/> are transport counters.</summary>
    public LiveStatsSnapshot Snapshot(long frames, long errors, long bytes)
    {
        Frame? f = Latest;

        int xMax = 0;
        int yMax = 0;
        if (f is not null)
        {
            foreach (ushort v in f.X)
                if (v > xMax) xMax = v;
            foreach (ushort v in f.Y)
                if (v > yMax) yMax = v;
        }

        return new LiveStatsSnapshot
        {
            Fps = Fps(),
            Frames = frames,
            Errors = errors,
            Bytes = bytes,
            Frequency = f?.Frequency ?? 9,
            FramePeriodUs = FramePeriodUs(),
            ScanRateHz = ScanRateHz(),
            ScanJitterUs = ScanJitterUs(),
            ScanPeriodMinUs = ScanPeriodMinUs(),
            ScanPeriodMaxUs = ScanPeriodMaxUs(),
            ScanTimeUs = ScanTimeUs(),
            ScanTimeMeanUs = ScanTimeMeanUs(),
            BuildTimeUs = BuildTimeUs(),
            BuildTimeMeanUs = BuildTimeMeanUs(),
            SendTimeUs = SendTimeUs(),
            SendTimeMeanUs = SendTimeMeanUs(),
            XMax = xMax,
            YMax = yMax,
            XPeak = f?.XPeak ?? 0,
            YPeak = f?.YPeak ?? 0,
            Pen = f?.PenPresent ?? false,
            Mode = f?.Mode ?? 0,
            Burst = f?.Burst ?? 0,
            AdcSamples = f?.AdcSamples ?? 0,
            AdcClock = f?.AdcClock ?? 2,
            Windowed = f?.Windowed ?? false,
            VendorEstimator = f?.VendorEstimator ?? false,
            SaturateRetry = f?.SaturateRetry ?? false,
            FollowPeak = f?.FollowPeak ?? true,
            Sticky = f?.Sticky ?? false,
            Reacquires = Reacquires,
            XFlipRate = XFlipRate(),
            YFlipRate = YFlipRate(),
        };
    }

    /// <summary>Clear all accumulated state.</summary>
    public void Reset()
    {
        Latest = null;
        FrameCount = 0;
        Reacquires = 0;

        _arrival.Clear();
        _devicePeriods.Clear();
        _scanTimes.Clear();
        _buildTimes.Clear();
        _sendTimes.Clear();

        _hasPreviousFrame = false;
        _hasPeakBaseline = false;
        _peakFrames = 0;
        _xFlips = 0;
        _yFlips = 0;
        _previousXPeak = 0;
        _previousYPeak = 0;
    }

    private static void Push(Queue<double> queue, double value, int max)
    {
        queue.Enqueue(value);
        if (queue.Count > max)
            queue.Dequeue();
    }

    private static double Mean(Queue<double> queue)
    {
        if (queue.Count == 0)
            return 0;

        double sum = 0;
        foreach (double v in queue)
            sum += v;

        return sum / queue.Count;
    }

    private static double Last(Queue<double> queue) => queue.Count > 0 ? queue.Last() : 0;
}
