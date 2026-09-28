namespace TabletAb.Core.Protocol;

/// <summary>
/// Protocol-agnostic set of A/B acquisition commands. Two implementations exist:
/// <see cref="CommandBuilderV6"/> for the current hs611-min-ab firmware and
/// <see cref="CommandBuilderV5"/> for the previous DEBUG_MIN firmware. Commands
/// are emitted through a caller-supplied delegate so a single logical setting
/// can expand to more than one wire command on the legacy protocol (e.g. ADC
/// samples + clock, or re-acquire threshold + stride + period).
/// </summary>
public interface ICommandBuilder
{
    /// <summary>Short name for the UI (e.g. "v6").</summary>
    string Name { get; }

    void Ping(Action<byte[]> send);

    void SetFrequency(Action<byte[]> send, int frequency);
    void SetFrequencyArr(Action<byte[]> send, int arr);

    /// <summary>Backend: 0 software, 1 hardware.</summary>
    void SetBackend(Action<byte[]> send, int backend);

    void SetBurst(Action<byte[]> send, int periods);
    void SetSettle(Action<byte[]> send, int a, int b, int c, int d);
    void SetAdc(Action<byte[]> send, int samples, int clockSelect);
    void SetRecovery(Action<byte[]> send, bool enabled);
    void SetWindow(Action<byte[]> send, int xCoils, int yCoils);
    void SetRecenter(Action<byte[]> send, int mode, int? hysteresis, int? persist, int? step, int? deadband);
    void SetScanOrder(Action<byte[]> send, int order);
    void SetEstimator(Action<byte[]> send, int mode);
    void SetLogGaussian(Action<byte[]> send, int axis, int baseline, int a1, int a3);
    void SetCentroidBase(Action<byte[]> send, int baseCounts);
    void SetReacquire(Action<byte[]> send, int threshold, int coarseStride, int periodMs);
    void SetRamp(Action<byte[]> send, int mode, int? primeBurst, int? primeRepeats, int? slope, int? revRadius);
    void SetPacing(Action<byte[]> send, int mode);
    void SetWarmup(Action<byte[]> send, int reads);
    void SetFlatTolerance(Action<byte[]> send, int tolerance);
    void RepeatCoil(Action<byte[]> send, int coil);
}

/// <summary>
/// v6 command builder (the current firmware). Thin adapter over the static
/// <see cref="CommandBuilder"/> so existing tests and callers keep working.
/// </summary>
public sealed class CommandBuilderV6 : ICommandBuilder
{
    public static readonly CommandBuilderV6 Instance = new();

    public string Name => "v6";

    public void Ping(Action<byte[]> send) => send(CommandBuilder.Ping());
    public void SetFrequency(Action<byte[]> send, int frequency) => send(CommandBuilder.SetFrequency(frequency));
    public void SetFrequencyArr(Action<byte[]> send, int arr) => send(CommandBuilder.SetFrequencyArr(arr));
    public void SetBackend(Action<byte[]> send, int backend) => send(CommandBuilder.SetBackend(backend));
    public void SetBurst(Action<byte[]> send, int periods) => send(CommandBuilder.SetBurst(periods));
    public void SetSettle(Action<byte[]> send, int a, int b, int c, int d) => send(CommandBuilder.SetSettle(a, b, c, d));
    public void SetAdc(Action<byte[]> send, int samples, int clockSelect) => send(CommandBuilder.SetAdc(samples, clockSelect));
    public void SetRecovery(Action<byte[]> send, bool enabled) => send(CommandBuilder.SetRecovery(enabled));
    public void SetWindow(Action<byte[]> send, int xCoils, int yCoils) => send(CommandBuilder.SetWindow(xCoils, yCoils));
    public void SetRecenter(Action<byte[]> send, int mode, int? hysteresis, int? persist, int? step, int? deadband)
        => send(CommandBuilder.SetRecenter(mode, hysteresis, persist, step, deadband));
    public void SetScanOrder(Action<byte[]> send, int order) => send(CommandBuilder.SetScanOrder(order));
    public void SetEstimator(Action<byte[]> send, int mode) => send(CommandBuilder.SetEstimator(mode));
    public void SetLogGaussian(Action<byte[]> send, int axis, int baseline, int a1, int a3)
        => send(CommandBuilder.SetLogGaussian(axis, baseline, a1, a3));
    public void SetCentroidBase(Action<byte[]> send, int baseCounts) => send(CommandBuilder.SetCentroidBase(baseCounts));
    public void SetReacquire(Action<byte[]> send, int threshold, int coarseStride, int periodMs)
        => send(CommandBuilder.SetReacquire(threshold, coarseStride, periodMs));
    public void SetRamp(Action<byte[]> send, int mode, int? primeBurst, int? primeRepeats, int? slope, int? revRadius)
        => send(CommandBuilder.SetRamp(mode, primeBurst, primeRepeats, slope, revRadius));
    public void SetPacing(Action<byte[]> send, int mode) => send(CommandBuilder.SetPacing(mode));
    public void SetWarmup(Action<byte[]> send, int reads) => send(CommandBuilder.SetWarmup(reads));
    public void SetFlatTolerance(Action<byte[]> send, int tolerance) => send(CommandBuilder.SetFlatTolerance(tolerance));
    public void RepeatCoil(Action<byte[]> send, int coil) => send(CommandBuilder.RepeatCoil(coil));
}
