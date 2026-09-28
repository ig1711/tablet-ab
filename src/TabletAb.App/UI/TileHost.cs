using System.Numerics;
using ImGuiNET;

namespace TabletAb.App.UI;

/// <summary>
/// Hosts the fixed tiles and manages the single-window fullscreen toggle.
/// Tiles are plain borderless ImGui windows placed at computed rectangles; a
/// tile switched to fullscreen covers the client area and the rest are hidden.
/// </summary>
public sealed class TileHost
{
    private const ImGuiWindowFlags TileFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoBringToFrontOnFocus |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.NoSavedSettings;

    /// <summary>Flags for a tile whose body is wrapped in a scrollable child.</summary>
    private const ImGuiWindowFlags ScrollableTileFlags =
        TileFlags & ~(ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

    /// <summary>Id of the fullscreen tile, or null when all tiles are shown.</summary>
    public string? Fullscreen { get; private set; }

    private bool _bodyOpen;

    public bool ShouldDraw(string id) => Fullscreen is null || Fullscreen == id;

    /// <summary>
    /// Begin a tile window at the given rectangle (or fullscreen if toggled).
    /// When <paramref name="scrollable"/> is set the header stays put and the
    /// body is wrapped in a scrolling child region. Always pair with
    /// <see cref="EndTile"/>.
    /// </summary>
    public void BeginTile(string id, string title, Vector2 position, Vector2 size, bool scrollable = false)
    {
        bool isFullscreen = Fullscreen == id;
        Vector2 pos = isFullscreen ? Vector2.Zero : position;
        Vector2 dimensions = isFullscreen ? ImGui.GetIO().DisplaySize : size;

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);
        ImGui.Begin(id, scrollable ? ScrollableTileFlags : TileFlags);

        // Header: title on the left, fullscreen toggle on the right.
        ImGui.TextUnformatted(title);

        string buttonText = isFullscreen ? "Tile" : "Fullscreen";
        float buttonWidth = ImGui.CalcTextSize(buttonText).X + ImGui.GetStyle().FramePadding.X * 2f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - buttonWidth - ImGui.GetStyle().WindowPadding.X));
        if (ImGui.Button($"{buttonText}###tilefs"))
            Fullscreen = isFullscreen ? null : id;

        ImGui.Separator();

        _bodyOpen = scrollable;
        if (scrollable)
            ImGui.BeginChild("##body", new Vector2(0, 0), false, ImGuiWindowFlags.None);
    }

    public void EndTile()
    {
        if (_bodyOpen)
        {
            _bodyOpen = false;
            ImGui.EndChild();
        }

        ImGui.End();
    }
}
