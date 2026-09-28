using TabletAb.Core.Input;
using TabletAb.Core.Protocol;

namespace TabletAb.Input.Tests;

public class DebugFramePointerSourceTests
{
    private static Frame BuildFrame(int xPos, int yPos, int xPeak, int yPeak, ushort amplitude)
        => new()
        {
            XPos = xPos,
            YPos = yPos,
            XPeak = xPeak,
            YPeak = yPeak,
            HostTimeMs = 1234.5,
            X = BuildAmplitudes(ProtocolConstants.Nx, amplitude),
            Y = BuildAmplitudes(ProtocolConstants.Ny, amplitude),
        };

    private static Frame MidTableFrame(ushort amplitude = 800)
    {
        int xPos = (int)(256.0 * (1.0 + 0.5 * (ProtocolConstants.Nx - 1)));
        int yPos = (int)(256.0 * (1.0 + 0.5 * (ProtocolConstants.Ny - 1)));
        return BuildFrame(xPos, yPos, 1, 1, amplitude);
    }

    private static ushort[] BuildAmplitudes(int count, ushort value)
    {
        var values = new ushort[count];
        values[0] = value;
        return values;
    }

    [Fact]
    public void ValidMidTableFrame_EmitsSinglePointAtCentre()
    {
        var frames = new FakeFrameSource();
        var source = new DebugFramePointerSource(frames);
        var points = new List<PointerPoint>();
        source.PointReceived += (_, e) => points.Add(e.Point);

        frames.Raise(MidTableFrame());

        PointerPoint point = Assert.Single(points);
        Assert.Equal(0.5, point.NormalizedX, 4);
        Assert.Equal(0.5, point.NormalizedY, 4);
        Assert.Equal(21f, point.RawX);
        Assert.Equal(14f, point.RawY);
        Assert.True(Math.Abs(point.TimestampMs - 1234.5) < 1e-6);
        Assert.Equal(1, source.PointsReceived);
    }

    [Fact]
    public void FrameWithZeroPosition_EmitsNothing()
    {
        var frames = new FakeFrameSource();
        var source = new DebugFramePointerSource(frames);
        int count = 0;
        source.PointReceived += (_, _) => count++;

        frames.Raise(BuildFrame(0, 0, 1, 1, 800));

        Assert.Equal(0, count);
        Assert.Equal(0, source.PointsReceived);
    }

    [Fact]
    public void FrameWithZeroAmplitude_EmitsNothing()
    {
        var frames = new FakeFrameSource();
        var source = new DebugFramePointerSource(frames);
        int count = 0;
        source.PointReceived += (_, _) => count++;

        frames.Raise(MidTableFrame(amplitude: 0));

        Assert.Equal(0, count);
        Assert.Equal(0, source.PointsReceived);
    }

    [Fact]
    public void MissingPeak_EmitsNothing()
    {
        var frames = new FakeFrameSource();
        var source = new DebugFramePointerSource(frames);
        int count = 0;
        source.PointReceived += (_, _) => count++;

        int xPos = (int)(256.0 * (1.0 + 0.5 * (ProtocolConstants.Nx - 1)));
        frames.Raise(BuildFrame(xPos, 100, 0, 1, 800));

        Assert.Equal(0, count);
    }

    [Fact]
    public void AfterDispose_FurtherFramesEmitNothing()
    {
        var frames = new FakeFrameSource();
        var source = new DebugFramePointerSource(frames);
        int count = 0;
        source.PointReceived += (_, _) => count++;

        frames.Raise(MidTableFrame());
        Assert.Equal(1, count);

        source.Dispose();

        frames.Raise(MidTableFrame());
        Assert.Equal(1, count);
        Assert.Equal(1, source.PointsReceived);
    }
}
