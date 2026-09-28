namespace TabletAb.Core.Protocol;

/// <summary>Helpers for the wrapping 32-bit device microsecond timestamp.</summary>
public static class DeviceTime
{
    /// <summary>
    /// Signed 32-bit difference <c>b - a</c>, honouring the ~59.6 s wrap.
    /// Matches the web <c>deviceDeltaUs</c> (<c>(b - a) | 0</c>).
    /// </summary>
    public static int DeltaUs(uint a, uint b) => unchecked((int)(b - a));
}
