using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>Settings for <see cref="VendorUsbTransport"/>.</summary>
public sealed class VendorUsbOptions
{
    /// <summary>HS611 debug firmware vendor id (also used by the REL_DEBUG build).</summary>
    public int VendorId { get; init; } = 0x256C;

    /// <summary>HS611 debug firmware product id (vendor-class, bulk IN/OUT).</summary>
    public int ProductId { get; init; } = 0x6111;

    /// <summary>Bulk IN transfer timeout (milliseconds).</summary>
    public int ReadTimeoutMs { get; init; } = 500;

    /// <summary>Bulk OUT write timeout (milliseconds).</summary>
    public int WriteTimeoutMs { get; init; } = 500;

    /// <summary>
    /// Maximum bulk IN transfer size. The reader requests this many bytes and
    /// parses whatever length actually arrives, so it must cover both the v6
    /// (168 B) and self-describing v7 (up to <see cref="ProtocolConstants.MaxFrameLen"/> B) frames.
    /// </summary>
    public int FrameLength { get; init; } = ProtocolConstants.MaxFrameLen;

    /// <summary>Detach a bound kernel driver before claiming (no-op if none).</summary>
    public bool AutoDetachKernelDriver { get; init; } = true;
}
