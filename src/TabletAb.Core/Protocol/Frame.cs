namespace TabletAb.Core.Protocol;

/// <summary>
/// One decoded acquisition frame. For protocol v6 the fields map directly to the
/// layout in <see cref="ProtocolConstants"/> / <c>hs611-min-ab/src/protocol.h</c>.
/// Frames from the legacy firmware (v1..v5) are mapped onto the same shape so the
/// rest of the app does not branch on version.
/// </summary>
public sealed class Frame
{
    public int Version { get; init; }

    public int Seq { get; init; }

    /// <summary>Raw v6 flags word (<see cref="FrameFlags"/>) or the legacy v5 flags byte.</summary>
    public int Flags { get; init; }

    /// <summary>Acquisition backend: <see cref="Backend"/>.</summary>
    public int Backend { get; init; }

    /// <summary>Backend alias kept for the UI/simulator (v6 = <see cref="Backend"/>).</summary>
    public int Mode => Backend;

    /// <summary>Active sub-pixel estimator: <see cref="Protocol.Estimator"/>.</summary>
    public int Estimator { get; init; }

    /// <summary>Drive frequency index (1..12).</summary>
    public int Frequency { get; init; }

    /// <summary>Read burst periods.</summary>
    public int Burst { get; init; }

    /// <summary>ADC samples per channel (1..7).</summary>
    public int AdcSamples { get; init; }

    /// <summary>ADC clock prescaler select (0..3).</summary>
    public int AdcClock { get; init; }

    /// <summary>Axis-B (X) tracking window coils; 0 = full scan.</summary>
    public int WindowX { get; init; }

    /// <summary>Axis-A (Y) tracking window coils; 0 = full scan.</summary>
    public int WindowY { get; init; }

    /// <summary>Active ramp-mitigation mode: <see cref="RampMode"/>.</summary>
    public int RampMode { get; init; }

    /// <summary>Re-centre mode: <see cref="Recenter"/>.</summary>
    public int RecenterMode { get; init; }

    /// <summary>Scan order: <see cref="ScanOrder"/>.</summary>
    public int ScanOrder { get; init; }

    /// <summary>Discarded warm-up reads per axis (0..8).</summary>
    public int Warmup { get; init; }

    /// <summary>1-based X peak loop, 0 when no peak was found.</summary>
    public int XPeak { get; init; }

    /// <summary>1-based Y peak loop, 0 when no peak was found.</summary>
    public int YPeak { get; init; }

    /// <summary>Device microsecond timestamp (wraps ~59.6 s).</summary>
    public uint DeviceTimeUs { get; init; }

    /// <summary>Device-side duration of the pure coil scan (microseconds).</summary>
    public uint ScanUs { get; init; }

    /// <summary>Device-computed sub-pixel X position, 1/256 channel (0 when unavailable).</summary>
    public int XPos { get; init; }

    /// <summary>Device-computed sub-pixel Y position, 1/256 channel (0 when unavailable).</summary>
    public int YPos { get; init; }

    /// <summary>Legacy device frame-build duration (v5 only; 0 in v6).</summary>
    public uint BuildUs { get; init; }

    /// <summary>Legacy last USB transfer duration (v5 only; 0 in v6).</summary>
    public uint SendUs { get; init; }

    /// <summary>Raw legacy v5 options byte (synthetic for v6, see parser).</summary>
    public int Options { get; init; }

    /// <summary>Host wall-clock time at which the frame was parsed (ms).</summary>
    public double HostTimeMs { get; init; }

    /// <summary>X amplitudes: <see cref="ProtocolConstants.Nx"/> u16 samples.</summary>
    public ushort[] X { get; init; } = [];

    /// <summary>Y amplitudes: <see cref="ProtocolConstants.Ny"/> u16 samples.</summary>
    public ushort[] Y { get; init; } = [];

    // ---- derived flags ----------------------------------------------------

    /// <summary>True when the frame flags report pen coupling.</summary>
    public bool PenPresent => HasFlag(FrameFlags.Pen, legacyBit: 0x01);

    /// <summary>True when a windowed scan is active.</summary>
    public bool Windowed => WindowX != 0 || HasFlag(FrameFlags.Windowed, legacyBit: 0x08, legacyOptions: true);

    public bool Reacquired => HasFlag(FrameFlags.Reacquired, legacyBit: 0x10, legacyOptions: true);

    public bool SaturateRetry => HasFlag(FrameFlags.SaturateRetry, legacyBit: 0x40, legacyOptions: true);

    public bool FollowPeak => HasFlag(FrameFlags.FollowPeak, legacyBit: 0x80, legacyOptions: true);

    public bool VendorEstimator => Estimator == Protocol.Estimator.Vendor || (Version < ProtocolVersion.V6 && OptionsBit(0x20));

    /// <summary>True for the de-entangled / deadband sticky re-centre modes.</summary>
    public bool Sticky => RecenterMode is Protocol.Recenter.Sticky or Protocol.Recenter.Deadband
                          || HasFlag(FrameFlags.Sticky, legacyBit: 0x80);

    public bool ScanDescending => ScanOrder == Protocol.ScanOrder.Descending
                                  || HasFlag(FrameFlags.Descending, legacyBit: 0x02);

    /// <summary>Legacy flat-top hold flag (v5 only).</summary>
    public bool FlatHold => HasFlag(FrameFlags.Windowed, legacyBit: 0x40) && WindowX == 0;

    /// <summary>True when acquisition is free-running (carry-over).</summary>
    public bool CarryOver => (Flags & FrameFlags.CarryOver) != 0;

    private bool HasFlag(int v6Flag, int legacyBit, bool legacyOptions = false)
    {
        if (Version >= ProtocolVersion.V6)
            return (Flags & v6Flag) != 0;
        return legacyOptions ? OptionsBit(legacyBit) : (Flags & legacyBit) != 0;
    }

    private bool OptionsBit(int mask) => (Options & mask) != 0;
}
