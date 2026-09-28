using TabletAb.Core;
using TabletAb.Core.Protocol;

namespace TabletAb.Input.Tests;

/// <summary>Captures every command buffer passed to <see cref="SendCommand"/>.</summary>
internal sealed class RecordingCommandSink : ICommandSink
{
    private readonly List<byte[]> _commands = new();

    public IReadOnlyList<byte[]> Commands => _commands;

    public DeviceKind Kind => DeviceKind.DebugVendor;

    public string DisplayName => "recording";

    public bool IsOpen { get; private set; }

    public void SendCommand(ReadOnlySpan<byte> command) => _commands.Add(command.ToArray());

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void Dispose() => Close();

    public IEnumerable<byte> CommandIds() => _commands.Select(c => c[0]);

    public int Count(byte commandId) => _commands.Count(c => c[0] == commandId);

    public byte[] Single(byte commandId) => _commands.Single(c => c[0] == commandId);
}

public class AcquisitionSettingsTests
{
    private static RecordingCommandSink PushDefaults(AcquisitionSettings? settings = null)
    {
        var sink = new RecordingCommandSink();
        (settings ?? new AcquisitionSettings()).PushAll(sink);
        return sink;
    }

    [Fact]
    public void PushAll_EmitsEveryCommandAsSixtyFourBytes()
    {
        var sink = PushDefaults();

        Assert.NotEmpty(sink.Commands);
        Assert.All(sink.Commands, command => Assert.Equal(ProtocolConstants.CommandLen, command.Length));
    }

    [Fact]
    public void PushAll_DefaultSettings_EmitExpectedCommandIds()
    {
        var sink = PushDefaults();

        byte[] expected =
        {
            ProtocolCommands.SetPacing,
            ProtocolCommands.SetBackend,
            ProtocolCommands.SetBurst,
            ProtocolCommands.SetAdc,
            ProtocolCommands.SetRecovery,
            ProtocolCommands.SetWindow,
            ProtocolCommands.SetReacquire,
            ProtocolCommands.SetEstimator,
            ProtocolCommands.SetCentroidBase,
            ProtocolCommands.SetLogGaussian,
            ProtocolCommands.SetRecenter,
            ProtocolCommands.SetScanOrder,
            ProtocolCommands.SetWarmup,
            ProtocolCommands.SetFlatTolerance,
            ProtocolCommands.SetRamp,
            ProtocolCommands.SetSettle,
            ProtocolCommands.RepeatCoil,
            ProtocolCommands.SetFrequency,
        };

        var ids = sink.CommandIds().ToList();
        foreach (byte id in expected)
            Assert.Contains(id, ids);

        // Both estimator axes are pushed.
        Assert.Equal(2, sink.Count(ProtocolCommands.SetLogGaussian));
        Assert.Contains(sink.Commands, c => c[0] == ProtocolCommands.SetLogGaussian && c[1] == 0);
        Assert.Contains(sink.Commands, c => c[0] == ProtocolCommands.SetLogGaussian && c[1] == 1);

        // Indexed frequency selected by default; the raw ARR override is not sent.
        Assert.Equal(1, sink.Count(ProtocolCommands.SetFrequency));
        Assert.Equal(0, sink.Count(ProtocolCommands.SetFrequencyArr));
    }

    [Fact]
    public void PushAll_UseFrequencyArr_EmitsArrAndSkipsIndex()
    {
        var settings = new AcquisitionSettings { UseFrequencyArr = true, FrequencyArr = 152 };
        var sink = PushDefaults(settings);

        Assert.Equal(0, sink.Count(ProtocolCommands.SetFrequency));

        byte[] arr = sink.Single(ProtocolCommands.SetFrequencyArr);
        Assert.Equal(152, arr[1] | (arr[2] << 8));
    }

    [Fact]
    public void PushAll_SettlePayload_MatchesConfiguredSettles()
    {
        var settings = new AcquisitionSettings { SettleA = 3, SettleB = 5, SettleC = 7, SettleD = 11 };
        byte[] settle = PushDefaults(settings).Single(ProtocolCommands.SetSettle);

        Assert.Equal(3, settle[1]);
        Assert.Equal(5, settle[2]);
        Assert.Equal(7, settle[3]);
        Assert.Equal(11, settle[4]);
    }

    [Fact]
    public void PushAll_WindowPayload_MatchesConfiguredCoils()
    {
        var settings = new AcquisitionSettings { WindowCoilsX = 6, WindowCoilsY = 7 };
        byte[] coils = PushDefaults(settings).Single(ProtocolCommands.SetWindow);

        Assert.Equal(6, coils[1]);
        Assert.Equal(7, coils[2]);
    }

    [Fact]
    public void PushAll_PacingAndBackendPayloads_ReflectSettings()
    {
        var settings = new AcquisitionSettings { Pacing = ProtocolConstants.PacingContinuous, HardwareTiming = false };
        var sink = PushDefaults(settings);

        Assert.Equal(ProtocolConstants.PacingContinuous, sink.Single(ProtocolCommands.SetPacing)[1]);
        Assert.Equal(0, sink.Single(ProtocolCommands.SetBackend)[1]);

        settings.HardwareTiming = true;
        Assert.Equal(1, PushDefaults(settings).Single(ProtocolCommands.SetBackend)[1]);
    }

    [Fact]
    public void PushAll_ChangedSettings_ChangeTheirPayloads()
    {
        var baseline = new AcquisitionSettings();
        byte[] baselineBurst = PushDefaults(baseline).Single(ProtocolCommands.SetBurst);
        byte[] baselineEstimator = PushDefaults(baseline).Single(ProtocolCommands.SetEstimator);
        byte[] baselinePacing = PushDefaults(baseline).Single(ProtocolCommands.SetPacing);

        var changed = new AcquisitionSettings { Burst = 20, Estimator = 3, Pacing = ProtocolConstants.PacingSof };
        var sink = PushDefaults(changed);

        Assert.NotEqual(baselineBurst[1], sink.Single(ProtocolCommands.SetBurst)[1]);
        Assert.NotEqual(baselineEstimator[1], sink.Single(ProtocolCommands.SetEstimator)[1]);
        Assert.NotEqual(baselinePacing[1], sink.Single(ProtocolCommands.SetPacing)[1]);

        Assert.Equal(20, sink.Single(ProtocolCommands.SetBurst)[1]);
        Assert.Equal(3, sink.Single(ProtocolCommands.SetEstimator)[1]);
        Assert.Equal(ProtocolConstants.PacingSof, sink.Single(ProtocolCommands.SetPacing)[1]);
    }

    [Fact]
    public void PushAll_CycleSettle_EmitsSettleCyclesOnV6()
    {
        var settings = new AcquisitionSettings
        {
            UseCycleSettle = true,
            SettleCycA = 100,
            SettleCycB = 200,
            SettleCycC = 300,
            SettleCycD = 400,
        };
        var sink = PushDefaults(settings);

        byte[] b = sink.Single(ProtocolCommands.SetSettleCycles);
        Assert.Equal(100, b[1] | (b[2] << 8));
        Assert.Equal(200, b[3] | (b[4] << 8));
        Assert.Equal(300, b[5] | (b[6] << 8));
        Assert.Equal(400, b[7] | (b[8] << 8));
        Assert.Equal(0, sink.Count(ProtocolCommands.SetSettle));
    }

    [Fact]
    public void PushAll_CycleSettle_V5FallsBackToMicroseconds()
    {
        var settings = new AcquisitionSettings { UseCycleSettle = true };
        var sink = new RecordingCommandSink();
        settings.PushAll(sink, CommandBuilderV5.Instance);

        byte[] settle = sink.Single(0x12); // v5 SET_SETTLE (0x15 is v5 RepeatCoil)
        Assert.Equal(2, settle[1]);    // 144 / 72
        Assert.Equal(2, settle[2]);
        Assert.Equal(14, settle[3]);   // 1008 / 72
        Assert.Equal(2, settle[4]);
    }

    [Fact]
    public void PushAll_V5Protocol_EmitsLegacyIdsAndExpandsCompositeCommands()
    {
        var sink = new RecordingCommandSink();
        new AcquisitionSettings().PushAll(sink, CommandBuilderV5.Instance);

        Assert.NotEmpty(sink.Commands);
        Assert.All(sink.Commands, command => Assert.Equal(ProtocolConstants.CommandLen, command.Length));

        // v5 opcodes for settings that moved/merged in v6.
        Assert.Equal(1, sink.Count(0x01));  // SET_FREQ
        Assert.Equal(1, sink.Count(0x10));  // SET_TIMING (backend)
        Assert.Equal(1, sink.Count(0x1B));  // SET_WINDOW_N (window coils)

        // Composite settings expand to more than one legacy command...
        Assert.Equal(1, sink.Count(0x13));  // SET_ADC_N
        Assert.Equal(1, sink.Count(0x16));  // SET_ADC_CLK
        Assert.Equal(1, sink.Count(0x18));  // SET_REACQ
        Assert.Equal(1, sink.Count(0x19));  // SET_COARSE
        Assert.Equal(1, sink.Count(0x23));  // SET_REACQ_PERIOD

        // ...and v6-only opcodes are never sent on the legacy path.
        Assert.Equal(0, sink.Count(0x07));  // v6 SET_ADC
        Assert.Equal(0, sink.Count(0x09));  // v6 SET_WINDOW
        Assert.Equal(0, sink.Count(0x0F));  // v6 SET_REACQ
    }
}
