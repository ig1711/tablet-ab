using TabletAb.Core;
using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>Carries one decoded frame from a transport's reader thread.</summary>
/// <remarks>
/// Handlers run on the transport's reader thread, not the UI thread; marshal
/// before touching UI state.
/// </remarks>
public sealed class FrameReceivedEventArgs : EventArgs
{
    public FrameReceivedEventArgs(Frame frame) => Frame = frame;

    public Frame Frame { get; }
}

/// <summary>Raised when a transport can no longer read from the device.</summary>
public sealed class TransportLostEventArgs : EventArgs
{
    public TransportLostEventArgs(string reason, Exception? error = null)
    {
        Reason = reason;
        Error = error;
    }

    public string Reason { get; }

    public Exception? Error { get; }
}

/// <summary>
/// A transport that streams decoded acquisition frames (vendor debug and
/// release-vendor builds).
/// </summary>
public interface IFrameSource : IDeviceTransport
{
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    event EventHandler<TransportLostEventArgs>? Lost;

    long FramesReceived { get; }

    long Errors { get; }

    long BytesReceived { get; }
}

/// <summary>Sends host-to-device command buffers (vendor debug builds).</summary>
public interface ICommandSink : IDeviceTransport
{
    /// <summary>
    /// Send one command buffer (typically 64 bytes, zero-padded). Implementations
    /// must be safe to call from a non-UI thread.
    /// </summary>
    void SendCommand(ReadOnlySpan<byte> command);
}
