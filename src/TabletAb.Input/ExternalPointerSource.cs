using TabletAb.Core;
using TabletAb.Core.Input;
using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>
/// Push-based external-pointer source. osu!lazer's external tablet mode does
/// not read the tablet; an external driver moves the OS cursor and the game
/// reads the OS/SDL mouse. The app samples the window mouse (per motion event,
/// or per frame as a fallback) and pushes client coordinates here.
///
/// Note: native Wayland cannot read the global cursor; run with X11/XWayland
/// (SDL_VIDEODRIVER=x11) for the same behaviour as lazer.
/// </summary>
public sealed class ExternalPointerSource : IPointerSource
{
    private volatile bool _open;
    private long _points;

    public DeviceKind Kind => DeviceKind.ExternalPointer;

    public string DisplayName => "external pointer (OS cursor)";

    public bool IsOpen => _open;

    public long PointsReceived => Interlocked.Read(ref _points);

    public event EventHandler<PointerPointEventArgs>? PointReceived;

    public void Open() => _open = true;

    public void Close() => _open = false;

    public void Dispose() => _open = false;

    /// <summary>
    /// Push a cursor sample in window client pixels. No-op unless open.
    /// </summary>
    public void Push(double clientX, double clientY, double clientWidth, double clientHeight, float pressure = 1f)
    {
        if (!_open || clientWidth <= 0 || clientHeight <= 0)
            return;

        double nx = Math.Clamp(clientX / clientWidth, 0.0, 1.0);
        double ny = Math.Clamp(clientY / clientHeight, 0.0, 1.0);

        var point = new PointerPoint(
            nx, ny,
            (float)clientX, (float)clientY,
            (float)clientX, (float)clientY,
            pressure,
            MonotonicClock.NowMs());

        Interlocked.Increment(ref _points);
        PointReceived?.Invoke(this, new PointerPointEventArgs(point));
    }
}
