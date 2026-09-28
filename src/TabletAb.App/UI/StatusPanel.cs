using System.Numerics;
using ImGuiNET;
using TabletAb.App.Rendering;
using TabletAb.Core;
using TabletAb.Core.Input;
using TabletAb.Core.Statistics;
using Veldrid;

namespace TabletAb.App.UI;

/// <summary>
/// The Phase 1 status / environment panel. Shows the active graphics backend,
/// frame pacing controls and a placeholder for the A/B acquisition controls
/// (added in Phase 5).
/// </summary>
public sealed class StatusPanel
{
    private static readonly FrameSyncMode[] SyncModes =
    {
        FrameSyncMode.Unlimited,
        FrameSyncMode.VSync,
        FrameSyncMode.Limit2x,
        FrameSyncMode.Limit4x,
        FrameSyncMode.Limit8x,
    };

    private static readonly string[] SyncLabels =
    {
        "Unlimited",
        "VSync",
        "2x refresh",
        "4x refresh",
        "8x refresh",
    };

    // --- state set by AbApplication each frame or by the UI ---

    public GraphicsBackend CurrentBackend { get; set; }
    public GraphicsBackend[] AvailableBackends { get; set; } = [];
    public string DeviceName { get; set; } = "";
    public double Fps { get; set; }

    public FrameSyncMode FrameSync { get; set; } = FrameSyncMode.VSync;
    public int RefreshHz { get; set; } = 60;

    // --- device state set by AbApplication each frame ---

    public bool IsConnected { get; set; }
    public string TabletDeviceName { get; set; } = "";
    public string TabletStatus { get; set; } = "disconnected";

    /// <summary>Detected/forced device protocol ("v5"/"v6"), or null while unknown.</summary>
    public string? ProtocolName { get; set; }
    public LiveStatsSnapshot? Stats { get; set; }

    public bool ConnectVendorRequested { get; private set; }
    public bool ConnectSimulatedRequested { get; private set; }
    public bool DisconnectRequested { get; private set; }

    // --- pointer state set by AbApplication each frame ---

    public PointerMode PointerMode { get; set; }
    public string PointerDevice { get; set; } = "";
    public long PointerPoints { get; set; }
    public PointerPoint? LastPointer { get; set; }
    public PointerMode? RequestedPointerMode { get; private set; }

    private static readonly PointerMode[] PointerModes =
    {
        PointerMode.Off, PointerMode.Debug, PointerMode.Otd, PointerMode.External,
    };

    private static readonly string[] PointerLabels =
    {
        "Off", "Debug frames", "OTD release HID", "External pointer",
    };

    /// <summary>When a backend change is requested, set only if hot-switch is allowed.</summary>
    public bool AllowHotSwitch { get; set; }

    /// <summary>Set by the UI when the user picks a different backend.</summary>
    public GraphicsBackend? RequestedBackend { get; set; }

    public bool QuitRequested { get; private set; }

    public void DrawContent()
    {
        ConnectVendorRequested = false;
        ConnectSimulatedRequested = false;
        DisconnectRequested = false;
        RequestedPointerMode = null;

        ImGui.Text($"Backend : {CurrentBackend}");
            ImGui.Text($"GPU     : {DeviceName}");
            ImGui.Text($"FPS     : {Fps:F1}");
            ImGui.Separator();

            DrawDeviceSection();
            DrawPointerSection();

            ImGui.Separator();
            DrawFrameSync();
            DrawBackendSelector();

            ImGui.Separator();
            ImGui.TextDisabled("Acquisition controls are in the A/B acquisition window.");

            ImGui.Separator();
            if (ImGui.Button("Quit"))
                QuitRequested = true;
    }

    private void DrawDeviceSection()
    {
        ImGui.Text("Device (256c:6111 vendor debug)");

        if (ImGui.Button("Connect USB"))
            ConnectVendorRequested = true;
        ImGui.SameLine();
        if (ImGui.Button("Connect simulated"))
            ConnectSimulatedRequested = true;
        ImGui.SameLine();
        if (ImGui.Button("Disconnect"))
            DisconnectRequested = true;

        ImGui.Text($"state   : {(IsConnected ? "streaming" : TabletStatus)}");
        if (IsConnected && !string.IsNullOrEmpty(ProtocolName))
            ImGui.Text($"protocol: {ProtocolName}");
        if (IsConnected && !string.IsNullOrEmpty(TabletDeviceName))
            ImGui.Text($"device  : {TabletDeviceName}");

        if (Stats is { } s)
        {
            ImGui.Text($"frames  : {s.Frames}   errors: {s.Errors}");
            ImGui.Text($"rate    : {s.ScanRateHz:F1} Hz scan   host {s.Fps:F1} fps");
            ImGui.Text($"period  : {s.FramePeriodUs / 1000.0:F3} ms   jitter ±{s.ScanJitterUs / 1000.0:F3} ms");
            ImGui.Text($"scan    : {s.ScanTimeMeanUs:F0} us   mode {(s.Mode == 2 ? "repeat" : s.Mode == 1 ? "hw" : "sw")}   adc {s.AdcSamples}@/{s.AdcClock}");
            ImGui.Text($"pen     : {(s.Pen ? "yes" : "no")}   freq {s.Frequency}   burst {s.Burst}");
            ImGui.Text($"peaks   : x {s.XPeak} ({s.XMax})   y {s.YPeak} ({s.YMax})");
            ImGui.Text($"flip    : x {(s.XFlipRate * 100):F0}%   y {(s.YFlipRate * 100):F0}%   reacq {s.Reacquires}");
            ImGui.Text($"window  : {(s.Windowed ? "windowed" : "full")}   estimator {(s.VendorEstimator ? "vendor" : "other")}");
        }
        else
        {
            ImGui.TextDisabled("no frames yet");
        }
    }

    private void DrawPointerSection()
    {
        ImGui.Text("Cursor input (osu!lazer-style)");

        int current = Array.IndexOf(PointerModes, PointerMode);
        if (current < 0)
            current = 0;

        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("Mode##pointer", ref current, PointerLabels, PointerLabels.Length) &&
            PointerModes[current] != PointerMode)
        {
            RequestedPointerMode = PointerModes[current];
        }

        if (!string.IsNullOrEmpty(PointerDevice))
            ImGui.Text($"device  : {PointerDevice}");
        ImGui.Text($"points  : {PointerPoints}");

        if (LastPointer is { } p)
        {
            ImGui.Text($"last    : nx {p.NormalizedX:F4} ny {p.NormalizedY:F4}  pressure {p.Pressure:F2}");
            ImGui.Text($"          raw {p.RawX:F1},{p.RawY:F1}   mapped {p.MappedX:F1},{p.MappedY:F1}");
        }
        else
        {
            ImGui.TextDisabled("no pointer points yet");
        }

        if (PointerMode == PointerMode.External)
            ImGui.TextDisabled("external mode: run under X11/XWayland for global capture");
    }

    private void DrawFrameSync()
    {
        ImGui.Text("Frame pacing (osu!lazer FrameSync)");

        int current = Array.IndexOf(SyncModes, FrameSync);
        if (current < 0)
            current = 1;

        ImGui.SetNextItemWidth(200);
        if (ImGui.Combo("##framesync", ref current, SyncLabels, SyncLabels.Length))
            FrameSync = SyncModes[current];

        if (FrameSync is FrameSyncMode.Limit2x or FrameSyncMode.Limit4x or FrameSyncMode.Limit8x)
        {
            ImGui.SetNextItemWidth(200);
            int refresh = RefreshHz;
            if (ImGui.InputInt("Display refresh (Hz)", ref refresh))
                RefreshHz = Math.Clamp(refresh, 10, 1000);
        }
    }

    private void DrawBackendSelector()
    {
        ImGui.Text("Graphics backend");

        string[] names = Array.ConvertAll(AvailableBackends, b => b.ToString());
        int current = Array.IndexOf(AvailableBackends, CurrentBackend);

        ImGui.SetNextItemWidth(200);
        if (current >= 0 &&
            ImGui.Combo("##backend", ref current, names, names.Length) &&
            AvailableBackends[current] != CurrentBackend)
        {
            RequestedBackend = AvailableBackends[current];
        }

        bool allowHotSwitch = AllowHotSwitch;
        if (ImGui.Checkbox("Allow hot backend switch (experimental)", ref allowHotSwitch))
            AllowHotSwitch = allowHotSwitch;

        if (!AllowHotSwitch)
            ImGui.TextDisabled("Restart with --backend <name> to change reliably.");
    }
}
