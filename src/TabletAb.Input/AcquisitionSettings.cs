using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>
/// The full A/B acquisition configuration, mirroring the web tool's defaults
/// (<c>web/src/lib/state.ts</c>). The panel edits these fields and pushes each
/// change immediately; <see cref="PushAll"/> re-sends the whole set when a
/// device connects so the UI and device agree.
/// </summary>
public sealed class AcquisitionSettings
{
    // --- frequency ---

    /// <summary>Drive frequency index (1..12) when not using a raw ARR.</summary>
    public int FrequencyIndex { get; set; } = 6;

    /// <summary>When true, drive the raw carrier period <see cref="FrequencyArr"/>.</summary>
    public bool UseFrequencyArr { get; set; }

    /// <summary>Raw TIMER1 ARR (72 MHz counts, 100..400); hardware backend only.</summary>
    public int FrequencyArr { get; set; } = 152;

    // --- pacing (phase 4 firmware) ---

    /// <summary>0 SOF, 1 continuous, 2 auto (legacy ramp carry-over).</summary>
    public int Pacing { get; set; } = ProtocolConstants.PacingAuto;

    // --- drive ---

    public bool HardwareTiming { get; set; } = true;

    public int Burst { get; set; } = 12;

    public int RampMode { get; set; }

    public int PrimeBurst { get; set; } = 64;

    public int PrimeRepeats { get; set; } = 4;

    /// <summary>Signed per-mille-per-coil gain for ramp mode 5 (-1000..1000).</summary>
    public int RampSlope { get; set; }

    /// <summary>Targeted-reverse band radius for ramp mode 6 (1..8).</summary>
    public int ReverseRadius { get; set; } = 1;

    // --- ADC ---

    public int AdcSamples { get; set; } = 3;

    public int AdcClock { get; set; } = 2;

    public bool CoilRecovery { get; set; } = true;

    // --- window ---

    public int WindowRadius { get; set; }

    public int WindowCoilsX { get; set; } = 5;

    public int WindowCoilsY { get; set; } = 5;

    public int ReacquireThreshold { get; set; } = 800;

    public int ReacquirePeriodMs { get; set; } = 200;

    public int CoarseStride { get; set; } = 6;

    // --- tracking ---

    public int RecentreMode { get; set; } = 3;

    public int RecentreHyst { get; set; } = 60;

    public int RecentrePersist { get; set; } = 1;

    public int RecentreStep { get; set; } = 1;

    public int RecentreDeadband { get; set; } = 1;

    public int ScanOrder { get; set; }

    public int Warmup { get; set; }

    public int FlatHoldTolerance { get; set; }

    // --- estimator ---

    public int Estimator { get; set; }

    public int CentroidBase { get; set; } = 685;

    public int LogBaseline { get; set; } = 690;

    public int LogA1x { get; set; } = 200;

    public int LogA3x { get; set; } = 224;

    public int LogA1y { get; set; } = 221;

    public int LogA3y { get; set; } = 141;

    // --- diagnostics ---

    public int RepeatCoil { get; set; }

    // --- settles ---

    public int SettleA { get; set; } = 2;

    public int SettleB { get; set; } = 2;

    public int SettleC { get; set; } = 14;

    public int SettleD { get; set; } = 2;

    /// <summary>When true (v6 only), push settles in DWT cycles instead of µs.</summary>
    public bool UseCycleSettle { get; set; }

    // Cycle settles: 72 cycles = 1 µs. Defaults = 2/2/14/2 µs.
    public int SettleCycA { get; set; } = 144;
    public int SettleCycB { get; set; } = 144;
    public int SettleCycC { get; set; } = 1008;
    public int SettleCycD { get; set; } = 144;

    // --- display-only ---

    /// <summary>Freeze the visualizers without stopping the stream.</summary>
    public bool FreezeDisplay { get; set; }

    /// <summary>Send every setting to the device (mirrors the web attach sequence).</summary>
    public void PushAll(ICommandSink sink) => PushAll(sink, CommandBuilderV6.Instance);

    /// <summary>Send every setting using the command set for the connected protocol.</summary>
    public void PushAll(ICommandSink sink, ICommandBuilder commands)
    {
        void send(byte[] command) => sink.SendCommand(command);

        if (UseFrequencyArr)
            commands.SetFrequencyArr(send, FrequencyArr);
        else
            commands.SetFrequency(send, FrequencyIndex);

        commands.SetPacing(send, Pacing);
        commands.SetBackend(send, HardwareTiming ? Backend.Hardware : Backend.Software);
        commands.SetBurst(send, Burst);
        commands.SetAdc(send, AdcSamples, AdcClock);
        commands.SetRecovery(send, CoilRecovery);
        commands.SetWindow(send, WindowCoilsX, WindowCoilsY);
        commands.SetReacquire(send, ReacquireThreshold, CoarseStride, ReacquirePeriodMs);
        commands.SetEstimator(send, Estimator);
        commands.SetCentroidBase(send, CentroidBase);
        commands.SetLogGaussian(send, 0, LogBaseline, LogA1x, LogA3x);
        commands.SetLogGaussian(send, 1, LogBaseline, LogA1y, LogA3y);
        commands.SetRecenter(send, RecentreMode, RecentreHyst, RecentrePersist, RecentreStep, RecentreDeadband);
        commands.SetScanOrder(send, ScanOrder);
        commands.SetWarmup(send, Warmup);
        commands.SetFlatTolerance(send, FlatHoldTolerance);
        commands.SetRamp(send, RampMode, PrimeBurst, PrimeRepeats, RampSlope, ReverseRadius);
        if (UseCycleSettle && commands.SupportsCycleSettle)
            commands.SetSettleCycles(send, SettleCycA, SettleCycB, SettleCycC, SettleCycD);
        else
            commands.SetSettle(send, SettleA, SettleB, SettleC, SettleD);
        commands.RepeatCoil(send, RepeatCoil);
    }
}
