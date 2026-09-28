using TabletAb.Core.Protocol;

namespace TabletAb.Core.Tests;

public class ProtocolConstantsTests
{
    [Fact]
    public void FrameLayout_MatchesFirmwareV6()
    {
        Assert.Equal(168, ProtocolConstants.FrameLen);
        Assert.Equal(168, ProtocolConstants.FrameLenV5);
        Assert.Equal(160, ProtocolConstants.FrameLenV4);
        Assert.Equal(156, ProtocolConstants.FrameLenV3);
        Assert.Equal(152, ProtocolConstants.FrameLenV2);
        Assert.Equal(148, ProtocolConstants.FrameLenV1);

        Assert.Equal(32, ProtocolConstants.HeaderLen);
        Assert.Equal(ProtocolConstants.HeaderLen + (ProtocolConstants.Nx + ProtocolConstants.Ny) * 2, ProtocolConstants.FrameLen);
    }

    [Fact]
    public void FrameLayout_MatchesFirmwareV7()
    {
        Assert.Equal(36, ProtocolConstants.HeaderLenV7);
        Assert.Equal(32, ProtocolConstants.GeomNxOffset);
        Assert.Equal(33, ProtocolConstants.GeomNyOffset);
        Assert.Equal(256, ProtocolConstants.MaxFrameLen);

        Assert.Equal(172, FrameGeometry.V7(41, 27).FrameLen);
        Assert.Equal(134, FrameGeometry.V7(30, 19).FrameLen);
        Assert.Equal(118, FrameGeometry.V7(41, 27).OffAmpY);
        Assert.Equal(36, FrameGeometry.V7(41, 27).OffAmpX);
        Assert.Equal(168, FrameGeometry.V6.FrameLen);
        Assert.Equal(114, FrameGeometry.V6.OffAmpY);
    }

    [Fact]
    public void FieldOffsets_MatchFirmwareV6()
    {
        Assert.Equal(4, ProtocolConstants.OffFlags);
        Assert.Equal(6, ProtocolConstants.OffBackend);
        Assert.Equal(7, ProtocolConstants.OffEstimator);
        Assert.Equal(8, ProtocolConstants.OffXPos);
        Assert.Equal(10, ProtocolConstants.OffYPos);
        Assert.Equal(12, ProtocolConstants.OffFrequency);
        Assert.Equal(13, ProtocolConstants.OffBurst);
        Assert.Equal(14, ProtocolConstants.OffAdcSamples);
        Assert.Equal(15, ProtocolConstants.OffAdcClock);
        Assert.Equal(16, ProtocolConstants.OffWindowX);
        Assert.Equal(17, ProtocolConstants.OffWindowY);
        Assert.Equal(18, ProtocolConstants.OffRampMode);
        Assert.Equal(19, ProtocolConstants.OffRecenterMode);
        Assert.Equal(20, ProtocolConstants.OffScanOrder);
        Assert.Equal(21, ProtocolConstants.OffWarmup);
        Assert.Equal(22, ProtocolConstants.OffXPeak);
        Assert.Equal(23, ProtocolConstants.OffYPeak);
        Assert.Equal(24, ProtocolConstants.OffDeviceTimeUs);
        Assert.Equal(28, ProtocolConstants.OffScanUs);
        Assert.Equal(32, ProtocolConstants.OffAmpX);
        Assert.Equal(114, ProtocolConstants.OffAmpY);
    }

    [Fact]
    public void CoilAndCommandCounts_MatchFirmware()
    {
        Assert.Equal(41, ProtocolConstants.Nx);
        Assert.Equal(27, ProtocolConstants.Ny);
        Assert.Equal(68, ProtocolConstants.NCoils);
        Assert.Equal(64, ProtocolConstants.CommandLen);
        Assert.Equal(100, ProtocolConstants.NearThreshold);
    }

    [Fact]
    public void MagicBackendAndFlags_MatchFirmware()
    {
        Assert.Equal(0x48, ProtocolConstants.Magic0);
        Assert.Equal(0x53, ProtocolConstants.Magic1);
        Assert.Equal(0, Backend.Software);
        Assert.Equal(1, Backend.Hardware);
        Assert.Equal(2, Backend.Repeat);

        Assert.Equal(0x0001, FrameFlags.Pen);
        Assert.Equal(0x0002, FrameFlags.Windowed);
        Assert.Equal(0x0004, FrameFlags.Reacquired);
        Assert.Equal(0x0008, FrameFlags.SaturateRetry);
        Assert.Equal(0x0010, FrameFlags.FollowPeak);
        Assert.Equal(0x0020, FrameFlags.Sticky);
        Assert.Equal(0x0040, FrameFlags.Descending);
        Assert.Equal(0x0080, FrameFlags.CarryOver);
    }

    [Fact]
    public void Versions_AreContiguousAndCurrentIsV7()
    {
        Assert.Equal(1, ProtocolVersion.V1);
        Assert.Equal(2, ProtocolVersion.V2);
        Assert.Equal(3, ProtocolVersion.V3);
        Assert.Equal(4, ProtocolVersion.V4);
        Assert.Equal(5, ProtocolVersion.V5);
        Assert.Equal(6, ProtocolVersion.V6);
        Assert.Equal(7, ProtocolVersion.V7);
        Assert.Equal(ProtocolVersion.V7, ProtocolVersion.Current);
    }

    [Fact]
    public void CommandIds_MatchFirmwareV6()
    {
        Assert.Equal(0x01, ProtocolCommands.Ping);
        Assert.Equal(0x02, ProtocolCommands.SetFrequency);
        Assert.Equal(0x03, ProtocolCommands.SetFrequencyArr);
        Assert.Equal(0x04, ProtocolCommands.SetBackend);
        Assert.Equal(0x05, ProtocolCommands.SetBurst);
        Assert.Equal(0x06, ProtocolCommands.SetSettle);
        Assert.Equal(0x07, ProtocolCommands.SetAdc);
        Assert.Equal(0x08, ProtocolCommands.SetRecovery);
        Assert.Equal(0x09, ProtocolCommands.SetWindow);
        Assert.Equal(0x0A, ProtocolCommands.SetRecenter);
        Assert.Equal(0x0B, ProtocolCommands.SetScanOrder);
        Assert.Equal(0x0C, ProtocolCommands.SetEstimator);
        Assert.Equal(0x0D, ProtocolCommands.SetLogGaussian);
        Assert.Equal(0x0E, ProtocolCommands.SetCentroidBase);
        Assert.Equal(0x0F, ProtocolCommands.SetReacquire);
        Assert.Equal(0x10, ProtocolCommands.SetRamp);
        Assert.Equal(0x11, ProtocolCommands.SetPacing);
        Assert.Equal(0x12, ProtocolCommands.SetWarmup);
        Assert.Equal(0x13, ProtocolCommands.SetFlatTolerance);
        Assert.Equal(0x14, ProtocolCommands.RepeatCoil);
        Assert.Equal(0x15, ProtocolCommands.SetSettleCycles);
    }

    [Fact]
    public void PacingModes_MatchFirmware()
    {
        Assert.Equal(0, ProtocolConstants.PacingSof);
        Assert.Equal(1, ProtocolConstants.PacingContinuous);
        Assert.Equal(2, ProtocolConstants.PacingAuto);
    }
}
