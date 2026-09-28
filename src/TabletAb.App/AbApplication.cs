using System.IO;
using System.Numerics;
using ImGuiNET;
using TabletAb.App.Rendering;
using TabletAb.App.UI;
using TabletAb.Core;
using TabletAb.Core.Input;
using TabletAb.Core.Protocol;
using TabletAb.Core.Statistics;
using TabletAb.Input;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace TabletAb.App;

/// <summary>
/// Owns the window, graphics device and main loop. Renders via Veldrid (the
/// same GPU abstraction osu!lazer uses) and drives the ImGui UI.
/// </summary>
public sealed class AbApplication : IDisposable
{
    /// <summary>Backends probed at startup, in preference order.</summary>
    private static readonly GraphicsBackend[] Candidates =
    {
        GraphicsBackend.Vulkan,
        GraphicsBackend.OpenGL,
        GraphicsBackend.Direct3D11,
        GraphicsBackend.OpenGLES,
        GraphicsBackend.Metal,
    };

    /// <summary>UI scale factor applied to style metrics and the font size.</summary>
    private const float UiScale = 1.5f;

    /// <summary>ImGui's built-in reference font size (px), scaled by <see cref="UiScale"/>.</summary>
    private const float BaseFontSize = 13f;

    private readonly AppOptions _options;
    private readonly FrameLimiter _limiter = new();
    private readonly StatusPanel _panel = new();
    private readonly AcquisitionPanel _acquisitionPanel = new();
    private readonly AcquisitionSettings _settings = new();
    private readonly ProfileChartPanel _profileX = new(true);
    private readonly ProfileChartPanel _profileY = new(false);
    private readonly TileHost _tiles = new();
    private readonly LiveStatistics _statistics = new();
    private readonly object _statisticsGate = new();

    private Frame? _displayFrame;

    private IFrameSource? _source;
    private volatile bool _pendingDisconnect;
    private string _tabletStatus = "disconnected";

    /// <summary>Command set matching the connected firmware (v6 by default).</summary>
    private ICommandBuilder _commands = CommandBuilderV6.Instance;

    /// <summary>Detected/forced protocol name for the UI, or null while unknown.</summary>
    private string? _protocolName;

    /// <summary>Whether the initial full settings push still has to happen (once the
    /// device protocol version is known from the first frame).</summary>
    private bool _pushPending;

    private readonly ExternalPointerSource _externalPointer = new();
    private readonly object _pointerGate = new();
    private readonly PointerTrail _pointerTrail = new();
    private readonly CursorPointsPanel _cursorPanel = new();
    private IPointerSource? _pointerSource;
    private PointerMode _pointerMode;
    private PointerPoint? _lastPointer;
    private double _lastPointerMs = double.NegativeInfinity;

    private readonly GraphicsBackend[] _available;
    private GraphicsBackend _backend;

    private Sdl2Window _window = null!;
    private GraphicsDevice _gd = null!;
    private ImGuiRenderer _imgui = null!;
    private CommandList _cl = null!;

    private bool _resourcesCreated;
    private bool _shutdownDone;
    private bool _disposed;
    private bool _styleConfigured;
    private long _frameCount;

    public AbApplication(AppOptions options)
    {
        _options = options;

        _available = Candidates.Where(IsSupported).ToArray();
        if (_available.Length == 0)
            throw new InvalidOperationException("No supported graphics backend found.");

        _backend = options.Backend is { } requested && _available.Contains(requested)
            ? requested
            : DefaultBackend(_available);

        _panel.FrameSync = options.FrameSync;
        _panel.RefreshHz = options.RefreshHz;
    }

    public void Run()
    {
        CreateResources();
        _resourcesCreated = true;

        Console.WriteLine($"{ProductInfo.Name} {ProductInfo.Version}  backend={_backend}  device={_gd.DeviceName}");
        Console.WriteLine($"available backends: {string.Join(", ", _available)}");

        _limiter.Start();

        if (_options.Source != InputSource.None)
            Connect(_options.Source);

        if (_options.Pointer != PointerMode.Off)
            SetPointerMode(_options.Pointer);

        while (_window.Exists)
        {
            double delta = _limiter.Tick();
            InputSnapshot snapshot = _window.PumpEvents();
            _imgui.Update((float)delta, snapshot);

            UpdatePanel();
            DrawLayout();
            HandlePanelRequests();

            ApplyFrameSync();

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(0.06f, 0.07f, 0.09f, 1f));
            _imgui.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);

            _limiter.Sleep(TargetHz());

            if (_options.Frames > 0 && ++_frameCount >= _options.Frames)
                _window.Close();
        }

        Shutdown();
    }

    private void UpdatePanel()
    {
        _panel.CurrentBackend = _backend;
        _panel.AvailableBackends = _available;
        _panel.DeviceName = _gd.DeviceName;
        _panel.Fps = _limiter.Fps;

        _panel.IsConnected = _source?.IsOpen ?? false;
        _panel.TabletDeviceName = _source?.DisplayName ?? string.Empty;
        _panel.TabletStatus = _tabletStatus;
        _panel.ProtocolName = _protocolName;

        if (_source is { } source)
        {
            lock (_statisticsGate)
                _panel.Stats = _statistics.Snapshot(source.FramesReceived, source.Errors, source.BytesReceived);
        }
        else
        {
            _panel.Stats = null;
        }

        // Freeze display holds the last shown frame without stopping the stream.
        Frame? latest;
        lock (_statisticsGate)
            latest = _statistics.Latest;

        if (!_settings.FreezeDisplay)
            _displayFrame = latest;

        _panel.PointerMode = _pointerMode;
        _panel.PointerDevice = _pointerSource?.DisplayName ?? string.Empty;
        _panel.PointerPoints = _pointerSource?.PointsReceived ?? 0;
        lock (_pointerGate)
            _panel.LastPointer = _lastPointer;
    }

    private void HandlePanelRequests()
    {
        ResolveProtocolAndPush();

        if (_acquisitionPanel.PushAllRequested)
            PushAllSettings();

        if (_panel.QuitRequested)
        {
            _window.Close();
            return;
        }

        if (_pendingDisconnect)
        {
            _pendingDisconnect = false;
            Disconnect();
        }

        if (_panel.ConnectVendorRequested)
            Connect(InputSource.VendorUsb);

        if (_panel.ConnectSimulatedRequested)
            Connect(InputSource.Simulated);

        if (_panel.DisconnectRequested)
        {
            _tabletStatus = "disconnected";
            Disconnect();
        }

        if (_panel.RequestedPointerMode is { } pointerMode)
            SetPointerMode(pointerMode);

        if (_panel.RequestedBackend is not { } requested)
            return;

        _panel.RequestedBackend = null;

        if (_panel.AllowHotSwitch)
        {
            Recreate(requested);
        }
        else
        {
            // Hot switching is flaky on some compositors (notably Vulkan -> OpenGL
            // with Arch's sdl2-compat), so changes require a restart by default.
            Console.WriteLine(
                $"Backend '{requested}' requested. Hot switch is disabled; " +
                $"restart with --backend {requested.ToString().ToLowerInvariant()}.");
        }
    }

    private void Connect(InputSource source)
    {
        Disconnect();
        _statistics.Reset();

        IFrameSource transport = source == InputSource.Simulated
            ? new SimulatedFrameTransport(_options.SimulatedRateHz)
            : new VendorUsbTransport();

        transport.FrameReceived += OnFrameReceived;
        transport.Lost += OnTransportLost;

        try
        {
            transport.Open();
            _source = transport;
            _tabletStatus = "streaming";
            Console.WriteLine($"connected: {transport.DisplayName}");

            // Pick the command set: forced, or auto-detect from the first frame.
            if (_options.Protocol == ProtocolPreference.V5)
            {
                _commands = CommandBuilderV5.Instance;
                _protocolName = "v5 (forced)";
                _pushPending = false;
                PushAllSettings();
            }
            else if (_options.Protocol == ProtocolPreference.V6)
            {
                _commands = CommandBuilderV6.Instance;
                _protocolName = "v6 (forced)";
                _pushPending = false;
                PushAllSettings();
            }
            else
            {
                _commands = CommandBuilderV6.Instance;
                _protocolName = null;
                _pushPending = true;   // wait for the first frame to learn the version
            }

            if (_pointerMode == PointerMode.Debug)
                SetPointerMode(PointerMode.Debug);
        }
        catch (Exception ex)
        {
            transport.FrameReceived -= OnFrameReceived;
            transport.Lost -= OnTransportLost;
            transport.Dispose();
            _tabletStatus = $"failed: {ex.Message}";
            Console.WriteLine($"connect failed: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        if (_pointerMode == PointerMode.Debug)
            DetachPointer();

        IFrameSource? source = _source;
        _source = null;

        if (source is null)
            return;

        _pushPending = false;
        _commands = CommandBuilderV6.Instance;
        _protocolName = null;

        source.FrameReceived -= OnFrameReceived;
        source.Lost -= OnTransportLost;

        try
        {
            source.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"disconnect error: {ex.Message}");
        }

        source.Dispose();
    }

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        lock (_statisticsGate)
            _statistics.Add(e.Frame);
    }

    private void OnTransportLost(object? sender, TransportLostEventArgs e)
    {
        _tabletStatus = $"lost: {e.Reason}";
        _pendingDisconnect = true;
    }

    /// <summary>Push one command to the device (no-op when disconnected).</summary>
    private void SendCommand(byte[] command) => (_source as ICommandSink)?.SendCommand(command);

    /// <summary>Once the first frame has arrived, match the command set to its
    /// protocol version and push the full configuration.</summary>
    private void ResolveProtocolAndPush()
    {
        if (!_pushPending || _source is not ICommandSink sink)
            return;

        Frame? latest;
        lock (_statisticsGate)
            latest = _statistics.Latest;

        if (latest is null)
            return;

        _commands = latest.Version >= ProtocolVersion.V6 ? CommandBuilderV6.Instance : CommandBuilderV5.Instance;
        _protocolName = _commands.Name;
        _settings.PushAll(sink, _commands);
        _pushPending = false;
        Console.WriteLine($"protocol detected: {_commands.Name} (frame v{latest.Version}); device reported " +
                          $"freq={latest.Frequency} burst={latest.Burst} adc={latest.AdcSamples}@{latest.AdcClock}");
        if (_commands is CommandBuilderV5)
            Console.WriteLine("  (legacy v5 firmware cannot report window size/ramp/recentre in its frames)");
        Console.WriteLine($"pushed: freq={_settings.FrequencyIndex} burst={_settings.Burst} " +
                          $"adc={_settings.AdcSamples}@{_settings.AdcClock} window={_settings.WindowCoilsX}/{_settings.WindowCoilsY} " +
                          $"settleC={_settings.SettleC} recentre={_settings.RecentreMode} estimator={_settings.Estimator} " +
                          $"reacq={_settings.ReacquireThreshold}/{_settings.CoarseStride}/{_settings.ReacquirePeriodMs}ms");
    }

    /// <summary>Re-send every A/B setting (on connect or via "Push all settings").</summary>
    private void PushAllSettings()
    {
        if (_source is ICommandSink sink)
            _settings.PushAll(sink, _commands);
    }

    private void SetPointerMode(PointerMode mode)
    {
        DetachPointer();
        _pointerMode = mode;
        _pointerTrail.Clear();

        switch (mode)
        {
            case PointerMode.Debug when _source is IFrameSource frames:
                _pointerSource = new DebugFramePointerSource(frames);
                break;

            case PointerMode.Otd:
                var otd = new OtdPointerSource();
                try
                {
                    otd.Open();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OTD pointer failed: {ex.Message}");
                }
                _pointerSource = otd;
                break;

            case PointerMode.External:
                _externalPointer.Open();
                _pointerSource = _externalPointer;
                break;

            default:
                _pointerSource = null;
                break;
        }

        if (_pointerSource is not null)
            _pointerSource.PointReceived += OnPointerPoint;

        Console.WriteLine($"pointer mode: {_pointerMode} ({_pointerSource?.DisplayName ?? "none"})");
    }

    private void DetachPointer()
    {
        if (_pointerSource is null)
            return;

        _pointerSource.PointReceived -= OnPointerPoint;

        if (ReferenceEquals(_pointerSource, _externalPointer))
            _externalPointer.Close();
        else
            _pointerSource.Dispose();

        _pointerSource = null;
        _pointerTrail.Clear();
    }

    private void OnPointerPoint(object? sender, PointerPointEventArgs e)
    {
        lock (_pointerGate)
        {
            _lastPointer = e.Point;
            _lastPointerMs = e.Point.TimestampMs;
        }

        _pointerTrail.Add(e.Point);
    }

    private void DrawCursorContent()
    {
        // Debug firmware emits nothing while the pen is away, so a live trail
        // that stops receiving points is stale: clear it after a short gap.
        double lastMs;
        lock (_pointerGate)
            lastMs = _lastPointerMs;

        if (_pointerSource is not null && _pointerTrail.Count > 0 && MonotonicClock.NowMs() - lastMs > 300)
            _pointerTrail.Clear();

        _cursorPanel.RawMax = _pointerMode switch
        {
            PointerMode.Debug => new Vector2(ProtocolConstants.Nx, ProtocolConstants.Ny),
            PointerMode.Otd when _pointerSource is OtdPointerSource otd => otd.RawMax,
            PointerMode.External => new Vector2(_window.Width, _window.Height),
            _ => Vector2.One,
        };

        _cursorPanel.MappedMax = _pointerMode == PointerMode.Otd && _pointerSource is OtdPointerSource otdMapped
            ? otdMapped.MappedMax
            : _cursorPanel.RawMax;

        _cursorPanel.DrawContent(_pointerTrail);

        if (_cursorPanel.ClearRequested)
            _pointerTrail.Clear();
    }

    /// <summary>Lay out the fixed tiles: status + A/B on the left, plots on the right.</summary>
    private void DrawLayout()
    {
        Vector2 display = ImGui.GetIO().DisplaySize;
        if (display.X < 1f || display.Y < 1f)
            return;

        float leftWidth = MathF.Round(display.X * 0.36f);
        float rightX = leftWidth;
        float rightWidth = display.X - leftWidth;

        float statusHeight = MathF.Round(display.Y * 0.36f);
        float abHeight = display.Y - statusHeight;

        float profilesHeight = MathF.Round(display.Y * 0.40f);
        float cursorY = profilesHeight;
        float cursorHeight = display.Y - profilesHeight;

        DrawTile("tile.status", "Status", new Vector2(0, 0), new Vector2(leftWidth, statusHeight), _panel.DrawContent, scrollable: true);
        DrawTile("tile.ab", "A/B acquisition", new Vector2(0, statusHeight), new Vector2(leftWidth, abHeight),
            () => _acquisitionPanel.DrawContent(_settings, SendCommand, _commands, _source is not null), scrollable: true);
        DrawTile("tile.profiles", "Amplitude profiles", new Vector2(rightX, 0), new Vector2(rightWidth, profilesHeight), DrawProfiles);
        DrawTile("tile.cursor", "Cursor points", new Vector2(rightX, cursorY), new Vector2(rightWidth, cursorHeight), DrawCursorContent);
    }

    private void DrawTile(string id, string title, Vector2 position, Vector2 size, Action content, bool scrollable = false)
    {
        if (!_tiles.ShouldDraw(id))
            return;

        _tiles.BeginTile(id, title, position, size, scrollable);
        content();
        _tiles.EndTile();
    }

    private void DrawProfiles()
    {
        Vector2 avail = ImGui.GetContentRegionAvail();
        float half = MathF.Max(60f, (avail.Y - ImGui.GetStyle().ItemSpacing.Y) / 2f);

        ImGui.BeginChild("profile.x", new Vector2(0, half), true, ImGuiWindowFlags.NoScrollbar);
        _profileX.DrawChart(_displayFrame);
        ImGui.EndChild();

        ImGui.BeginChild("profile.y", new Vector2(0, 0), true, ImGuiWindowFlags.NoScrollbar);
        _profileY.DrawChart(_displayFrame);
        ImGui.EndChild();
    }

    private void OnMouseMove(MouseMoveEventArgs e)
    {
        if (_pointerMode == PointerMode.External)
            _externalPointer.Push(e.MousePosition.X, e.MousePosition.Y, _window.Width, _window.Height);
    }

    private void ApplyFrameSync()
    {
        bool wantVsync = _panel.FrameSync == FrameSyncMode.VSync;
        if (_gd.SyncToVerticalBlank != wantVsync)
            _gd.SyncToVerticalBlank = wantVsync;
    }

    private double TargetHz() => _panel.FrameSync switch
    {
        FrameSyncMode.Limit2x => _panel.RefreshHz * 2.0,
        FrameSyncMode.Limit4x => _panel.RefreshHz * 4.0,
        FrameSyncMode.Limit8x => _panel.RefreshHz * 8.0,
        _ => 0.0, // Unlimited and VSync are not software-limited.
    };

    private void CreateResources()
    {
        var deviceOptions = new GraphicsDeviceOptions(
            debug: false,
            swapchainDepthFormat: null,
            syncToVerticalBlank: _panel.FrameSync == FrameSyncMode.VSync)
        {
            PreferStandardClipSpaceYDirection = true,
        };

        var windowCi = new WindowCreateInfo
        {
            X = 100,
            Y = 100,
            WindowWidth = _options.Width,
            WindowHeight = _options.Height,
            WindowTitle = $"{ProductInfo.Name} ({_backend})",
            WindowInitialState = WindowState.Normal,
        };

        VeldridStartup.CreateWindowAndGraphicsDevice(windowCi, deviceOptions, _backend, out _window, out _gd);
        _window.Resized += OnWindowResized;
        _window.MouseMove += OnMouseMove;

        _cl = _gd.ResourceFactory.CreateCommandList();
        _imgui = new ImGuiRenderer(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
        ConfigureImGuiStyle();
    }

    /// <summary>Scale the UI 1.5x and use flat, border-only tiles (once).</summary>
    private void ConfigureImGuiStyle()
    {
        if (_styleConfigured)
            return;

        _styleConfigured = true;
        ImGuiIOPtr io = ImGui.GetIO();
        ImGui.GetStyle().ScaleAllSizes(UiScale);
        ImGui.GetStyle().WindowRounding = 0f;
        ImGui.GetStyle().WindowBorderSize = 1f;

        // The default ImGui font is a bitmap atlas that goes blurry when scaled
        // up. Rasterise a real TTF at the effective size instead and keep
        // FontGlobalScale at 1 so glyphs are drawn 1:1 with the atlas.
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "NotoSans-Regular.ttf");
        if (File.Exists(fontPath))
        {
            io.Fonts.Clear();
            io.Fonts.AddFontFromFileTTF(fontPath, BaseFontSize * UiScale);
            io.FontGlobalScale = 1f;
            _imgui.RecreateFontDeviceTexture(_gd);
        }
        else
        {
            Console.WriteLine($"UI font not found at {fontPath}; using scaled default bitmap font.");
            io.FontGlobalScale = UiScale;
        }
    }

    /// <summary>
    /// Tear down and recreate the window/device on a new backend. Experimental:
    /// only selectable from the UI when the user opts in.
    /// </summary>
    private void Recreate(GraphicsBackend backend)
    {
        Console.WriteLine($"Switching backend {_backend} -> {backend} (experimental)");

        _window.Resized -= OnWindowResized;
        _imgui.Dispose();
        _cl.Dispose();
        DisposeDevice();
        _window.Close();

        _backend = backend;
        // ImGuiRenderer creates a fresh ImGui context, resetting the style to
        // defaults, so re-apply the 1.5x scale in CreateResources.
        _styleConfigured = false;
        CreateResources();
        _limiter.Start();
    }

    private void OnWindowResized()
    {
        _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
        _imgui.WindowResized(_window.Width, _window.Height);
    }

    private void DisposeDevice()
    {
        // Arch ships sdl2-compat (SDL3-backed); disposing an OpenGL device there
        // can segfault in SDL's Wayland thread. Let the OS reclaim it instead.
        if (_gd.BackendType != GraphicsBackend.OpenGL)
            _gd.Dispose();
    }

    private void Shutdown()
    {
        if (_shutdownDone || !_resourcesCreated)
            return;

        _shutdownDone = true;

        DetachPointer();
        Disconnect();

        _window.Resized -= OnWindowResized;
        _imgui.Dispose();
        _cl.Dispose();
        DisposeDevice();
        _window.Close();
    }

    private static bool IsSupported(GraphicsBackend backend)
    {
        try
        {
            return GraphicsDevice.IsBackendSupported(backend);
        }
        catch
        {
            return false;
        }
    }

    private static GraphicsBackend DefaultBackend(GraphicsBackend[] available)
    {
        GraphicsBackend[] preference = OperatingSystem.IsWindows()
            ? [GraphicsBackend.Direct3D11, GraphicsBackend.Vulkan, GraphicsBackend.OpenGL]
            : [GraphicsBackend.Vulkan, GraphicsBackend.OpenGL, GraphicsBackend.OpenGLES];

        foreach (GraphicsBackend backend in preference)
        {
            if (available.Contains(backend))
                return backend;
        }

        return available[0];
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Shutdown();
    }
}
