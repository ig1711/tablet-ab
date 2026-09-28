namespace TabletAb.Core.Protocol;

/// <summary>
/// Builds the 64-byte host -&gt; device command buffers for protocol v6
/// (<c>hs611-min-ab/src/protocol.h</c>). Every buffer is zero-padded to
/// <see cref="ProtocolConstants.CommandLen"/>.
/// </summary>
public static class CommandBuilder
{
    /// <summary>0x01 — ping; the device replies with a frame.</summary>
    public static byte[] Ping() => New(ProtocolCommands.Ping);

    /// <summary>0x02 — drive frequency index (1..12). Clears any raw ARR override.</summary>
    public static byte[] SetFrequency(int frequency)
    {
        byte[] b = New(ProtocolCommands.SetFrequency);
        b[1] = (byte)Math.Clamp(frequency, 1, 12);
        return b;
    }

    /// <summary>
    /// 0x03 — raw carrier period override (TIMER1 ARR in 72 MHz counts, clamped
    /// to 100..400). 0 restores the <see cref="SetFrequency"/> index table.
    /// Hardware backend only.
    /// </summary>
    public static byte[] SetFrequencyArr(int arr)
    {
        byte[] b = New(ProtocolCommands.SetFrequencyArr);
        int clamped = arr == 0 ? 0 : Math.Clamp(arr, 100, 400);
        PutU16(b, 1, clamped);
        return b;
    }

    /// <summary>0x04 — timing backend: 0 software, 1 hardware (see <see cref="Backend"/>).</summary>
    public static byte[] SetBackend(int backend)
    {
        byte[] b = New(ProtocolCommands.SetBackend);
        b[1] = (byte)Math.Clamp(backend, 0, 1);
        return b;
    }

    /// <summary>0x04 — timing backend convenience: false = software, true = hardware.</summary>
    public static byte[] SetTiming(bool hardware) => SetBackend(hardware ? Backend.Hardware : Backend.Software);

    /// <summary>0x05 — read burst periods (6..32).</summary>
    public static byte[] SetBurst(int periods)
    {
        byte[] b = New(ProtocolCommands.SetBurst);
        b[1] = (byte)Math.Clamp(periods, 6, 32);
        return b;
    }

    /// <summary>0x06 — settle sites A/B/C/D microseconds (0..255 each).</summary>
    public static byte[] SetSettle(int a, int b, int c, int d)
    {
        byte[] buf = New(ProtocolCommands.SetSettle);
        buf[1] = ClampByte(a, 0, 255);
        buf[2] = ClampByte(b, 0, 255);
        buf[3] = ClampByte(c, 0, 255);
        buf[4] = ClampByte(d, 0, 255);
        return buf;
    }

    /// <summary>
    /// 0x15 — settle sites A/B/C/D in DWT cycles (72 cycles = 1 us), each a u16
    /// LE (0..65535). Sub-microsecond precision for the AFE settle optimum.
    /// </summary>
    public static byte[] SetSettleCycles(int a, int b, int c, int d)
    {
        byte[] buf = New(ProtocolCommands.SetSettleCycles);
        PutU16(buf, 1, a);
        PutU16(buf, 3, b);
        PutU16(buf, 5, c);
        PutU16(buf, 7, d);
        return buf;
    }

    /// <summary>0x07 — ADC samples per channel (1..7) and clock prescaler select (0..3).</summary>
    public static byte[] SetAdc(int samples, int clockSelect)
    {
        byte[] b = New(ProtocolCommands.SetAdc);
        b[1] = (byte)Math.Clamp(samples, 1, 7);
        b[2] = (byte)Math.Clamp(clockSelect, 0, 3);
        return b;
    }

    /// <summary>0x08 — site-D coil recovery on/off.</summary>
    public static byte[] SetRecovery(bool enabled)
    {
        byte[] b = New(ProtocolCommands.SetRecovery);
        b[1] = (byte)(enabled ? 1 : 0);
        return b;
    }

    /// <summary>0x09 — per-axis window coil counts (0..8 each; 0 = full scan).</summary>
    public static byte[] SetWindow(int x, int y)
    {
        byte[] b = New(ProtocolCommands.SetWindow);
        b[1] = ClampByte(x, 0, 8);
        b[2] = ClampByte(y, 0, 8);
        return b;
    }

    /// <summary>
    /// 0x0A — window re-centre mode plus optional parameters. Null or
    /// non-positive parameters are sent as 0, meaning "keep the device's current
    /// value" (except <paramref name="step"/>, where 0 means full snap).
    /// </summary>
    public static byte[] SetRecenter(int mode, int? hysteresis = null, int? persist = null, int? step = null, int? deadband = null)
    {
        byte[] b = New(ProtocolCommands.SetRecenter);
        b[1] = (byte)Math.Clamp(mode, 0, 3);
        b[2] = (byte)(hysteresis is > 0 ? Math.Min(255, hysteresis.Value) : 0);
        b[3] = (byte)(persist is > 0 ? Math.Min(255, persist.Value) : 0);
        b[4] = (byte)(step is > 0 ? Math.Min(255, step.Value) : 0);
        b[5] = (byte)(deadband is > 0 ? Math.Min(255, deadband.Value) : 0);
        return b;
    }

    /// <summary>0x0B — scan order: 0 ascending, 1 descending, 2 outside-in, 3 center-out.</summary>
    public static byte[] SetScanOrder(int order)
    {
        byte[] b = New(ProtocolCommands.SetScanOrder);
        b[1] = (byte)Math.Clamp(order, 0, 3);
        return b;
    }

    /// <summary>0x0C — sub-pixel estimator (0..3).</summary>
    public static byte[] SetEstimator(int mode)
    {
        byte[] b = New(ProtocolCommands.SetEstimator);
        b[1] = (byte)Math.Clamp(mode, 0, 3);
        return b;
    }

    /// <summary>
    /// 0x0D — 3-point log-Gaussian estimator parameters for one axis: baseline
    /// (u16 LE) and signed Q8 coefficients a1/a3.
    /// </summary>
    public static byte[] SetLogGaussian(int axis, int baseline, int a1, int a3)
    {
        byte[] b = New(ProtocolCommands.SetLogGaussian);
        b[1] = (byte)(axis != 0 ? 1 : 0);
        PutU16(b, 2, baseline);
        b[4] = (byte)(a1 & 0xFF);
        b[5] = (byte)((a1 >> 8) & 0xFF);
        b[6] = (byte)(a3 & 0xFF);
        b[7] = (byte)((a3 >> 8) & 0xFF);
        return b;
    }

    /// <summary>0x0E — additive noise base (counts) shared by the N-centroid and Gaomon estimators.</summary>
    public static byte[] SetCentroidBase(int baseCounts)
    {
        byte[] b = New(ProtocolCommands.SetCentroidBase);
        PutU16(b, 1, baseCounts);
        return b;
    }

    /// <summary>
    /// 0x0F — re-acquire policy: threshold (u16), coarse stride (0 = full grid,
    /// 2..8) and the minimum time between attempts while not tracking (ms).
    /// </summary>
    public static byte[] SetReacquire(int threshold, int coarseStride, int periodMs)
    {
        byte[] b = New(ProtocolCommands.SetReacquire);
        PutU16(b, 1, threshold);
        b[3] = (byte)Math.Clamp(coarseStride, 0, 8);
        PutU16(b, 4, periodMs);
        return b;
    }

    /// <summary>
    /// 0x10 — ramp-mitigation mode plus parameters. Modes are mutually
    /// exclusive; 0 restores the baseline. Null or non-positive optional
    /// parameters (except <paramref name="slope"/>) mean "keep current".
    /// </summary>
    public static byte[] SetRamp(
        int mode,
        int? primeBurst = null,
        int? primeRepeats = null,
        int? slope = null,
        int? revRadius = null)
    {
        byte[] b = New(ProtocolCommands.SetRamp);
        b[1] = (byte)Math.Clamp(mode, 0, 7);
        b[2] = (byte)(primeBurst is > 0 ? Math.Min(255, primeBurst.Value) : 0);
        b[3] = (byte)(primeRepeats is > 0 ? Math.Min(32, primeRepeats.Value) : 0);
        int s = Math.Clamp(slope ?? 0, -1000, 1000);
        b[4] = (byte)(s & 0xFF);
        b[5] = (byte)((s >> 8) & 0xFF);
        b[6] = (byte)(revRadius is > 0 ? Math.Min(8, revRadius.Value) : 0);
        return b;
    }

    /// <summary>
    /// 0x11 — acquisition pacing: 0 SOF-paced, 1 continuous/free-run, 2 auto
    /// (free-run only in the carry-over ramp mode). Independent of
    /// <see cref="SetRamp"/>.
    /// </summary>
    public static byte[] SetPacing(int mode)
    {
        byte[] b = New(ProtocolCommands.SetPacing);
        b[1] = (byte)Math.Clamp(mode, 0, 2);
        return b;
    }

    /// <summary>0x12 — discarded warm-up reads before each axis scan (0..8).</summary>
    public static byte[] SetWarmup(int reads)
    {
        byte[] b = New(ProtocolCommands.SetWarmup);
        b[1] = (byte)Math.Clamp(reads, 0, 8);
        return b;
    }

    /// <summary>0x13 — flat-top hold tolerance in counts (u16 LE; 0 = off).</summary>
    public static byte[] SetFlatTolerance(int tolerance)
    {
        byte[] b = New(ProtocolCommands.SetFlatTolerance);
        PutU16(b, 1, tolerance);
        return b;
    }

    /// <summary>0x14 — repeat one axis-B coil (0 = off, 1..41).</summary>
    public static byte[] RepeatCoil(int coil)
    {
        byte[] b = New(ProtocolCommands.RepeatCoil);
        b[1] = (byte)Math.Clamp(coil, 0, ProtocolConstants.Nx);
        return b;
    }

    private static byte[] New(byte commandId)
    {
        var buffer = new byte[ProtocolConstants.CommandLen];
        buffer[0] = commandId;
        return buffer;
    }

    private static byte ClampByte(int value, int min, int max) => (byte)Math.Clamp(value, min, max);

    private static void PutU16(byte[] buffer, int offset, int value)
    {
        ushort v = (ushort)Math.Clamp(value, 0, 65535);
        buffer[offset] = (byte)(v & 0xFF);
        buffer[offset + 1] = (byte)(v >> 8);
    }
}
