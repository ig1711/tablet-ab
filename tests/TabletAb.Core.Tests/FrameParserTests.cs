using System.Buffers.Binary;
using TabletAb.Core.Protocol;

namespace TabletAb.Core.Tests;

public class FrameParserTests
{
    private const double Host = 1234.5;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(147)]
    public void Parse_TooShort_ReturnsNull(int length)
    {
        Assert.Null(FrameParser.Parse(new byte[length], Host));
    }

    [Fact]
    public void Parse_BadMagic_ReturnsNull()
    {
        byte[] frame = new FrameBuilder().Build();

        byte[] bad0 = (byte[])frame.Clone();
        bad0[0] = 0x00;
        Assert.Null(FrameParser.Parse(bad0, Host));

        byte[] bad1 = (byte[])frame.Clone();
        bad1[1] = 0x00;
        Assert.Null(FrameParser.Parse(bad1, Host));
    }

    [Fact]
    public void Parse_UnknownVersion_ReturnsNull()
    {
        byte[] frame = new FrameBuilder().Build();
        frame[2] = 9;
        Assert.Null(FrameParser.Parse(frame, Host));

        frame[2] = 0;
        Assert.Null(FrameParser.Parse(frame, Host));
    }

    [Fact]
    public void Parse_V6TruncatedPayload_ReturnsNull()
    {
        byte[] frame = new FrameBuilder().Build();

        Assert.Equal(ProtocolConstants.FrameLen, frame.Length);
        Assert.Null(FrameParser.Parse(frame[..(ProtocolConstants.FrameLen - 1)], Host));
        Assert.Null(FrameParser.Parse(frame[..31], Host));
    }

    [Fact]
    public void Parse_V6SyntheticFrame_DecodesEveryField()
    {
        byte[] frame = new FrameBuilder()
            .Seq(0xAB)
            .Flags(FrameFlags.Pen | FrameFlags.Windowed | FrameFlags.Reacquired | FrameFlags.SaturateRetry
                   | FrameFlags.FollowPeak | FrameFlags.Sticky | FrameFlags.Descending | FrameFlags.CarryOver)
            .Backend(Backend.Repeat)
            .Estimator(Estimator.LogGaussian)
            .Frequency(9)
            .Burst(21)
            .Adc(6, 1)
            .Window(5, 4)
            .RampMode(RampMode.TargetedReverse)
            .Recenter(Recenter.Deadband)
            .ScanOrder(ScanOrder.OutsideIn)
            .Warmup(7)
            .Peaks(17, 23)
            .DeviceTimeUs(0x12345678)
            .ScanUs(0x0000ABCD)
            .Positions(0x1357, 0x2468)
            .FillX(i => (ushort)(0x1000 + i))
            .FillY(i => (ushort)(0x2000 + i))
            .Build();

        Frame? parsed = FrameParser.Parse(frame, Host);

        Assert.NotNull(parsed);
        Frame f = parsed!;

        Assert.Equal(ProtocolVersion.V6, f.Version);
        Assert.Equal(0xAB, f.Seq);
        Assert.Equal(ProtocolConstants.OffAmpX, ProtocolConstants.OffAmpY - ProtocolConstants.Nx * 2);
        Assert.Equal(Backend.Repeat, f.Backend);
        Assert.Equal(Backend.Repeat, f.Mode);
        Assert.Equal(Estimator.LogGaussian, f.Estimator);
        Assert.Equal(9, f.Frequency);
        Assert.Equal(21, f.Burst);
        Assert.Equal(6, f.AdcSamples);
        Assert.Equal(1, f.AdcClock);
        Assert.Equal(5, f.WindowX);
        Assert.Equal(4, f.WindowY);
        Assert.Equal(RampMode.TargetedReverse, f.RampMode);
        Assert.Equal(Recenter.Deadband, f.RecenterMode);
        Assert.Equal(ScanOrder.OutsideIn, f.ScanOrder);
        Assert.Equal(7, f.Warmup);
        Assert.Equal(17, f.XPeak);
        Assert.Equal(23, f.YPeak);
        Assert.Equal(0x12345678u, f.DeviceTimeUs);
        Assert.Equal(0x0000ABCDu, f.ScanUs);
        Assert.Equal(0x1357, f.XPos);
        Assert.Equal(0x2468, f.YPos);

        Assert.True(f.PenPresent);
        Assert.True(f.Windowed);
        Assert.True(f.Reacquired);
        Assert.True(f.SaturateRetry);
        Assert.True(f.FollowPeak);
        Assert.True(f.Sticky);
        Assert.True(f.ScanDescending);
        Assert.True(f.CarryOver);

        Assert.Equal(Host, f.HostTimeMs);
        Assert.Equal(ProtocolConstants.Nx, f.X.Length);
        Assert.Equal(ProtocolConstants.Ny, f.Y.Length);
        Assert.Equal(0x1000, f.X[0]);
        Assert.Equal(0x1028, f.X[40]);
        Assert.Equal(0x2000, f.Y[0]);
        Assert.Equal(0x201A, f.Y[26]);
    }

    [Fact]
    public void Parse_V6NoFlags_DerivedFlagsAreFalse()
    {
        byte[] frame = new FrameBuilder().Backend(Backend.Hardware).Build();
        Frame f = FrameParser.Parse(frame, Host)!;

        Assert.False(f.PenPresent);
        Assert.False(f.Windowed);
        Assert.False(f.Reacquired);
        Assert.False(f.SaturateRetry);
        Assert.False(f.FollowPeak);
        Assert.False(f.Sticky);
        Assert.False(f.ScanDescending);
        Assert.False(f.CarryOver);
    }

    [Fact]
    public void Parse_LegacyV5Frame_StillDecodesReadOnly()
    {
        // Minimal v5 frame: header 32, then the same 41+27 amplitude block.
        byte[] frame = new byte[ProtocolConstants.FrameLen];
        frame[0] = ProtocolConstants.Magic0;
        frame[1] = ProtocolConstants.Magic1;
        frame[2] = ProtocolVersion.V5;
        frame[3] = 0x44;
        frame[4] = 0x01;          // pen
        frame[5] = 7;             // frequency
        frame[6] = 11;            // x peak
        frame[7] = 12;            // y peak
        frame[8] = 0x11;          // backend 1 (hardware) | ramp mode 1
        frame[9] = 21;            // burst
        frame[10] = 4;            // adc samples
        frame[11] = 0x28;         // windowed + vendor estimator
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(12, 4), 0x0A0B0C0D);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(16, 4), 0x0000BEEF);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(20, 2), 0x1000);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(22, 2), 0x2000);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(24, 4), 50);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(28, 4), 30);
        for (int i = 0; i < ProtocolConstants.Nx; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(32 + i * 2, 2), (ushort)(i + 1));

        Frame? parsed = FrameParser.Parse(frame, Host);

        Assert.NotNull(parsed);
        Frame f = parsed!;
        Assert.Equal(ProtocolVersion.V5, f.Version);
        Assert.Equal(Backend.Hardware, f.Backend);
        Assert.Equal(RampMode.CarryOver, f.RampMode);
        Assert.Equal(21, f.Burst);
        Assert.Equal(4, f.AdcSamples);
        Assert.True(f.PenPresent);
        Assert.True(f.Windowed);
        Assert.True(f.VendorEstimator);
        Assert.Equal(0x0A0B0C0Du, f.DeviceTimeUs);
        Assert.Equal(0x0000BEEFu, f.ScanUs);
        Assert.Equal(0x1000, f.XPos);
        Assert.Equal(0x2000, f.YPos);
        Assert.Equal(50u, f.BuildUs);
        Assert.Equal(30u, f.SendUs);
        Assert.Equal(1, f.X[0]);
    }
}
