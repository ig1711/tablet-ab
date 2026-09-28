using System.Numerics;
using ImGuiNET;
using TabletAb.Core.Input;

namespace TabletAb.App.UI;

/// <summary>Coordinate space for the cursor trail.</summary>
public enum CursorCoordinateMode
{
    /// <summary>0..1 across the active area.</summary>
    Normalized,

    /// <summary>Device-native units (channels for debug, tablet units for OTD).</summary>
    Raw,

    /// <summary>lazer-mapped output pixels.</summary>
    Mapped,
}

/// <summary>
/// Cursor points screen: the newest point plus a configurable trail of discrete
/// previous points (no connecting or interpolated lines), fed from the active
/// pointer source. Replaces the web tool's connected-line cursor view.
/// The plot lives in its own clipped child region so controls can never be
/// covered by zoomed drawing.
/// </summary>
public sealed class CursorPointsPanel
{
    private static readonly uint Background = Rgba(7, 9, 16);
    private static readonly uint AreaFill = Rgba(11, 16, 32);
    private static readonly uint AreaBorder = Rgba(120, 140, 200, 90);
    private static readonly uint Grid = Rgba(140, 160, 220, 28);
    private static readonly uint Crosshair = Rgba(255, 209, 102, 77);
    private static readonly uint Trail = Rgba(94, 168, 255);
    private static readonly uint TrailCrosshair = Rgba(255, 224, 64);
    private static readonly uint PointerFill = Rgba(255, 209, 102);
    private static readonly uint PointerRing = Rgba(255, 255, 255, 230);
    private static readonly uint Label = Rgba(139, 148, 184);
    private static readonly uint JitterGood = Rgba(127, 214, 168);
    private static readonly uint JitterBad = Rgba(255, 154, 122);

    private int _trailLength = 220;
    private int _pointRadius = 2;
    private int _zoom = 1;
    private bool _showGrid = true;
    private CursorCoordinateMode _mode = CursorCoordinateMode.Normalized;

    /// <summary>
    /// Fixed view centre in the current coordinate mode, set by the
    /// "Centre on latest" button. Null means the area centre. It is not
    /// recomputed each frame, so the view does not follow the cursor.
    /// </summary>
    private Vector2? _viewCentre;

    /// <summary>Device-native range for the Raw coordinate mode.</summary>
    public Vector2 RawMax { get; set; } = Vector2.One;

    /// <summary>Output range for the Mapped coordinate mode.</summary>
    public Vector2 MappedMax { get; set; } = new(1920f, 1080f);

    /// <summary>Set when the user clicks "Clear".</summary>
    public bool ClearRequested { get; private set; }

    /// <summary>Draw into the current tile window.</summary>
    public void DrawContent(PointerTrail trail)
    {
        ClearRequested = false;

        bool centreRequested = DrawControls(trail);

        PointerPoint[] points = trail.Snapshot(out long totalAdded);
        if (centreRequested && points.Length > 0)
            _viewCentre = Units(points[^1]);

        if (ImGui.BeginChild("cursorplot", new Vector2(0, 0), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            Vector2 size = ImGui.GetContentRegionAvail();
            if (size.X >= 40 && size.Y >= 40)
            {
                Vector2 origin = ImGui.GetCursorScreenPos();
                ImGui.Dummy(size);

                ImDrawListPtr draw = ImGui.GetWindowDrawList();
                draw.AddRectFilled(origin, origin + size, Background);

                if (points.Length == 0)
                {
                    draw.AddText(origin + new Vector2(10, 10), Label, "no cursor points (a pointer source must be active)");
                }
                else
                {
                    DrawPlot(draw, origin, size, points, totalAdded, ModeMax());
                    DrawReadouts(draw, origin, points);
                }
            }
        }

        ImGui.EndChild();
    }

    /// <summary>Draw the controls; returns true when "Centre on latest" was clicked.</summary>
    private bool DrawControls(PointerTrail trail)
    {
        bool centreRequested = false;

        string[] modes = { "normalized", "raw", "lazer px" };
        int modeIndex = (int)_mode;
        ImGui.SetNextItemWidth(140);
        if (ImGui.Combo("Coords##cursor", ref modeIndex, modes, modes.Length))
        {
            _mode = (CursorCoordinateMode)modeIndex;
            _viewCentre = null;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderInt("Trail##cursor", ref _trailLength, 1, PointerTrail.MaxCapacity))
            trail.DisplayCount = _trailLength;

        ImGui.SetNextItemWidth(160);
        ImGui.SliderInt("Point size##cursor", ref _pointRadius, 1, 6);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        ImGui.SliderInt("Zoom##cursor", ref _zoom, 1, 64);

        ImGui.Checkbox("Grid##cursor", ref _showGrid);
        ImGui.SameLine();
        if (ImGui.Button("Centre on latest##cursor"))
            centreRequested = true;

        ImGui.SameLine();
        if (ImGui.Button("Clear##cursor"))
            ClearRequested = true;

        return centreRequested;
    }

    private void DrawPlot(ImDrawListPtr draw, Vector2 origin, Vector2 size, PointerPoint[] points, long totalAdded, Vector2 max)
    {
        float maxX = Math.Max(1e-3f, max.X);
        float maxY = Math.Max(1e-3f, max.Y);

        // Fill the whole window: independent X/Y scales, no letterboxing.
        float scaleX = size.X / maxX;
        float scaleY = size.Y / maxY;

        // View window in units, centred on the fixed view centre (or the area
        // centre). The view no longer follows the latest point.
        float zoom = Math.Max(1, _zoom);
        float halfX = maxX / (2f * zoom);
        float halfY = maxY / (2f * zoom);

        Vector2 centre = _viewCentre ?? new Vector2(maxX / 2f, maxY / 2f);
        centre.X = Math.Clamp(centre.X, halfX, maxX - halfX);
        centre.Y = Math.Clamp(centre.Y, halfY, maxY - halfY);

        float vx0 = centre.X - halfX;
        float vy0 = centre.Y - halfY;

        Vector2 ToScreen(Vector2 units) => new(
            origin.X + (units.X - vx0) * scaleX * zoom,
            origin.Y + (units.Y - vy0) * scaleY * zoom);

        bool InView(Vector2 units) =>
            units.X >= vx0 && units.X <= vx0 + 2f * halfX &&
            units.Y >= vy0 && units.Y <= vy0 + 2f * halfY;

        // Clip everything to the plot rect. Without this, zoomed-in area/grid/
        // crosshair geometry extends far off-screen and produces rendering
        // artifacts once the zoom (or trail count) grows.
        draw.PushClipRect(origin, origin + size, true);

        Vector2 areaMin = ToScreen(Vector2.Zero);
        Vector2 areaMax = ToScreen(new Vector2(maxX, maxY));
        Vector2 plotMin = origin;
        Vector2 plotMax = origin + size;

        // Area fill only over its visible intersection.
        Vector2 visMin = Vector2.Max(areaMin, plotMin);
        Vector2 visMax = Vector2.Min(areaMax, plotMax);
        if (visMin.X < visMax.X && visMin.Y < visMax.Y)
            draw.AddRectFilled(visMin, visMax, AreaFill);
        draw.AddRect(areaMin, areaMax, AreaBorder);

        if (_showGrid)
        {
            for (int i = 1; i < 10; i++)
            {
                float gx = maxX * i / 10f;
                float sx = origin.X + (gx - vx0) * scaleX * zoom;
                if (sx >= plotMin.X && sx <= plotMax.X)
                    draw.AddLine(new Vector2(sx, Math.Max(areaMin.Y, plotMin.Y)),
                        new Vector2(sx, Math.Min(areaMax.Y, plotMax.Y)), Grid);

                float gy = maxY * i / 10f;
                float sy = origin.Y + (gy - vy0) * scaleY * zoom;
                if (sy >= plotMin.Y && sy <= plotMax.Y)
                    draw.AddLine(new Vector2(Math.Max(areaMin.X, plotMin.X), sy),
                        new Vector2(Math.Min(areaMax.X, plotMax.X), sy), Grid);
            }
        }

        // Centre crosshair (the origin jitter is measured against), limited to
        // the visible span.
        Vector2 c = ToScreen(centre);
        float top = Math.Max(areaMin.Y, plotMin.Y);
        float bottom = Math.Min(areaMax.Y, plotMax.Y);
        float left = Math.Max(areaMin.X, plotMin.X);
        float right = Math.Min(areaMax.X, plotMax.X);
        if (c.X >= plotMin.X && c.X <= plotMax.X)
            DashedLine(draw, new Vector2(c.X, top), new Vector2(c.X, bottom), Crosshair);
        if (c.Y >= plotMin.Y && c.Y <= plotMax.Y)
            DashedLine(draw, new Vector2(left, c.Y), new Vector2(right, c.Y), Crosshair);

        // Trail: discrete points, oldest most transparent, newest brightest.
        // Every fifth point (by its stable absolute index, not its position in
        // the sliding window) is a yellow crosshair instead of a dot.
        int n = points.Length;
        long firstIndex = totalAdded - n;
        for (int i = 0; i < n; i++)
        {
            Vector2 units = Units(points[i]);
            if (!InView(units))
                continue;

            Vector2 p = ToScreen(units);
            float alpha = 0.08f + 0.7f * ((float)(i + 1) / n);

            if ((firstIndex + i) % 5 == 0)
            {
                uint cross = (TrailCrosshair & 0x00FFFFFFu) | ((uint)(alpha * 255f) << 24);
                float arm = _pointRadius + 3f;
                draw.AddLine(p - new Vector2(arm, 0), p + new Vector2(arm, 0), cross, 1.5f);
                draw.AddLine(p - new Vector2(0, arm), p + new Vector2(0, arm), cross, 1.5f);
                continue;
            }

            uint colour = (Trail & 0x00FFFFFFu) | ((uint)(alpha * 255f) << 24);
            draw.AddCircleFilled(p, _pointRadius, colour, 10);
        }

        // Newest point.
        Vector2 latest = Units(points[^1]);
        if (InView(latest))
        {
            Vector2 p = ToScreen(latest);
            draw.AddCircleFilled(p, _pointRadius + 2f, PointerFill, 12);
            draw.AddCircle(p, _pointRadius + 5f, PointerRing, 16, 1.25f);
        }

        draw.PopClipRect();
    }

    private void DrawReadouts(ImDrawListPtr draw, Vector2 origin, PointerPoint[] points)
    {
        // Jitter is measured over a fixed recent window, independent of the
        // visible trail length (so it is meaningful at any trail setting).
        const int JitterWindow = 240;

        int n = points.Length;
        int start = Math.Max(0, n - JitterWindow);

        double meanX = 0, meanY = 0;
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        for (int i = start; i < n; i++)
        {
            PointerPoint p = points[i];
            meanX += p.NormalizedX;
            meanY += p.NormalizedY;
            minX = Math.Min(minX, p.NormalizedX);
            maxX = Math.Max(maxX, p.NormalizedX);
            minY = Math.Min(minY, p.NormalizedY);
            maxY = Math.Max(maxY, p.NormalizedY);
        }

        int window = n - start;
        meanX /= window;
        meanY /= window;

        double ssX = 0, ssY = 0;
        for (int i = start; i < n; i++)
        {
            PointerPoint p = points[i];
            ssX += (p.NormalizedX - meanX) * (p.NormalizedX - meanX);
            ssY += (p.NormalizedY - meanY) * (p.NormalizedY - meanY);
        }

        double sdX = window > 1 ? Math.Sqrt(ssX / (window - 1)) : 0;
        double sdY = window > 1 ? Math.Sqrt(ssY / (window - 1)) : 0;
        double p2pX = maxX - minX;
        double p2pY = maxY - minY;

        bool bad = p2pX > 0.005 || p2pY > 0.005;
        PointerPoint latest = points[^1];

        Vector2 pos = origin + new Vector2(10, 8);
        draw.AddText(pos, Label, $"points {n}   mode {_mode.ToString().ToLowerInvariant()}   latest nx {latest.NormalizedX:F4} ny {latest.NormalizedY:F4}");
        draw.AddText(pos + new Vector2(0, 16), bad ? JitterBad : JitterGood,
            $"jitter({window}) X sigma {sdX:F5} p2p {p2pX:F5}   Y sigma {sdY:F5} p2p {p2pY:F5}");
        draw.AddText(pos + new Vector2(0, 32), Label,
            $"raw {latest.RawX:F1},{latest.RawY:F1}   mapped {latest.MappedX:F1},{latest.MappedY:F1}   pressure {latest.Pressure:F2}");
    }

    private Vector2 ModeMax() => _mode switch
    {
        CursorCoordinateMode.Raw => RawMax,
        CursorCoordinateMode.Mapped => MappedMax,
        _ => Vector2.One,
    };

    private Vector2 Units(PointerPoint p) => _mode switch
    {
        CursorCoordinateMode.Raw => new Vector2(p.RawX, p.RawY),
        CursorCoordinateMode.Mapped => new Vector2(p.MappedX, p.MappedY),
        _ => new Vector2((float)p.NormalizedX, (float)p.NormalizedY),
    };

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
