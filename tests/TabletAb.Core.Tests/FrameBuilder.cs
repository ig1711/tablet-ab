using TabletAb.Core.Protocol;

namespace TabletAb.Core.Tests;

/// <summary>
/// Assembles a raw little-endian v6 wire frame
/// (<c>hs611-min-ab/src/protocol.h</c>).
/// </summary>
public sealed class FrameBuilder
{
    private readonly byte[] _buffer = new byte[ProtocolConstants.FrameLen];

    private int _seq;
    private int _flags;
    private int _backend;
    private int _estimator;
    private int _frequency;
    private int _burst;
    private int _adcSamples;
    private int _adcClock;
    private int _windowX;
    private int _windowY;
    private int _rampMode;
    private int _recenterMode;
    private int _scanOrder;
    private int _warmup;
    private int _xPeak;
    private int _yPeak;
    private uint _deviceTimeUs;
    private uint _scanUs;
    private ushort _xPos;
    private ushort _yPos;
    private readonly ushort[] _x = new ushort[ProtocolConstants.Nx];
    private readonly ushort[] _y = new ushort[ProtocolConstants.Ny];

    public FrameBuilder()
    {
        _buffer[0] = ProtocolConstants.Magic0;
        _buffer[1] = ProtocolConstants.Magic1;
        _buffer[2] = ProtocolVersion.V6;
    }

    public FrameBuilder Seq(int value) { _seq = value; return this; }
    public FrameBuilder Flags(int value) { _flags = value; return this; }
    public FrameBuilder Backend(int value) { _backend = value; return this; }
    public FrameBuilder Estimator(int value) { _estimator = value; return this; }
    public FrameBuilder Frequency(int value) { _frequency = value; return this; }
    public FrameBuilder Burst(int value) { _burst = value; return this; }
    public FrameBuilder Adc(int samples, int clock) { _adcSamples = samples; _adcClock = clock; return this; }
    public FrameBuilder Window(int x, int y) { _windowX = x; _windowY = y; return this; }
    public FrameBuilder RampMode(int value) { _rampMode = value; return this; }
    public FrameBuilder Recenter(int value) { _recenterMode = value; return this; }
    public FrameBuilder ScanOrder(int value) { _scanOrder = value; return this; }
    public FrameBuilder Warmup(int value) { _warmup = value; return this; }
    public FrameBuilder Peaks(int xPeak, int yPeak) { _xPeak = xPeak; _yPeak = yPeak; return this; }
    public FrameBuilder DeviceTimeUs(uint value) { _deviceTimeUs = value; return this; }
    public FrameBuilder ScanUs(uint value) { _scanUs = value; return this; }
    public FrameBuilder Positions(ushort x, ushort y) { _xPos = x; _yPos = y; return this; }
    public FrameBuilder X(int index, ushort value) { _x[index] = value; return this; }
    public FrameBuilder Y(int index, ushort value) { _y[index] = value; return this; }

    public FrameBuilder FillX(Func<int, ushort> value)
    {
        for (int i = 0; i < ProtocolConstants.Nx; i++)
            _x[i] = value(i);
        return this;
    }

    public FrameBuilder FillY(Func<int, ushort> value)
    {
        for (int i = 0; i < ProtocolConstants.Ny; i++)
            _y[i] = value(i);
        return this;
    }

    public byte[] Build()
    {
        _buffer[3] = (byte)_seq;
        WriteU16(4, _flags);
        _buffer[6] = (byte)_backend;
        _buffer[7] = (byte)_estimator;
        WriteU16(8, _xPos);
        WriteU16(10, _yPos);
        _buffer[12] = (byte)_frequency;
        _buffer[13] = (byte)_burst;
        _buffer[14] = (byte)_adcSamples;
        _buffer[15] = (byte)_adcClock;
        _buffer[16] = (byte)_windowX;
        _buffer[17] = (byte)_windowY;
        _buffer[18] = (byte)_rampMode;
        _buffer[19] = (byte)_recenterMode;
        _buffer[20] = (byte)_scanOrder;
        _buffer[21] = (byte)_warmup;
        _buffer[22] = (byte)_xPeak;
        _buffer[23] = (byte)_yPeak;
        WriteU32(24, _deviceTimeUs);
        WriteU32(28, _scanUs);

        for (int i = 0; i < ProtocolConstants.Nx; i++)
            WriteU16(ProtocolConstants.OffAmpX + i * 2, _x[i]);
        for (int i = 0; i < ProtocolConstants.Ny; i++)
            WriteU16(ProtocolConstants.OffAmpY + i * 2, _y[i]);

        return _buffer;
    }

    private void WriteU16(int offset, int value)
    {
        _buffer[offset] = (byte)(value & 0xFF);
        _buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private void WriteU32(int offset, uint value)
    {
        _buffer[offset] = (byte)(value & 0xFF);
        _buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        _buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        _buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
