using TabletAb.Core;
using TabletAb.Core.Input;
using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>
/// Adapts the debug firmware's frame stream into cursor points. It is passive:
/// it does not own the underlying <see cref="IFrameSource"/> (the app connects
/// and disconnects that), it just re-emits the device sub-pixel position
/// (1/256 channel) as a normalised point, using the same validity gate as the
/// web Position panel.
/// </summary>
public sealed class DebugFramePointerSource : IPointerSource
{
    private readonly IFrameSource _frames;
    private long _points;

    public DebugFramePointerSource(IFrameSource frames)
    {
        _frames = frames;
        _frames.FrameReceived += OnFrame;
    }

    public DeviceKind Kind => _frames.Kind;

    public string DisplayName => $"debug frames ({_frames.DisplayName})";

    public bool IsOpen => _frames.IsOpen;

    public long PointsReceived => Interlocked.Read(ref _points);

    public event EventHandler<PointerPointEventArgs>? PointReceived;

    public void Open()
    {
        // The underlying frame source is managed by the app.
    }

    public void Close()
    {
        // The underlying frame source is managed by the app.
    }

    public void Dispose() => _frames.FrameReceived -= OnFrame;

    private void OnFrame(object? sender, FrameReceivedEventArgs e)
    {
        Frame frame = e.Frame;

        ushort amplitude = 0;
        foreach (ushort v in frame.X)
        {
            if (v > amplitude)
                amplitude = v;
        }
        foreach (ushort v in frame.Y)
        {
            if (v > amplitude)
                amplitude = v;
        }

        bool valid = frame.XPos > 0 && frame.YPos > 0 && frame.XPeak > 0 && frame.YPeak > 0 && amplitude > 20;
        if (!valid)
            return;

        float rawX = frame.XPos / 256f;
        float rawY = frame.YPos / 256f;

        // Web normalisation: (xPos/256 - 1) / (N - 1), clamped to 0..1.
        double nx = Math.Clamp((rawX - 1.0) / Math.Max(1, frame.X.Length - 1), 0.0, 1.0);
        double ny = Math.Clamp((rawY - 1.0) / Math.Max(1, frame.Y.Length - 1), 0.0, 1.0);

        var point = new PointerPoint(nx, ny, rawX, rawY, rawX, rawY, 0f, frame.HostTimeMs);

        Interlocked.Increment(ref _points);
        PointReceived?.Invoke(this, new PointerPointEventArgs(point));
    }
}
