using TabletAb.Core.Input;

namespace TabletAb.Input;

/// <summary>Carries one cursor sample from a pointer source's thread.</summary>
/// <remarks>Handlers run on the source's thread; marshal before touching UI state.</remarks>
public sealed class PointerPointEventArgs : EventArgs
{
    public PointerPointEventArgs(PointerPoint point) => Point = point;

    public PointerPoint Point { get; }
}

/// <summary>
/// A cursor-point stream. Implemented by the debug-frame adapter, the
/// OpenTabletDriver release-HID source, and the external OS-pointer sampler.
/// </summary>
public interface IPointerSource : IDeviceTransport
{
    event EventHandler<PointerPointEventArgs>? PointReceived;

    long PointsReceived { get; }
}
