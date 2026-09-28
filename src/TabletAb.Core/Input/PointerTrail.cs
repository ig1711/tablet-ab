namespace TabletAb.Core.Input;

/// <summary>
/// Thread-safe ring buffer of recent cursor points. Points are added from a
/// pointer source's thread and a snapshot is read once per frame by the
/// renderer. <see cref="DisplayCount"/> caps how many of the newest points are
/// returned, so the trail length can be changed without reallocating.
/// </summary>
public sealed class PointerTrail
{
    /// <summary>Hard upper bound on retained points (independent of the visible trail).</summary>
    public const int MaxCapacity = 8192;

    private readonly PointerPoint[] _buffer = new PointerPoint[MaxCapacity];
    private readonly object _gate = new();

    private int _head;   // next write index
    private int _count;  // total stored (<= MaxCapacity)
    private int _display = 220;
    private long _totalAdded; // absolute number of points ever added

    /// <summary>
    /// Monotonic count of every point ever added (not capped by capacity).
    /// Together with a snapshot it gives each point a stable identity, so
    /// per-point styling does not change as the window slides.
    /// </summary>
    public long TotalAdded
    {
        get
        {
            lock (_gate)
                return _totalAdded;
        }
    }

    /// <summary>Number of newest points returned by <see cref="Snapshot"/> (1..MaxCapacity).</summary>
    public int DisplayCount
    {
        get => _display;
        set => _display = Math.Clamp(value, 1, MaxCapacity);
    }

    /// <summary>Number of points a snapshot will currently return.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
                return Math.Min(_count, _display);
        }
    }

    public void Add(PointerPoint point)
    {
        lock (_gate)
        {
            _buffer[_head] = point;
            _head = (_head + 1) % MaxCapacity;
            if (_count < MaxCapacity)
                _count++;
            _totalAdded++;
        }
    }

    /// <summary>Newest <see cref="Count"/> points, oldest first.</summary>
    public PointerPoint[] Snapshot()
    {
        lock (_gate)
            return SnapshotLocked(out _);
    }

    /// <summary>
    /// Newest <see cref="Count"/> points, oldest first, together with the
    /// absolute index of the newest point. The absolute index of
    /// <c>result[i]</c> is <c>totalAdded - result.Length + i</c>, which is
    /// stable for the lifetime of the point.
    /// </summary>
    public PointerPoint[] Snapshot(out long totalAdded)
    {
        lock (_gate)
            return SnapshotLocked(out totalAdded);
    }

    private PointerPoint[] SnapshotLocked(out long totalAdded)
    {
        totalAdded = _totalAdded;
        int n = Math.Min(_count, _display);
        var result = new PointerPoint[n];
        for (int i = 0; i < n; i++)
        {
            int index = (_head - n + i + MaxCapacity) % MaxCapacity;
            result[i] = _buffer[index];
        }
        return result;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _head = 0;
            _count = 0;
            _totalAdded = 0;
        }
    }
}
