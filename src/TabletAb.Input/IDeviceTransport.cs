using TabletAb.Core;

namespace TabletAb.Input;

/// <summary>
/// A source of tablet reports / cursor points.
///
/// Implemented in later phases by the vendor USB (libusb) reader, the HID /
/// OpenTabletDriver reader, and the external-pointer sampler. Kept deliberately
/// small here so the app can already depend on the seam.
/// </summary>
public interface IDeviceTransport : IDisposable
{
    /// <summary>Which input path this transport represents.</summary>
    DeviceKind Kind { get; }

    /// <summary>Human-readable device / backend name for the UI.</summary>
    string DisplayName { get; }

    /// <summary>True once <see cref="Open"/> has succeeded and before <see cref="Close"/>.</summary>
    bool IsOpen { get; }

    /// <summary>Acquire the device / start sampling.</summary>
    void Open();

    /// <summary>Release the device / stop sampling. Safe to call when not open.</summary>
    void Close();
}
