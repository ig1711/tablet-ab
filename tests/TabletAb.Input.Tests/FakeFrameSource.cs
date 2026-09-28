using TabletAb.Core;
using TabletAb.Core.Protocol;

namespace TabletAb.Input.Tests;

/// <summary>Test double for <see cref="IFrameSource"/> that lets a test raise frames.</summary>
internal sealed class FakeFrameSource : IFrameSource
{
    public DeviceKind Kind => DeviceKind.DebugVendor;

    public string DisplayName => "fake frames";

    public bool IsOpen { get; private set; }

    public long FramesReceived { get; private set; }

    public long Errors { get; private set; }

    public long BytesReceived { get; private set; }

    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;

#pragma warning disable CS0067 // Never raised by the fake.
    public event EventHandler<TransportLostEventArgs>? Lost;
#pragma warning restore CS0067

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void Dispose() => Close();

    public void Raise(Frame frame)
    {
        FramesReceived++;
        FrameReceived?.Invoke(this, new FrameReceivedEventArgs(frame));
    }
}
