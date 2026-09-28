using System.Diagnostics;

namespace TabletAb.App.Rendering;

/// <summary>
/// Tracks the frame period and optionally sleeps out the remainder of a target
/// frame budget. Mirrors the intent of osu!framework's frame limiter.
/// </summary>
public sealed class FrameLimiter
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;

    /// <summary>Seconds elapsed between the last two <see cref="Tick"/> calls.</summary>
    public double DeltaSeconds { get; private set; }

    /// <summary>Instantaneous frames per second derived from <see cref="DeltaSeconds"/>.</summary>
    public double Fps { get; private set; }

    /// <summary>Start the frame clock; call once before the loop.</summary>
    public void Start() => _lastFrame = _clock.Elapsed.TotalSeconds;

    /// <summary>Advance the frame clock and return the delta in seconds.</summary>
    public double Tick()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double delta = now - _lastFrame;
        _lastFrame = now;

        if (delta <= 0)
            delta = 1.0 / 1000.0;

        DeltaSeconds = delta;
        Fps = 1.0 / delta;
        return delta;
    }

    /// <summary>
    /// Sleep out the remainder of the frame budget for <paramref name="targetHz"/>.
    /// A target of zero (or less) means "no limit" and returns immediately.
    /// </summary>
    public void Sleep(double targetHz)
    {
        if (targetHz <= 0)
            return;

        double remaining = (1.0 / targetHz) - (_clock.Elapsed.TotalSeconds - _lastFrame);
        if (remaining > 0.0005)
            Thread.Sleep(TimeSpan.FromSeconds(remaining));
    }
}
