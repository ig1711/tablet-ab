using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using OpenTabletDriver;
using OpenTabletDriver.Plugin.Components;
using OpenTabletDriver.Plugin.Tablet;
using TabletAb.Core;
using TabletAb.Core.Input;
using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>
/// Internal release-HID input mode: reads the tablet through OpenTabletDriver
/// 0.6.7 exactly as osu!lazer does (the same library, the same report event),
/// and maps raw reports to cursor points with the OTD area transform.
///
/// Uses OTD's built-in configuration set (no injection), like lazer. OTD 0.6.7
/// already ships a Huion HS611 config for 256c:006F; the release firmware
/// presents the matching device string (index 201, "HUION_T19c_..."), so it
/// matches without a custom config. Detection runs off-thread.
/// </summary>
public sealed class OtdPointerSource : IPointerSource
{
    private Driver? _driver;
    private ServiceProvider? _provider;
    private TabletAreaTransform? _transform;
    private float _maxPressure = 1f;
    private long _points;
    private Task? _detectTask;
    private volatile bool _closing;
    private readonly List<InputDevice> _hooked = new();

    public DeviceKind Kind => DeviceKind.ReleaseHid;

    public string DisplayName { get; private set; } = "OpenTabletDriver (256c:006f)";

    public bool IsOpen { get; private set; }

    public long PointsReceived => Interlocked.Read(ref _points);

    /// <summary>Digitizer raw range (for the raw coordinate view).</summary>
    public Vector2 RawMax { get; private set; } = new(51680f, 32300f);

    /// <summary>Mapped output range in pixels.</summary>
    public Vector2 MappedMax { get; private set; } = new(1920f, 1080f);

    public event EventHandler<PointerPointEventArgs>? PointReceived;

    public void Open()
    {
        if (IsOpen)
            return;

        _closing = false;

        var services = new DriverServiceCollection();
        services.AddSingleton<Driver>();

        _provider = services.BuildServiceProvider();

        Driver driver;
        try
        {
            driver = _provider.GetRequiredService<Driver>();
        }
        catch
        {
            _provider.Dispose();
            _provider = null;
            throw;
        }

        _driver = driver;
        IsOpen = true;
        _driver.TabletsChanged += OnTabletsChanged;

        // Detect is synchronous and probes every config; keep it off the UI thread.
        _detectTask = Task.Run(() =>
        {
            try
            {
                driver.Detect();
                if (!_closing)
                    HookDevices();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OTD detect failed: {ex.Message}");
            }
        });
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _closing = true;

        // Let an in-flight Detect finish before tearing the driver down.
        try
        {
            _detectTask?.Wait(2000);
        }
        catch
        {
            /* ignore */
        }

        _detectTask = null;

        if (_driver is not null)
            _driver.TabletsChanged -= OnTabletsChanged;

        UnhookDevices();

        Driver? driver = _driver;
        _driver = null;
        driver?.Dispose();

        _provider?.Dispose();
        _provider = null;
        _transform = null;
    }

    public void Dispose() => Close();

    private void OnTabletsChanged(object? sender, IEnumerable<TabletReference> tablets)
    {
        if (!_closing)
            HookDevices();
    }

    private void HookDevices()
    {
        if (_driver is null)
            return;

        UnhookDevices();

        foreach (InputDeviceTree tree in _driver.InputDevices)
        {
            var digitizer = tree.Properties.Specifications.Digitizer;
            DisplayName = tree.CreateReference().Properties.Name;
            _maxPressure = Math.Max(1f, (float)tree.Properties.Specifications.Pen.MaxPressure);
            RawMax = new Vector2(digitizer.MaxX, digitizer.MaxY);

            // Default mapping: full tablet -> a 1920x1080 output area, centred.
            _transform = new TabletAreaTransform(
                new DigitizerArea(digitizer.Width, digitizer.Height, digitizer.MaxX, digitizer.MaxY),
                TabletArea.Full(digitizer.Width, digitizer.Height),
                TabletArea.Full(1920f, 1080f));

            foreach (InputDevice device in tree.InputDevices)
            {
                device.Report += OnReport;
                _hooked.Add(device);
            }
        }
    }

    private void UnhookDevices()
    {
        foreach (InputDevice device in _hooked)
            device.Report -= OnReport;

        _hooked.Clear();
    }

    private void OnReport(object? sender, IDeviceReport report)
    {
        if (report is not ITabletReport tablet)
            return;

        Vector2 raw = tablet.Position;
        TabletAreaTransform? transform = _transform;

        Vector2 mapped = transform?.TransformClamped(raw) ?? raw;
        Vector2 normalized = transform?.Normalized(raw) ?? new Vector2(0.5f, 0.5f);

        float pressure = Math.Clamp(tablet.Pressure / _maxPressure, 0f, 1f);

        var point = new PointerPoint(
            normalized.X, normalized.Y,
            raw.X, raw.Y,
            mapped.X, mapped.Y,
            pressure,
            MonotonicClock.NowMs());

        Interlocked.Increment(ref _points);
        PointReceived?.Invoke(this, new PointerPointEventArgs(point));
    }
}
