using System.Diagnostics;

namespace TabletAb.Core.Protocol;

/// <summary>
/// Monotonic host clock used to timestamp parsed frames (the analogue of the
/// browser's <c>performance.now()</c>).
/// </summary>
public static class MonotonicClock
{
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    public static double NowMs() => Stopwatch.GetTimestamp() * TicksToMs;
}
