using TabletAb.Core.Input;

namespace TabletAb.Core.Tests;

public class PointerTrailTests
{
    private static PointerPoint Point(float rawX, double timestampMs = 0)
        => new(0, 0, rawX, 0, 0, 0, 0, timestampMs);

    [Fact]
    public void EmptyTrail_HasNoPoints()
    {
        var trail = new PointerTrail();

        Assert.Equal(0, trail.Count);
        Assert.Empty(trail.Snapshot());
    }

    [Fact]
    public void Snapshot_ReturnsNewestDisplayCountPoints_OldestFirst()
    {
        var trail = new PointerTrail { DisplayCount = 3 };
        for (int i = 0; i < 5; i++)
            trail.Add(Point(i, timestampMs: i));

        Assert.Equal(3, trail.Count);

        var snap = trail.Snapshot();
        Assert.Equal(new[] { 2f, 3f, 4f }, snap.Select(p => p.RawX).ToArray());
        Assert.Equal(new[] { 2d, 3d, 4d }, snap.Select(p => p.TimestampMs).ToArray());
    }

    [Fact]
    public void DisplayCount_IsClampedToValidRange()
    {
        var trail = new PointerTrail();

        trail.DisplayCount = 0;
        Assert.Equal(1, trail.DisplayCount);

        trail.DisplayCount = int.MaxValue;
        Assert.Equal(PointerTrail.MaxCapacity, trail.DisplayCount);

        trail.DisplayCount = -100;
        Assert.Equal(1, trail.DisplayCount);
    }

    [Fact]
    public void Snapshot_ReturnsOnlyDisplayCountNewest_WhenFewerThanCapacityStored()
    {
        var trail = new PointerTrail { DisplayCount = 4 };
        for (int i = 0; i < 10; i++)
            trail.Add(Point(i));

        var snap = trail.Snapshot();

        Assert.Equal(4, snap.Length);
        Assert.Equal(4, trail.Count);
        Assert.Equal(new[] { 6f, 7f, 8f, 9f }, snap.Select(p => p.RawX).ToArray());
    }

    [Fact]
    public void Snapshot_KeepsRingOrderAfterWrapping()
    {
        var trail = new PointerTrail { DisplayCount = PointerTrail.MaxCapacity };
        int total = PointerTrail.MaxCapacity + 10;
        for (int i = 0; i < total; i++)
            trail.Add(Point(i));

        var snap = trail.Snapshot();

        Assert.Equal(PointerTrail.MaxCapacity, trail.Count);
        Assert.Equal(PointerTrail.MaxCapacity, snap.Length);

        Assert.Equal(10f, snap[0].RawX);
        Assert.Equal(total - 1, (int)snap[^1].RawX);
        for (int i = 1; i < snap.Length; i++)
            Assert.Equal(snap[i - 1].RawX + 1, snap[i].RawX);
    }

    [Fact]
    public void Snapshot_ReportsStableAbsoluteIndexForEachPoint()
    {
        var trail = new PointerTrail { DisplayCount = 3 };
        for (int i = 0; i < 5; i++)
            trail.Add(Point(i));

        var snap = trail.Snapshot(out long totalAdded);

        Assert.Equal(5, totalAdded);
        // result[0] is the oldest shown (raw 2), so its absolute index is 2.
        Assert.Equal(2, totalAdded - snap.Length);
    }

    [Fact]
    public void Snapshot_AbsoluteIndexStaysStableAsWindowSlides()
    {
        var trail = new PointerTrail { DisplayCount = 3 };
        trail.Add(Point(0));
        trail.Add(Point(1));
        trail.Add(Point(2));

        var before = trail.Snapshot(out long beforeTotal);
        // The point with raw 1 is at absolute index 1.
        int beforeIndex = -1;
        for (int i = 0; i < before.Length; i++)
        {
            if (before[i].RawX == 1f)
                beforeIndex = (int)(beforeTotal - before.Length + i);
        }
        Assert.Equal(1, beforeIndex);

        trail.Add(Point(3));
        trail.Add(Point(4));
        var after = trail.Snapshot(out long afterTotal);

        int afterIndex = -1;
        for (int i = 0; i < after.Length; i++)
        {
            if (after[i].RawX == 3f)
                afterIndex = (int)(afterTotal - after.Length + i);
        }
        Assert.Equal(3, afterIndex);
        // The raw-1 point has left the window, but its index was 1 both when it
        // was the oldest and forever after; the crosshair pattern never swaps.
        Assert.Equal(5, afterTotal);
    }

    [Fact]
    public void TotalAdded_CountsEveryAdd_AndResetsOnClear()
    {
        var trail = new PointerTrail { DisplayCount = 2 };
        for (int i = 0; i < 6; i++)
            trail.Add(Point(i));

        Assert.Equal(6, trail.TotalAdded);

        trail.Clear();

        Assert.Equal(0, trail.TotalAdded);
    }

    [Fact]
    public void Clear_EmptiesTheTrail()
    {
        var trail = new PointerTrail { DisplayCount = 10 };
        for (int i = 0; i < 20; i++)
            trail.Add(Point(i));
        Assert.Equal(10, trail.Count);

        trail.Clear();

        Assert.Equal(0, trail.Count);
        Assert.Empty(trail.Snapshot());

        trail.Add(Point(42));
        Assert.Equal(1, trail.Count);
        Assert.Equal(42f, trail.Snapshot()[0].RawX);
    }

    [Fact]
    public async Task ConcurrentAddAndSnapshot_DoesNotThrowOrOverflow()
    {
        var trail = new PointerTrail { DisplayCount = 256 };
        using var cts = new CancellationTokenSource();

        var writers = Enumerable.Range(0, 4).Select(w => Task.Run(() =>
        {
            for (int i = 0; i < 5000; i++)
                trail.Add(Point(i, timestampMs: w));
        })).ToArray();

        var reader = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var snap = trail.Snapshot();
                int count = trail.Count;
                Assert.True(snap.Length <= count,
                    $"snapshot {snap.Length} exceeded count {count}");
            }
        });

        await Task.WhenAll(writers);
        cts.Cancel();
        await reader;
    }
}
