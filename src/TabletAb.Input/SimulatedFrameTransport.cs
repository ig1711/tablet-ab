using System.Diagnostics;
using TabletAb.Core;
using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>
/// Synthetic frame generator used to exercise the app without hardware. Produces
/// one frame per target period on an <see cref="ThreadPriority.AboveNormal"/>
/// thread, with a moving Gaussian bump on each axis so the visualizers see
/// realistic motion. Commands are accepted and ignored.
/// </summary>
public sealed class SimulatedFrameTransport : IFrameSource, ICommandSink
{
    private const double Baseline = 690.0;
    private const double Amplitude = 900.0;
    private const double Sigma = 0.82;

    private readonly object _gate = new();
    private readonly double _rateHz;
    private readonly int _nx;
    private readonly int _ny;
    private readonly int _frameLength;

    private Thread? _thread;
    private volatile bool _running;
    private long _frames;
    private long _errors;
    private long _bytes;
    private double _phase;

    public SimulatedFrameTransport(double rateHz = 1000.0, int nx = ProtocolConstants.Nx, int ny = ProtocolConstants.Ny)
    {
        _rateHz = rateHz;
        _nx = nx;
        _ny = ny;
        _frameLength = FrameGeometry.V7(nx, ny).FrameLen;
    }

    public DeviceKind Kind => DeviceKind.DebugVendor;

    public string DisplayName => $"simulated ({_rateHz:0} Hz)";

    public bool IsOpen { get; private set; }

    public long FramesReceived => Interlocked.Read(ref _frames);

    public long Errors => Interlocked.Read(ref _errors);

    public long BytesReceived => Interlocked.Read(ref _bytes);

    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    // Never raised: the simulator cannot lose a physical device.
#pragma warning disable CS0067
    public event EventHandler<TransportLostEventArgs>? Lost;
#pragma warning restore CS0067

    public void Open()
    {
        lock (_gate)
        {
            if (_running)
                return;

            _running = true;
            IsOpen = true;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "tablet-ab simulator",
                Priority = ThreadPriority.AboveNormal,
            };
            _thread.Start();
        }
    }

    public void Close()
    {
        Thread? thread;
        lock (_gate)
        {
            _running = false;
            IsOpen = false;
            thread = _thread;
            _thread = null;
        }

        thread?.Join(1000);
    }

    public void SendCommand(ReadOnlySpan<byte> command)
    {
        // The simulator has no configurable acquisition; commands are ignored.
    }

    public void Dispose() => Close();

    private void Loop()
    {
        double periodSeconds = 1.0 / _rateHz;
        int periodUs = (int)Math.Round(periodSeconds * 1_000_000);
        var clock = Stopwatch.StartNew();
        double next = 0;
        uint deviceTimeUs = 0;

        while (_running)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (now < next)
            {
                double sleep = next - now;
                if (sleep > 0.0005)
                    Thread.Sleep((int)(sleep * 1000));
                continue;
            }

            next += periodSeconds;
            Publish(deviceTimeUs);
            deviceTimeUs += (uint)periodUs;
            _phase += periodSeconds * 0.4;
        }
    }

    private void Publish(uint deviceTimeUs)
    {
        double xCentre = 20.0 + 15.0 * Math.Sin(_phase);
        double yCentre = 13.0 + 9.0 * Math.Cos(_phase * 0.7);

        var frame = new Frame
        {
            Version = ProtocolVersion.Current,
            Seq = (int)(_frames & 0xFF),
            Flags = FrameFlags.Pen | FrameFlags.Windowed | FrameFlags.FollowPeak | FrameFlags.Sticky,
            Backend = Backend.Hardware,
            Estimator = Estimator.Vendor,
            Frequency = 9,
            Burst = 12,
            AdcSamples = 3,
            AdcClock = 2,
            WindowX = 5,
            WindowY = 5,
            RecenterMode = Recenter.Sticky,
            ScanOrder = ScanOrder.Ascending,
            XPeak = ClampPeak((int)Math.Round(xCentre) + 1, _nx),
            YPeak = ClampPeak((int)Math.Round(yCentre) + 1, _ny),
            DeviceTimeUs = deviceTimeUs,
            ScanUs = 900,
            XPos = (int)((xCentre + 1.0) * 256.0),
            YPos = (int)((yCentre + 1.0) * 256.0),
            HostTimeMs = MonotonicClock.NowMs(),
            X = Bump(xCentre, _nx),
            Y = Bump(yCentre, _ny),
        };

        Interlocked.Increment(ref _frames);
        Interlocked.Add(ref _bytes, _frameLength);
        FrameReceived?.Invoke(this, new FrameReceivedEventArgs(frame));
    }

    private static ushort[] Bump(double centre, int count)
    {
        var values = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            double d = (i - centre) / Sigma;
            double v = Baseline + Amplitude * Math.Exp(-0.5 * d * d);
            values[i] = (ushort)Math.Clamp(v, 0, ushort.MaxValue);
        }
        return values;
    }

    private static int ClampPeak(int peak, int count) => Math.Clamp(peak, 1, count);
}
