using TabletAb.Core.Input;

namespace TabletAb.Input.Tests;

public class ExternalPointerSourceTests
{
    [Fact]
    public void PushBeforeOpen_EmitsNothing()
    {
        var source = new ExternalPointerSource();
        var points = new List<PointerPoint>();
        source.PointReceived += (_, e) => points.Add(e.Point);

        source.Push(50, 25, 100, 50);

        Assert.Empty(points);
        Assert.Equal(0, source.PointsReceived);
    }

    [Fact]
    public void PushAfterOpen_EmitsNormalisedAndRawPoint()
    {
        var source = new ExternalPointerSource();
        source.Open();
        var points = new List<PointerPoint>();
        source.PointReceived += (_, e) => points.Add(e.Point);

        source.Push(50, 25, 100, 50);

        PointerPoint point = Assert.Single(points);
        Assert.Equal(0.5, point.NormalizedX, 6);
        Assert.Equal(0.5, point.NormalizedY, 6);
        Assert.Equal(50f, point.RawX);
        Assert.Equal(25f, point.RawY);
        Assert.Equal(50f, point.MappedX);
        Assert.Equal(25f, point.MappedY);
        Assert.Equal(1f, point.Pressure);
        Assert.Equal(1, source.PointsReceived);
    }

    [Fact]
    public void PushOutOfRangeClientCoords_ClampsNormalized()
    {
        var source = new ExternalPointerSource();
        source.Open();
        var points = new List<PointerPoint>();
        source.PointReceived += (_, e) => points.Add(e.Point);

        source.Push(-10, 200, 100, 50);

        PointerPoint point = Assert.Single(points);
        Assert.Equal(0.0, point.NormalizedX, 6);
        Assert.Equal(1.0, point.NormalizedY, 6);
    }

    [Fact]
    public void PushWithNonPositiveSize_EmitsNothing()
    {
        var source = new ExternalPointerSource();
        source.Open();
        int count = 0;
        source.PointReceived += (_, _) => count++;

        source.Push(10, 10, 0, 50);
        source.Push(10, 10, 100, 0);

        Assert.Equal(0, count);
    }

    [Fact]
    public void CloseStopsEmission()
    {
        var source = new ExternalPointerSource();
        source.Open();
        int count = 0;
        source.PointReceived += (_, _) => count++;

        source.Push(50, 25, 100, 50);
        Assert.Equal(1, count);

        source.Close();

        source.Push(50, 25, 100, 50);
        Assert.Equal(1, count);
        Assert.False(source.IsOpen);
    }
}
