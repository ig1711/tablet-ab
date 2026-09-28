using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using TabletAb.Core;
using TabletAb.Core.Protocol;

namespace TabletAb.Input;

/// <summary>
/// libusb transport for the vendor-class debug firmware (VID:PID 256c:6111).
///
/// Finds the device, claims the vendor-specific (0xFF) interface, then reads
/// fixed-size bulk IN frames on a dedicated <see cref="ThreadPriority.AboveNormal"/>
/// thread, mirroring the web tool's WebUSB loop but natively (no browser
/// scheduling jitter). Commands are written to the bulk OUT endpoint.
///
/// Linux needs the udev rule for 256c:6111 (uaccess / 0660). Windows needs the
/// vendor interface bound to WinUSB (Zadig) and `libusb-1.0.dll` beside the exe.
/// </summary>
public sealed class VendorUsbTransport : IFrameSource, ICommandSink
{
    private readonly VendorUsbOptions _options;
    private readonly object _openGate = new();
    private readonly object _writeGate = new();

    private UsbContext? _context;
    private IUsbDevice? _device;
    private UsbEndpointReader? _reader;
    private UsbEndpointWriter? _writer;
    private byte _interfaceNumber;
    private Thread? _thread;
    private volatile bool _running;

    private long _frames;
    private long _errors;
    private long _bytes;

    public VendorUsbTransport(VendorUsbOptions? options = null)
        => _options = options ?? new VendorUsbOptions();

    public DeviceKind Kind => DeviceKind.DebugVendor;

    public string DisplayName { get; private set; } = "";

    public bool IsOpen { get; private set; }

    public long FramesReceived => Interlocked.Read(ref _frames);

    public long Errors => Interlocked.Read(ref _errors);

    public long BytesReceived => Interlocked.Read(ref _bytes);

    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    public event EventHandler<TransportLostEventArgs>? Lost;

    public void Open()
    {
        lock (_openGate)
        {
            if (_running)
                return;

            try
            {
                _context = new UsbContext();

                IUsbDevice? device = _context.Find(new UsbDeviceFinder
                {
                    Vid = _options.VendorId,
                    Pid = _options.ProductId,
                });

                if (device is null)
                    throw new InvalidOperationException(
                        $"USB device {_options.VendorId:X4}:{_options.ProductId:X4} not found");

                _device = device;
                device.Open();

                if (_options.AutoDetachKernelDriver && device is UsbDevice usbDevice)
                    usbDevice.SetAutoDetachKernelDriver(true);

                // Info (product/serial) is cached on first access, so read it now,
                // after Open() has granted access.
                string product = SafeInfo(() => device.Info.Product);
                DisplayName = string.IsNullOrWhiteSpace(product)
                    ? $"USB {_options.VendorId:X4}:{_options.ProductId:X4}"
                    : $"{product} ({_options.VendorId:X4}:{_options.ProductId:X4})";

                (byte interfaceNumber, byte endpointIn, byte endpointOut, byte configValue) = SelectInterface(device);

                if (device.Configuration != configValue)
                    device.SetConfiguration(configValue);

                device.ClaimInterface(interfaceNumber);
                _interfaceNumber = interfaceNumber;

                _reader = device.OpenEndpointReader((ReadEndpointID)endpointIn, _options.FrameLength);
                if (endpointOut != 0)
                    _writer = device.OpenEndpointWriter((WriteEndpointID)endpointOut, EndpointType.Bulk);

                IsOpen = true;
                _running = true;
                _thread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "tablet-ab vendor reader",
                    Priority = ThreadPriority.AboveNormal,
                };
                _thread.Start();
            }
            catch
            {
                Cleanup();
                throw;
            }
        }
    }

    public void Close()
    {
        // Hold the open gate for the whole teardown so a concurrent Open() can't
        // rebuild a device that this Close is about to destroy.
        lock (_openGate)
        {
            _running = false;
            _thread?.Join(_options.ReadTimeoutMs + 500);
            _thread = null;
            Cleanup();
        }
    }

    public void SendCommand(ReadOnlySpan<byte> command)
    {
        if (command.Length == 0)
            return;

        lock (_writeGate)
        {
            UsbEndpointWriter? writer = _writer;
            if (writer is null || !IsOpen)
                return;

            try
            {
                Error result = writer.Write(command, 0, command.Length, _options.WriteTimeoutMs, out _);
                if (result != Error.Success)
                    Interlocked.Increment(ref _errors);
            }
            catch
            {
                Interlocked.Increment(ref _errors);
            }
        }
    }

    public void Dispose() => Close();

    private void ReadLoop()
    {
        var buffer = new byte[_options.FrameLength];

        while (_running)
        {
            Error result;
            int read;
            try
            {
                result = _reader!.Read(buffer, 0, buffer.Length, _options.ReadTimeoutMs, out read);
            }
            catch (Exception ex)
            {
                if (_running)
                    RaiseLost($"read threw: {ex.Message}", ex);
                return;
            }

            if (!_running)
                return;

            if (result == Error.Success)
            {
                // A successful zero/short transfer (e.g. a stray ZLP) is not fatal;
                // only a full frame is parsed below.
                if (read <= 0)
                    continue;

                Interlocked.Add(ref _bytes, read);
                Frame? frame = FrameParser.Parse(buffer.AsSpan(0, read));
                if (frame is null)
                {
                    Interlocked.Increment(ref _errors);
                    continue;
                }

                Interlocked.Increment(ref _frames);
                try
                {
                    FrameReceived?.Invoke(this, new FrameReceivedEventArgs(frame));
                }
                catch
                {
                    // A misbehaving subscriber must not kill the reader.
                    Interlocked.Increment(ref _errors);
                }
            }
            else if (result == Error.Timeout)
            {
                // Idle gap (pen away / no data); not an error.
            }
            else
            {
                Interlocked.Increment(ref _errors);
                RaiseLost($"bulk read failed: {result}");
                return;
            }
        }
    }

    private void RaiseLost(string reason, Exception? error = null)
    {
        _running = false;
        Lost?.Invoke(this, new TransportLostEventArgs(reason, error));
    }

    private void Cleanup()
    {
        // Serialise against SendCommand: it holds _writeGate while writing, so
        // taking it here guarantees no bulk OUT is in flight when we close.
        lock (_writeGate)
        {
            if (_device is not null)
            {
                try
                {
                    if (_device.IsOpen)
                    {
                        try { _device.ReleaseInterface(_interfaceNumber); }
                        catch { /* best effort */ }
                        _device.Close();
                    }
                }
                catch
                {
                    /* best effort */
                }
            }

            _reader = null;
            _writer = null;
            _device = null;

            _context?.Dispose();
            _context = null;

            IsOpen = false;
        }
    }

    /// <summary>
    /// Prefer the first vendor-specific (0xFF) interface, falling back to the
    /// first interface, and locate its bulk IN/OUT endpoints.
    /// </summary>
    private static (byte interfaceNumber, byte endpointIn, byte endpointOut, byte configValue) SelectInterface(IUsbDevice device)
    {
        UsbConfigInfo config = device.Configs[0];
        UsbInterfaceInfo iface = config.Interfaces[0];

        foreach (UsbConfigInfo candidateConfig in device.Configs)
        {
            foreach (UsbInterfaceInfo candidateInterface in candidateConfig.Interfaces)
            {
                if (candidateInterface.Class == ClassCode.VendorSpec)
                {
                    config = candidateConfig;
                    iface = candidateInterface;
                    goto selected;
                }
            }
        }

    selected:

        byte endpointIn = 0;
        byte endpointOut = 0;

        foreach (UsbEndpointInfo endpoint in iface.Endpoints)
        {
            bool isBulk = (endpoint.Attributes & 0x03) == 0x02;
            if (!isBulk)
                continue;

            if ((endpoint.EndpointAddress & 0x80) != 0)
                endpointIn = (byte)endpoint.EndpointAddress;
            else
                endpointOut = (byte)endpoint.EndpointAddress;
        }

        if (endpointIn == 0)
            throw new InvalidOperationException("interface exposes no bulk IN endpoint");

        return ((byte)iface.Number, endpointIn, endpointOut, (byte)config.ConfigurationValue);
    }

    private static string SafeInfo(Func<string?> getter)
    {
        try
        {
            return getter() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
