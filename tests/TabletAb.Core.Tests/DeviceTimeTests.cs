using TabletAb.Core.Protocol;

namespace TabletAb.Core.Tests;

public class DeviceTimeTests
{
    [Fact]
    public void DeltaUs_OrdinaryDifference()
    {
        Assert.Equal(250, DeviceTime.DeltaUs(100, 350));
        Assert.Equal(0, DeviceTime.DeltaUs(42, 42));
    }

    [Fact]
    public void DeltaUs_ForwardWrap_IsPositive()
    {
        Assert.Equal(32, DeviceTime.DeltaUs(0xFFFFFFF0u, 0x00000010u));
    }

    [Fact]
    public void DeltaUs_ReverseDirection_IsNegative()
    {
        Assert.Equal(-32, DeviceTime.DeltaUs(0x00000010u, 0xFFFFFFF0u));
    }

    [Fact]
    public void DeltaUs_EqualTimestamps_IsZero()
    {
        Assert.Equal(0, DeviceTime.DeltaUs(0xDEADBEEFu, 0xDEADBEEFu));
    }

    [Fact]
    public void DeltaUs_FullHalfRange_StaysSigned()
    {
        Assert.Equal(int.MaxValue, DeviceTime.DeltaUs(0, 0x7FFFFFFFu));
        Assert.Equal(int.MinValue + 1, DeviceTime.DeltaUs(0x7FFFFFFFu, 0));
    }
}
