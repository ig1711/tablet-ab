namespace TabletAb.Core.Protocol;

/// <summary>
/// v5 (legacy DEBUG_MIN) command builder. Same logical settings as v6, but the
/// old opcodes and payloads; composite settings expand to several wire commands.
/// Used automatically when the device streams v5 frames, so the tool can drive
/// the previous firmware without reflashing.
/// </summary>
public sealed class CommandBuilderV5 : ICommandBuilder
{
    public static readonly CommandBuilderV5 Instance = new();

    public string Name => "v5";

    // Legacy opcodes (see hs611-fw src/min/debug_proto.h, protocol v5).
    private const byte PingId = 0x02;
    private const byte SetFrequencyId = 0x01;
    private const byte SetTimingId = 0x10;
    private const byte SetBurstId = 0x11;
    private const byte SetSettleId = 0x12;
    private const byte SetAdcSamplesId = 0x13;
    private const byte SetRecoveryId = 0x14;
    private const byte RepeatCoilId = 0x15;
    private const byte SetAdcClockId = 0x16;
    private const byte SetReacquireThresholdId = 0x18;
    private const byte SetCoarseStrideId = 0x19;
    private const byte SetEstimatorId = 0x1A;
    private const byte SetWindowCoilsId = 0x1B;
    private const byte SetRecentreId = 0x1C;
    private const byte SetScanOrderId = 0x1D;
    private const byte SetWarmupId = 0x1E;
    private const byte SetFlatToleranceId = 0x1F;
    private const byte SetLogGaussianId = 0x20;
    private const byte SetRampId = 0x21;
    private const byte SetCentroidBaseId = 0x22;
    private const byte SetReacquirePeriodId = 0x23;
    private const byte SetFrequencyArrId = 0x24;
    private const byte SetPacingId = 0x25;

    public void Ping(Action<byte[]> send) => send(New(PingId));

    public void SetFrequency(Action<byte[]> send, int frequency)
    {
        byte[] b = New(SetFrequencyId);
        b[1] = (byte)Math.Clamp(frequency, 1, 12);
        send(b);
    }

    public void SetFrequencyArr(Action<byte[]> send, int arr)
    {
        byte[] b = New(SetFrequencyArrId);
        int clamped = arr == 0 ? 0 : Math.Clamp(arr, 100, 400);
        PutU16(b, 1, clamped);
        send(b);
    }

    public void SetBackend(Action<byte[]> send, int backend)
    {
        byte[] b = New(SetTimingId);
        b[1] = (byte)Math.Clamp(backend, 0, 1);
        send(b);
    }

    public void SetBurst(Action<byte[]> send, int periods)
    {
        byte[] b = New(SetBurstId);
        b[1] = (byte)Math.Clamp(periods, 6, 29);
        send(b);
    }

    public void SetSettle(Action<byte[]> send, int a, int b2, int c, int d)
    {
        byte[] b = New(SetSettleId);
        b[1] = ClampByte(a);
        b[2] = ClampByte(b2);
        b[3] = ClampByte(c);
        b[4] = ClampByte(d);
        send(b);
    }

    public void SetAdc(Action<byte[]> send, int samples, int clockSelect)
    {
        byte[] n = New(SetAdcSamplesId);
        n[1] = (byte)Math.Clamp(samples, 1, 7);
        send(n);

        byte[] clk = New(SetAdcClockId);
        clk[1] = (byte)Math.Clamp(clockSelect, 0, 3);
        send(clk);
    }

    public void SetRecovery(Action<byte[]> send, bool enabled)
    {
        byte[] b = New(SetRecoveryId);
        b[1] = (byte)(enabled ? 1 : 0);
        send(b);
    }

    public void SetWindow(Action<byte[]> send, int xCoils, int yCoils)
    {
        byte[] b = New(SetWindowCoilsId);
        b[1] = (byte)Math.Clamp(xCoils, 0, 8);
        b[2] = (byte)Math.Clamp(yCoils, 0, 8);
        send(b);
    }

    public void SetRecenter(Action<byte[]> send, int mode, int? hysteresis, int? persist, int? step, int? deadband)
    {
        byte[] b = New(SetRecentreId);
        b[1] = (byte)Math.Clamp(mode, 0, 3);
        b[2] = (byte)(hysteresis is > 0 ? Math.Min(255, hysteresis.Value) : 0);
        b[3] = (byte)(persist is > 0 ? Math.Min(255, persist.Value) : 0);
        b[4] = (byte)(step is > 0 ? Math.Min(255, step.Value) : 0);
        b[5] = (byte)(deadband is > 0 ? Math.Min(255, deadband.Value) : 0);
        send(b);
    }

    public void SetScanOrder(Action<byte[]> send, int order)
    {
        byte[] b = New(SetScanOrderId);
        b[1] = (byte)Math.Clamp(order, 0, 3);
        send(b);
    }

    public void SetEstimator(Action<byte[]> send, int mode)
    {
        byte[] b = New(SetEstimatorId);
        b[1] = (byte)Math.Clamp(mode, 0, 3);
        send(b);
    }

    public void SetLogGaussian(Action<byte[]> send, int axis, int baseline, int a1, int a3)
    {
        byte[] b = New(SetLogGaussianId);
        b[1] = (byte)(axis != 0 ? 1 : 0);
        PutU16(b, 2, baseline);
        b[4] = (byte)(a1 & 0xFF);
        b[5] = (byte)((a1 >> 8) & 0xFF);
        b[6] = (byte)(a3 & 0xFF);
        b[7] = (byte)((a3 >> 8) & 0xFF);
        send(b);
    }

    public void SetCentroidBase(Action<byte[]> send, int baseCounts)
    {
        byte[] b = New(SetCentroidBaseId);
        PutU16(b, 1, baseCounts);
        send(b);
    }

    public void SetReacquire(Action<byte[]> send, int threshold, int coarseStride, int periodMs)
    {
        byte[] thr = New(SetReacquireThresholdId);
        PutU16(thr, 1, threshold);
        send(thr);

        byte[] stride = New(SetCoarseStrideId);
        stride[1] = (byte)Math.Clamp(coarseStride, 0, 8);
        send(stride);

        byte[] period = New(SetReacquirePeriodId);
        PutU16(period, 1, periodMs);
        send(period);
    }

    public void SetRamp(Action<byte[]> send, int mode, int? primeBurst, int? primeRepeats, int? slope, int? revRadius)
    {
        byte[] b = New(SetRampId);
        b[1] = (byte)Math.Clamp(mode, 0, 7);
        b[2] = (byte)(primeBurst is > 0 ? Math.Min(255, primeBurst.Value) : 0);
        b[3] = (byte)(primeRepeats is > 0 ? Math.Min(32, primeRepeats.Value) : 0);
        int s = Math.Clamp(slope ?? 0, -1000, 1000);
        b[4] = (byte)(s & 0xFF);
        b[5] = (byte)((s >> 8) & 0xFF);
        b[6] = (byte)(revRadius is > 0 ? Math.Min(8, revRadius.Value) : 0);
        send(b);
    }

    public void SetPacing(Action<byte[]> send, int mode)
    {
        byte[] b = New(SetPacingId);
        b[1] = (byte)Math.Clamp(mode, 0, 2);
        send(b);
    }

    public void SetWarmup(Action<byte[]> send, int reads)
    {
        byte[] b = New(SetWarmupId);
        b[1] = (byte)Math.Clamp(reads, 0, 8);
        send(b);
    }

    public void SetFlatTolerance(Action<byte[]> send, int tolerance)
    {
        byte[] b = New(SetFlatToleranceId);
        PutU16(b, 1, tolerance);
        send(b);
    }

    public void RepeatCoil(Action<byte[]> send, int coil)
    {
        byte[] b = New(RepeatCoilId);
        b[1] = (byte)Math.Clamp(coil, 0, ProtocolConstants.Nx);
        send(b);
    }

    private static byte[] New(byte commandId)
    {
        var buffer = new byte[ProtocolConstants.CommandLen];
        buffer[0] = commandId;
        return buffer;
    }

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private static void PutU16(byte[] buffer, int offset, int value)
    {
        ushort v = (ushort)Math.Clamp(value, 0, 65535);
        buffer[offset] = (byte)(v & 0xFF);
        buffer[offset + 1] = (byte)(v >> 8);
    }
}
