namespace TabletAb.Core.Protocol;

/// <summary>A selectable protocol value with its UI label.</summary>
/// <param name="Value">Wire value sent in the command / reported in the frame.</param>
/// <param name="Label">Human-readable label (matches the web UI wording).</param>
public readonly record struct ProtocolChoice(int Value, string Label);

/// <summary>
/// Dropdown catalogs shared with the web A/B UI. Ported from
/// <c>web/src/lib/protocol.ts</c> so the app and web tool offer identical
/// choices.
/// </summary>
public static class ProtocolCatalog
{
    /// <summary>Window re-centring strategies.</summary>
    public static readonly ProtocolChoice[] RecentreModes =
    [
        new(2, "de-entangled sticky (Schmitt)"),
        new(3, "deadband sticky (legacy)"),
        new(1, "follow peak + flat hold (gaps)"),
        new(0, "edge only (legacy, gaps)"),
    ];

    /// <summary>Coil measurement order.</summary>
    public static readonly ProtocolChoice[] ScanOrders =
    [
        new(0, "ascending (1→N)"),
        new(1, "descending (N→1)"),
        new(2, "outside-in interleaved"),
        new(3, "center-out interleaved"),
    ];

    /// <summary>Scan-order ramp-mitigation strategies (SET_RAMP).</summary>
    public static readonly ProtocolChoice[] RampModes =
    [
        new(0, "none (baseline)"),
        new(1, "carry-over (free-run)"),
        new(2, "prime per window"),
        new(3, "per-read steady state"),
        new(4, "bidirectional average"),
        new(5, "slope correction"),
        new(6, "targeted reverse (cheap)"),
        new(7, "frame-alternated average (free)"),
    ];

    /// <summary>Sub-pixel estimators.</summary>
    public static readonly ProtocolChoice[] Estimators =
    [
        new(0, "n-pt centroid (all window coils)"),
        new(1, "vendor (rational + cal)"),
        new(2, "gaomon (5pt + linear)"),
        new(3, "log-gaussian (3pt)"),
    ];

    /// <summary>Coarse re-acquire strides (0 = full 68-coil grid).</summary>
    public static readonly ProtocolChoice[] CoarseStrides =
    [
        new(0, "full grid (68)"),
        new(2, "stride 2 (~35)"),
        new(3, "stride 3 (~23)"),
        new(4, "stride 4 (~18)"),
        new(6, "stride 6 (~12)"),
    ];

    /// <summary>Window radii (coils per axis = 2R+1).</summary>
    public static readonly ProtocolChoice[] WindowRadii =
    [
        new(0, "full scan (68)"),
        new(1, "3 + 3 = 6 coils"),
        new(2, "5 + 5 = 10 coils"),
        new(3, "7 + 7 = 14 coils"),
        new(4, "9 + 9 = 18 coils"),
    ];

    /// <summary>ADC clock prescaler labels (APB2 = 72 MHz).</summary>
    public static readonly ProtocolChoice[] AdcClockLabels =
    [
        new(0, "APB2/2 · 36 MHz"),
        new(1, "APB2/4 · 18 MHz"),
        new(2, "APB2/6 · 12 MHz"),
        new(3, "APB2/8 · 9 MHz"),
    ];

    /// <summary>Per-axis window coil counts (supports even sizes). 0 = full 68-coil scan.</summary>
    public static readonly ProtocolChoice[] WindowCoils =
    [
        new(0, "0 (full)"),
        new(2, "2"),
        new(3, "3"),
        new(4, "4"),
        new(5, "5"),
        new(6, "6"),
        new(7, "7"),
        new(8, "8"),
    ];

    /// <summary>Acquisition pacing (SET_PACING, phase 4 firmware).</summary>
    public static readonly ProtocolChoice[] PacingModes =
    [
        new(0, "SOF-paced (1 per USB frame)"),
        new(1, "continuous (free-run)"),
        new(2, "auto (legacy ramp carry-over)"),
    ];
}
