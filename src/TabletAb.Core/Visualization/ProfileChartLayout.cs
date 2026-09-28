namespace TabletAb.Core.Visualization;

/// <summary>A single bar rectangle in chart-local coordinates.</summary>
public readonly record struct ChartBar(float X, float Y, float Width, float Height);

/// <summary>
/// Geometry and scaling for the amplitude profile chart, ported from
/// <c>web/src/components/ProfileChart.tsx</c>. Pure math with no graphics
/// dependency so it can be unit-tested and reused by the renderer.
///
/// Display window: 300 (baseline, bars sit flat below) to 3200 (top). No
/// per-frame auto-scaling and no averaging: each bar is a straight
/// sample-and-hold of that coil's raw count.
/// </summary>
public sealed class ProfileChartLayout
{
    public const float Lo = 300f;
    public const float Hi = 3200f;

    /// <summary>No peak is marked unless some coil reaches this value.</summary>
    public const float PeakMin = 810f;

    public const float PadLeft = 62f;
    public const float PadRight = 18f;
    public const float PadTop = 26f;
    public const float PadBottom = 34f;

    private ProfileChartLayout()
    {
    }

    public float Width { get; private init; }
    public float Height { get; private init; }
    public int BarCount { get; private init; }
    public int SlotCount { get; private init; }

    public float PlotWidth { get; private init; }
    public float PlotHeight { get; private init; }
    public float SlotWidth { get; private init; }
    public float Gap { get; private init; }
    public float BarWidth { get; private init; }
    public float GroupX { get; private init; }
    public float BaseY { get; private init; }

    /// <summary>True when index labels fit (web: bar width &gt; 15 px).</summary>
    public bool ShowIndexLabels => BarWidth > 15f;

    /// <summary>True when per-bar value labels fit (web: slot width &gt; 27 px).</summary>
    public bool ShowValueLabels => SlotWidth > 27f;

    /// <summary>
    /// Compute the layout for a plot area. <paramref name="slotCount"/> is the
    /// shared reference used for one common bar width (both axes pass NX), while
    /// <paramref name="barCount"/> is the number of coils actually drawn.
    /// </summary>
    public static ProfileChartLayout Compute(float width, float height, int barCount, int slotCount)
    {
        float plotWidth = Math.Max(0f, width - PadLeft - PadRight);
        float plotHeight = Math.Max(0f, height - PadTop - PadBottom);
        float slotWidth = slotCount > 0 ? plotWidth / slotCount : plotWidth;
        float gap = Math.Max(1f, slotWidth * 0.1f);
        float barWidth = slotWidth - gap;
        float groupWidth = slotWidth * barCount;
        float groupX = PadLeft + (plotWidth - groupWidth) / 2f;
        float baseY = PadTop + plotHeight;

        return new ProfileChartLayout
        {
            Width = width,
            Height = height,
            BarCount = barCount,
            SlotCount = slotCount,
            PlotWidth = plotWidth,
            PlotHeight = plotHeight,
            SlotWidth = slotWidth,
            Gap = gap,
            BarWidth = barWidth,
            GroupX = groupX,
            BaseY = baseY,
        };
    }

    /// <summary>Normalised bar height for a raw amplitude, clamped to 0..1.</summary>
    public float Fraction(float value) => Math.Clamp((value - Lo) / (Hi - Lo), 0f, 1f);

    /// <summary>
    /// Resolve which bar is the peak. The gold bar is the device-reported peak
    /// channel (1-based), shown only when some coil reaches
    /// <see cref="PeakMin"/>; otherwise -1.
    /// </summary>
    public int ResolvePeakIndex(ReadOnlySpan<ushort> values, int reportedPeak1Based)
    {
        ushort max = 0;
        foreach (ushort v in values)
        {
            if (v > max)
                max = v;
        }

        if (max < PeakMin)
            return -1;

        int peak = reportedPeak1Based - 1;
        return (peak >= 0 && peak < values.Length) ? peak : -1;
    }

    /// <summary>Rectangle for one bar (chart-local coordinates).</summary>
    public ChartBar GetBar(int index, float value)
    {
        float barHeight = Math.Max(1f, Fraction(value) * PlotHeight);
        float x = GroupX + index * SlotWidth + Gap / 2f;
        float y = BaseY - barHeight;
        return new ChartBar(x, y, BarWidth, barHeight);
    }
}
