namespace TabletAb.Core.Statistics;

/// <summary>
/// A point-in-time snapshot of the rolling live statistics, mirroring the web
/// <c>LiveStats</c> interface. Transport counters (<see cref="Frames"/>,
/// <see cref="Errors"/>, <see cref="Bytes"/>) are supplied by the caller.
/// </summary>
public sealed record LiveStatsSnapshot
{
    public double Fps { get; init; }
    public long Frames { get; init; }
    public long Errors { get; init; }
    public long Bytes { get; init; }

    public int Frequency { get; init; }

    /// <summary>Mean device scan period (microseconds).</summary>
    public double FramePeriodUs { get; init; }

    /// <summary>True acquisition rate: 1e6 / <see cref="FramePeriodUs"/>.</summary>
    public double ScanRateHz { get; init; }

    public double ScanJitterUs { get; init; }
    public double ScanPeriodMinUs { get; init; }
    public double ScanPeriodMaxUs { get; init; }

    public double ScanTimeUs { get; init; }
    public double ScanTimeMeanUs { get; init; }
    public double BuildTimeUs { get; init; }
    public double BuildTimeMeanUs { get; init; }
    public double SendTimeUs { get; init; }
    public double SendTimeMeanUs { get; init; }

    public int XMax { get; init; }
    public int YMax { get; init; }
    public int XPeak { get; init; }
    public int YPeak { get; init; }
    public bool Pen { get; init; }

    public int Mode { get; init; }
    public int Burst { get; init; }
    public int AdcSamples { get; init; }
    public int AdcClock { get; init; }

    public bool Windowed { get; init; }
    public bool VendorEstimator { get; init; }
    public bool SaturateRetry { get; init; }
    public bool FollowPeak { get; init; }
    public bool Sticky { get; init; }

    public long Reacquires { get; init; }
    public double XFlipRate { get; init; }
    public double YFlipRate { get; init; }
}
