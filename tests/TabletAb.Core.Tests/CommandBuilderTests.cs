using TabletAb.Core.Protocol;

namespace TabletAb.Core.Tests;

public class CommandBuilderTests
{
    /// <summary>
    /// Asserts the 64-byte buffer carries <paramref name="id"/> at [0], the given
    /// payload immediately after, and zero padding for the rest.
    /// </summary>
    private static void AssertCommand(byte[] buffer, byte id, params byte[] payload)
    {
        Assert.Equal(ProtocolConstants.CommandLen, buffer.Length);
        Assert.Equal(id, buffer[0]);
        Assert.True(payload.Length <= buffer.Length - 1);
        for (int i = 0; i < payload.Length; i++)
            Assert.Equal(payload[i], buffer[1 + i]);
        for (int i = 1 + payload.Length; i < buffer.Length; i++)
            Assert.Equal(0, buffer[i]);
    }

    [Fact]
    public void Ping_CarriesOnlyCommandId()
    {
        AssertCommand(CommandBuilder.Ping(), ProtocolCommands.Ping);
    }

    [Fact]
    public void SetFrequency_ClampsTo1Through12()
    {
        AssertCommand(CommandBuilder.SetFrequency(1), ProtocolCommands.SetFrequency, 1);
        AssertCommand(CommandBuilder.SetFrequency(0), ProtocolCommands.SetFrequency, 1);
        AssertCommand(CommandBuilder.SetFrequency(99), ProtocolCommands.SetFrequency, 12);
        AssertCommand(CommandBuilder.SetFrequency(7), ProtocolCommands.SetFrequency, 7);
    }

    [Fact]
    public void SetFrequencyArr_WritesLittleEndianU16AndClamps()
    {
        AssertCommand(CommandBuilder.SetFrequencyArr(200), ProtocolCommands.SetFrequencyArr, 0xC8, 0x00);
        AssertCommand(CommandBuilder.SetFrequencyArr(0), ProtocolCommands.SetFrequencyArr, 0x00, 0x00);
        AssertCommand(CommandBuilder.SetFrequencyArr(-1), ProtocolCommands.SetFrequencyArr, 0x64, 0x00);
        AssertCommand(CommandBuilder.SetFrequencyArr(0xFFFF), ProtocolCommands.SetFrequencyArr, 0x90, 0x01);
    }

    [Fact]
    public void SetBackend_ClampsToSoftwareOrHardware()
    {
        AssertCommand(CommandBuilder.SetBackend(Backend.Software), ProtocolCommands.SetBackend, 0x00);
        AssertCommand(CommandBuilder.SetBackend(Backend.Hardware), ProtocolCommands.SetBackend, 0x01);
        AssertCommand(CommandBuilder.SetBackend(9), ProtocolCommands.SetBackend, 0x01);
    }

    [Fact]
    public void SetTiming_MapsBooleanToBackend()
    {
        AssertCommand(CommandBuilder.SetTiming(false), ProtocolCommands.SetBackend, 0x00);
        AssertCommand(CommandBuilder.SetTiming(true), ProtocolCommands.SetBackend, 0x01);
    }

    [Fact]
    public void SetBurst_ClampsTo6Through32()
    {
        AssertCommand(CommandBuilder.SetBurst(6), ProtocolCommands.SetBurst, 6);
        AssertCommand(CommandBuilder.SetBurst(0), ProtocolCommands.SetBurst, 6);
        AssertCommand(CommandBuilder.SetBurst(99), ProtocolCommands.SetBurst, 32);
        AssertCommand(CommandBuilder.SetBurst(20), ProtocolCommands.SetBurst, 20);
    }

    [Fact]
    public void SetSettle_ClampsEachSiteToByteRange()
    {
        AssertCommand(CommandBuilder.SetSettle(1, 2, 3, 4), ProtocolCommands.SetSettle, 1, 2, 3, 4);
        AssertCommand(CommandBuilder.SetSettle(-5, 0, 255, 300), ProtocolCommands.SetSettle, 0, 0, 255, 255);
    }

    [Fact]
    public void SetSettleCycles_WritesFourLittleEndianU16()
    {
        AssertCommand(CommandBuilder.SetSettleCycles(0x1234, 0x0056, 0xABCD, 0x00FF),
            ProtocolCommands.SetSettleCycles,
            0x34, 0x12, 0x56, 0x00, 0xCD, 0xAB, 0xFF, 0x00);
        AssertCommand(CommandBuilder.SetSettleCycles(0, 0, 0, 0),
            ProtocolCommands.SetSettleCycles, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void SetAdc_WritesSamplesAndClockClamped()
    {
        AssertCommand(CommandBuilder.SetAdc(1, 0), ProtocolCommands.SetAdc, 1, 0);
        AssertCommand(CommandBuilder.SetAdc(0, 9), ProtocolCommands.SetAdc, 1, 3);
        AssertCommand(CommandBuilder.SetAdc(5, 2), ProtocolCommands.SetAdc, 5, 2);
    }

    [Fact]
    public void SetRecovery_MapsBooleanToOneByte()
    {
        AssertCommand(CommandBuilder.SetRecovery(false), ProtocolCommands.SetRecovery, 0x00);
        AssertCommand(CommandBuilder.SetRecovery(true), ProtocolCommands.SetRecovery, 0x01);
    }

    [Fact]
    public void SetWindow_ClampsEachAxisTo0Through8()
    {
        AssertCommand(CommandBuilder.SetWindow(6, 4), ProtocolCommands.SetWindow, 6, 4);
        AssertCommand(CommandBuilder.SetWindow(-1, 9), ProtocolCommands.SetWindow, 0, 8);
        AssertCommand(CommandBuilder.SetWindow(0, 0), ProtocolCommands.SetWindow, 0, 0);
    }

    [Fact]
    public void SetRecenter_ClampsModeAndZeroesNonPositiveOptionals()
    {
        AssertCommand(CommandBuilder.SetRecenter(2, 60, 1, 1, 3), ProtocolCommands.SetRecenter, 2, 60, 1, 1, 3);
        AssertCommand(CommandBuilder.SetRecenter(9), ProtocolCommands.SetRecenter, 3, 0, 0, 0, 0);
        AssertCommand(CommandBuilder.SetRecenter(1, 0, -4, null, 0), ProtocolCommands.SetRecenter, 1, 0, 0, 0, 0);
        AssertCommand(CommandBuilder.SetRecenter(0, 300, 300, 300, 300), ProtocolCommands.SetRecenter, 0, 255, 255, 255, 255);
    }

    [Fact]
    public void SetScanOrder_ClampsTo0Through3()
    {
        AssertCommand(CommandBuilder.SetScanOrder(0), ProtocolCommands.SetScanOrder, 0);
        AssertCommand(CommandBuilder.SetScanOrder(99), ProtocolCommands.SetScanOrder, 3);
        AssertCommand(CommandBuilder.SetScanOrder(2), ProtocolCommands.SetScanOrder, 2);
    }

    [Fact]
    public void SetEstimator_ClampsTo0Through3()
    {
        AssertCommand(CommandBuilder.SetEstimator(0), ProtocolCommands.SetEstimator, 0);
        AssertCommand(CommandBuilder.SetEstimator(99), ProtocolCommands.SetEstimator, 3);
        AssertCommand(CommandBuilder.SetEstimator(2), ProtocolCommands.SetEstimator, 2);
    }

    [Fact]
    public void SetWarmup_ClampsTo0Through8()
    {
        AssertCommand(CommandBuilder.SetWarmup(0), ProtocolCommands.SetWarmup, 0);
        AssertCommand(CommandBuilder.SetWarmup(99), ProtocolCommands.SetWarmup, 8);
        AssertCommand(CommandBuilder.SetWarmup(7), ProtocolCommands.SetWarmup, 7);
    }

    [Fact]
    public void SetFlatTolerance_WritesLittleEndianU16()
    {
        AssertCommand(CommandBuilder.SetFlatTolerance(0x1234), ProtocolCommands.SetFlatTolerance, 0x34, 0x12);
        AssertCommand(CommandBuilder.SetFlatTolerance(0), ProtocolCommands.SetFlatTolerance, 0x00, 0x00);
    }

    [Fact]
    public void SetLogGaussian_WritesAxisBaselineAndSignedQ8Bytes()
    {
        AssertCommand(
            CommandBuilder.SetLogGaussian(1, 0x1234, -256, 224),
            ProtocolCommands.SetLogGaussian,
            0x01, 0x34, 0x12, 0x00, 0xFF, 0xE0, 0x00);

        AssertCommand(
            CommandBuilder.SetLogGaussian(0, 0x0000, 0, 0),
            ProtocolCommands.SetLogGaussian,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00);
    }

    [Fact]
    public void SetCentroidBase_WritesLittleEndianU16()
    {
        AssertCommand(CommandBuilder.SetCentroidBase(0x1234), ProtocolCommands.SetCentroidBase, 0x34, 0x12);
        AssertCommand(CommandBuilder.SetCentroidBase(0), ProtocolCommands.SetCentroidBase, 0x00, 0x00);
    }

    [Fact]
    public void SetReacquire_WritesThresholdStrideAndPeriod()
    {
        AssertCommand(
            CommandBuilder.SetReacquire(0x1234, 6, 0x00C8),
            ProtocolCommands.SetReacquire,
            0x34, 0x12, 6, 0xC8, 0x00);

        AssertCommand(CommandBuilder.SetReacquire(0, 99, 0), ProtocolCommands.SetReacquire, 0, 0, 8, 0, 0);
    }

    [Fact]
    public void SetRamp_ClampsMode_ParamsAndWritesSignedSlope()
    {
        AssertCommand(CommandBuilder.SetRamp(7), ProtocolCommands.SetRamp, 7, 0, 0, 0x00, 0x00, 0);
        AssertCommand(CommandBuilder.SetRamp(2, 300, 99, 1000, 99), ProtocolCommands.SetRamp, 2, 0xFF, 32, 0xE8, 0x03, 8);
        AssertCommand(CommandBuilder.SetRamp(3, 0, -1, 0, 0), ProtocolCommands.SetRamp, 3, 0, 0, 0x00, 0x00, 0);
        AssertCommand(CommandBuilder.SetRamp(5, null, null, -1, null), ProtocolCommands.SetRamp, 5, 0, 0, 0xFF, 0xFF, 0);
        AssertCommand(CommandBuilder.SetRamp(5, null, null, -5000, null), ProtocolCommands.SetRamp, 5, 0, 0, 0x18, 0xFC, 0);
        AssertCommand(CommandBuilder.SetRamp(99), ProtocolCommands.SetRamp, 7, 0, 0, 0x00, 0x00, 0);
    }

    [Fact]
    public void SetPacing_ClampsTo0Through2()
    {
        AssertCommand(CommandBuilder.SetPacing(0), ProtocolCommands.SetPacing, 0);
        AssertCommand(CommandBuilder.SetPacing(1), ProtocolCommands.SetPacing, 1);
        AssertCommand(CommandBuilder.SetPacing(2), ProtocolCommands.SetPacing, 2);
        AssertCommand(CommandBuilder.SetPacing(-1), ProtocolCommands.SetPacing, 0);
        AssertCommand(CommandBuilder.SetPacing(3), ProtocolCommands.SetPacing, 2);
    }

    [Fact]
    public void RepeatCoil_ClampsToZeroThroughNx()
    {
        AssertCommand(CommandBuilder.RepeatCoil(0), ProtocolCommands.RepeatCoil, 0);
        AssertCommand(CommandBuilder.RepeatCoil(-1), ProtocolCommands.RepeatCoil, 0);
        AssertCommand(CommandBuilder.RepeatCoil(99), ProtocolCommands.RepeatCoil, (byte)ProtocolConstants.Nx);
        AssertCommand(CommandBuilder.RepeatCoil(12), ProtocolCommands.RepeatCoil, 12);
    }

    [Fact]
    public void EveryBuilder_Returns64ByteBufferWithExpectedCommandId()
    {
        var commands = new (byte Id, byte[] Buffer)[]
        {
            (ProtocolCommands.Ping, CommandBuilder.Ping()),
            (ProtocolCommands.SetFrequency, CommandBuilder.SetFrequency(1)),
            (ProtocolCommands.SetFrequencyArr, CommandBuilder.SetFrequencyArr(200)),
            (ProtocolCommands.SetBackend, CommandBuilder.SetBackend(1)),
            (ProtocolCommands.SetBurst, CommandBuilder.SetBurst(10)),
            (ProtocolCommands.SetSettle, CommandBuilder.SetSettle(1, 2, 3, 4)),
            (ProtocolCommands.SetAdc, CommandBuilder.SetAdc(3, 2)),
            (ProtocolCommands.SetRecovery, CommandBuilder.SetRecovery(true)),
            (ProtocolCommands.SetWindow, CommandBuilder.SetWindow(5, 5)),
            (ProtocolCommands.SetRecenter, CommandBuilder.SetRecenter(2)),
            (ProtocolCommands.SetScanOrder, CommandBuilder.SetScanOrder(1)),
            (ProtocolCommands.SetEstimator, CommandBuilder.SetEstimator(1)),
            (ProtocolCommands.SetLogGaussian, CommandBuilder.SetLogGaussian(0, 1, 2, 3)),
            (ProtocolCommands.SetCentroidBase, CommandBuilder.SetCentroidBase(5)),
            (ProtocolCommands.SetReacquire, CommandBuilder.SetReacquire(100, 3, 200)),
            (ProtocolCommands.SetRamp, CommandBuilder.SetRamp(1)),
            (ProtocolCommands.SetPacing, CommandBuilder.SetPacing(1)),
            (ProtocolCommands.SetWarmup, CommandBuilder.SetWarmup(2)),
            (ProtocolCommands.SetFlatTolerance, CommandBuilder.SetFlatTolerance(5)),
            (ProtocolCommands.RepeatCoil, CommandBuilder.RepeatCoil(5)),
            (ProtocolCommands.SetSettleCycles, CommandBuilder.SetSettleCycles(144, 144, 1008, 144)),
        };

        foreach ((byte id, byte[] buffer) in commands)
        {
            Assert.Equal(ProtocolConstants.CommandLen, buffer.Length);
            Assert.Equal(id, buffer[0]);
        }
    }
}
