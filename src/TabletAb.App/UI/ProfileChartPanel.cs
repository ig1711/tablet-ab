using System.Numerics;
using ImGuiNET;
using TabletAb.Core.Protocol;
using TabletAb.Core.Visualization;

namespace TabletAb.App.UI;

/// <summary>
/// One amplitude profile chart (X or Y), ported from
/// <c>web/src/components/ProfileChart.tsx</c>. Drawn with the ImGui draw list
/// into the current child region: raw-count bars, sample-and-hold, gold peak,
/// identical constants (<see cref="ProfileChartLayout"/>).
/// </summary>
public sealed class ProfileChartPanel
{
    private const float IndexFontSize = 15f;
    private const float ValueFontSize = 14f;
    private const float PeakFontSize = 16f;
    private const float ScaleFontSize = 15f;

    private static readonly uint Background = Rgba(7, 9, 16);
    private static readonly uint BarTop = Rgba(94, 168, 255, 245);
    private static readonly uint BarBottom = Rgba(94, 168, 255, 51);
    private static readonly uint Peak = Rgba(255, 209, 102);
    private static readonly uint Baseline = Rgba(120, 140, 200, 77);
    private static readonly uint TopReference = Rgba(200, 210, 240, 51);
    private static readonly uint ScaleLabel = Rgba(139, 148, 184);
    private static readonly uint IndexLabel = Rgba(109, 118, 154);
    private static readonly uint ValueLabel = Rgba(147, 160, 200);
    private static readonly uint Waiting = Rgba(139, 148, 184);

    private readonly bool _isX;

    public ProfileChartPanel(bool isX) => _isX = isX;

    /// <summary>Draw the chart into the current window/child region.</summary>
    public void DrawChart(Frame? frame)
    {
        ImGui.TextUnformatted(_isX ? "X axis profile" : "Y axis profile");

        Vector2 size = ImGui.GetContentRegionAvail();
        if (size.X < 24 || size.Y < 24)
            return;

        Vector2 origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size); // reserve the content region for the draw list

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, Background);

        if (frame is null)
        {
            const string waiting = "waiting for frames...";
            float waitingWidth = MeasureText(waiting, ImGui.GetFontSize());
            draw.AddText(origin + new Vector2((size.X - waitingWidth) / 2f, size.Y / 2f), Waiting, waiting);
            return;
        }

        ushort[] values = _isX ? frame.X : frame.Y;
        int barCount = values.Length;

        // Shared slot count: both axes use the frame's NX so bar widths match
        // (as in the web UI), regardless of the device's coil geometry.
        ProfileChartLayout layout = ProfileChartLayout.Compute(size.X, size.Y, barCount, frame.Nx);
        int peak = layout.ResolvePeakIndex(values, _isX ? frame.XPeak : frame.YPeak);

        // Top reference line (HI) and baseline (LO), spanning the full plot width.
        float lineLeft = origin.X + ProfileChartLayout.PadLeft;
        float lineRight = lineLeft + layout.PlotWidth;
        DashedLine(draw,
            new Vector2(lineLeft, origin.Y + ProfileChartLayout.PadTop),
            new Vector2(lineRight, origin.Y + ProfileChartLayout.PadTop),
            TopReference);
        draw.AddLine(
            new Vector2(lineLeft, origin.Y + layout.BaseY),
            new Vector2(lineRight, origin.Y + layout.BaseY),
            Baseline);

        // Bars (sample-and-hold: no interpolation between frames).
        for (int i = 0; i < barCount; i++)
        {
            ChartBar bar = layout.GetBar(i, values[i]);
            Vector2 pMin = origin + new Vector2(bar.X, bar.Y);
            Vector2 pMax = origin + new Vector2(bar.X + bar.Width, layout.BaseY);

            if (i == peak)
                draw.AddRectFilled(pMin, pMax, Peak);
            else
                draw.AddRectFilledMultiColor(pMin, pMax, BarTop, BarTop, BarBottom, BarBottom);
        }

        // Index labels (only where they fit, or for the peak).
        for (int i = 0; i < barCount; i++)
        {
            if (!layout.ShowIndexLabels && i != peak)
                continue;

            string text = (i + 1).ToString();
            float textWidth = MeasureText(text, IndexFontSize);
            Vector2 pos = origin + new Vector2(
                layout.GroupX + i * layout.SlotWidth + layout.SlotWidth / 2f - textWidth / 2f,
                layout.BaseY + 6f);
            draw.AddText(ImGui.GetFont(), IndexFontSize, pos, i == peak ? Peak : IndexLabel, text);
        }

        // Per-bar value labels, or just the peak value when bars are narrow.
        for (int i = 0; i < barCount; i++)
        {
            if (!layout.ShowValueLabels)
                break;

            string text = values[i].ToString();
            float textWidth = MeasureText(text, ValueFontSize);
            Vector2 pos = origin + new Vector2(
                layout.GroupX + i * layout.SlotWidth + layout.SlotWidth / 2f - textWidth / 2f,
                layout.BaseY - layout.Fraction(values[i]) * layout.PlotHeight - 2f - ValueFontSize);
            draw.AddText(ImGui.GetFont(), ValueFontSize, pos, i == peak ? Peak : ValueLabel, text);
        }

        if (peak >= 0 && !layout.ShowValueLabels)
        {
            string text = values[peak].ToString();
            float textWidth = MeasureText(text, PeakFontSize);
            Vector2 pos = origin + new Vector2(
                layout.GroupX + peak * layout.SlotWidth + layout.SlotWidth / 2f - textWidth / 2f,
                layout.BaseY - layout.Fraction(values[peak]) * layout.PlotHeight - 3f - PeakFontSize);
            draw.AddText(ImGui.GetFont(), PeakFontSize, pos, Peak, text);
        }

        // Scale labels (HI at the top, LO at the baseline), right-aligned, vertically centred.
        DrawRightAlignedMiddle(draw, origin + new Vector2(ProfileChartLayout.PadLeft - 8f, ProfileChartLayout.PadTop + 6f),
            ((int)ProfileChartLayout.Hi).ToString(), ScaleLabel);
        DrawRightAlignedMiddle(draw, origin + new Vector2(ProfileChartLayout.PadLeft - 8f, layout.BaseY - 6f),
            ((int)ProfileChartLayout.Lo).ToString(), ScaleLabel);
    }

    private static void DrawRightAlignedMiddle(ImDrawListPtr draw, Vector2 rightCentre, string text, uint colour)
    {
        float width = MeasureText(text, ScaleFontSize);
        Vector2 pos = new(rightCentre.X - width, rightCentre.Y - ScaleFontSize / 2f);
        draw.AddText(ImGui.GetFont(), ScaleFontSize, pos, colour, text);
    }

    private static float MeasureText(string text, float fontSize)
        => ImGui.CalcTextSize(text).X * (fontSize / ImGui.GetFontSize());

    private static void DashedLine(ImDrawListPtr draw, Vector2 a, Vector2 b, uint colour, float dash = 5f, float gap = 5f)
    {
        Vector2 delta = b - a;
        float length = delta.Length();
        if (length <= 0.001f)
            return;

        Vector2 dir = delta / length;
        for (float t = 0; t < length; t += dash + gap)
        {
            float end = Math.Min(t + dash, length);
            draw.AddLine(a + dir * t, a + dir * end, colour);
        }
    }

    private static uint Rgba(int r, int g, int b, int a = 255)
        => (uint)((a << 24) | (b << 16) | (g << 8) | r);
}
