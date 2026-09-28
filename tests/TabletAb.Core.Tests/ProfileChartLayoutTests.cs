using TabletAb.Core.Visualization;

namespace TabletAb.Core.Tests;

public class ProfileChartLayoutTests
{
    [Fact]
    public void Constants_HaveDocumentedValues()
    {
        Assert.Equal(300f, ProfileChartLayout.Lo);
        Assert.Equal(3200f, ProfileChartLayout.Hi);
        Assert.Equal(810f, ProfileChartLayout.PeakMin);
        Assert.Equal(62f, ProfileChartLayout.PadLeft);
        Assert.Equal(18f, ProfileChartLayout.PadRight);
        Assert.Equal(26f, ProfileChartLayout.PadTop);
        Assert.Equal(34f, ProfileChartLayout.PadBottom);
    }

    [Fact]
    public void Compute_Geometry_ForFullWidthGroup()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);

        Assert.Equal(420d, layout.PlotWidth, 4);
        Assert.Equal(190d, layout.PlotHeight, 4);
        Assert.Equal(420d / 41d, layout.SlotWidth, 4);

        double slotWidth = 420d / 41d;
        double gap = Math.Max(1d, slotWidth * 0.1d);
        Assert.Equal(gap, layout.Gap, 4);
        Assert.Equal(slotWidth - gap, layout.BarWidth, 4);

        Assert.Equal(62d, layout.GroupX, 4);
        Assert.Equal(216d, layout.BaseY, 4);
    }

    [Fact]
    public void Compute_Geometry_CentersSmallerGroup()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 27, 41);

        double slotWidth = 420d / 41d;
        double expectedGroupX = ProfileChartLayout.PadLeft + (420d - 27d * slotWidth) / 2d;

        Assert.True(layout.GroupX > ProfileChartLayout.PadLeft);
        Assert.Equal(expectedGroupX, layout.GroupX, 4);
    }

    [Fact]
    public void Compute_ClampsTooSmallDimensionsToZero()
    {
        ProfileChartLayout narrow = ProfileChartLayout.Compute(40f, 250f, 41, 41);
        Assert.Equal(0d, narrow.PlotWidth, 4);
        Assert.Equal(190d, narrow.PlotHeight, 4);

        ProfileChartLayout shortPlot = ProfileChartLayout.Compute(500f, 40f, 41, 41);
        Assert.Equal(420d, shortPlot.PlotWidth, 4);
        Assert.Equal(0d, shortPlot.PlotHeight, 4);

        ProfileChartLayout degenerate = ProfileChartLayout.Compute(0f, 0f, 41, 41);
        Assert.Equal(0d, degenerate.PlotWidth, 4);
        Assert.Equal(0d, degenerate.PlotHeight, 4);
    }

    [Fact]
    public void Fraction_ClampsAndScalesLinearly()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);

        Assert.Equal(0d, layout.Fraction(300f), 4);
        Assert.Equal(1d, layout.Fraction(3200f), 4);
        Assert.Equal(0.5d, layout.Fraction(1750f), 4);

        Assert.Equal(0d, layout.Fraction(0f), 4);
        Assert.Equal(0d, layout.Fraction(100f), 4);
        Assert.Equal(1d, layout.Fraction(5000f), 4);
    }

    [Fact]
    public void GetBar_MinimumHeightWhenFractionZero()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);

        ChartBar bar = layout.GetBar(0, 300f);

        Assert.Equal(1d, bar.Height, 4);
        Assert.Equal(layout.BaseY - 1d, bar.Y, 4);
        Assert.Equal(layout.GroupX + layout.Gap / 2d, bar.X, 4);
        Assert.Equal((double)layout.BarWidth, bar.Width, 4);
    }

    [Fact]
    public void GetBar_UsesFractionHeightAndFormulas()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);

        const int index = 5;
        ChartBar bar = layout.GetBar(index, 1750f);
        double expectedHeight = layout.Fraction(1750f) * layout.PlotHeight;

        Assert.Equal(expectedHeight, bar.Height, 4);
        Assert.Equal(layout.BaseY - expectedHeight, bar.Y, 4);
        Assert.Equal(layout.GroupX + index * layout.SlotWidth + layout.Gap / 2d, bar.X, 4);
        Assert.Equal((double)layout.BarWidth, bar.Width, 4);

        ChartBar maxBar = layout.GetBar(10, 3200f);
        Assert.Equal((double)layout.PlotHeight, maxBar.Height, 4);
    }

    [Fact]
    public void ResolvePeakIndex_AllBelowThreshold_ReturnsMinusOne()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);

        Assert.Equal(-1, layout.ResolvePeakIndex([100, 500, 809], 2));
        Assert.Equal(-1, layout.ResolvePeakIndex(ReadOnlySpan<ushort>.Empty, 1));
    }

    [Fact]
    public void ResolvePeakIndex_ResolvesReportedChannelToZeroBased()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);
        ushort[] values = [100, 2000, 300, 2500, 900];

        Assert.Equal(3, layout.ResolvePeakIndex(values, 4));
    }

    [Fact]
    public void ResolvePeakIndex_InvalidReportedPeak_ReturnsMinusOne()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);
        ushort[] values = [2000, 900];

        Assert.Equal(-1, layout.ResolvePeakIndex(values, 0));
        Assert.Equal(-1, layout.ResolvePeakIndex(values, 5));
    }

    [Fact]
    public void LabelFlags_TrueForWideSlots()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(1400f, 250f, 41, 41);

        Assert.True(layout.SlotWidth > 27f);
        Assert.True(layout.BarWidth > 15f);
        Assert.True(layout.ShowIndexLabels);
        Assert.True(layout.ShowValueLabels);
    }

    [Fact]
    public void LabelFlags_FalseForNarrowSlots()
    {
        ProfileChartLayout layout = ProfileChartLayout.Compute(500f, 250f, 41, 41);

        Assert.True(layout.SlotWidth <= 27f);
        Assert.True(layout.BarWidth <= 15f);
        Assert.False(layout.ShowIndexLabels);
        Assert.False(layout.ShowValueLabels);
    }
}
