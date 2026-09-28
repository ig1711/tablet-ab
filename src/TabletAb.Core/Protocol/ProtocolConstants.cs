namespace TabletAb.Core.Protocol;

/// <summary>
/// Wire-protocol constants for the v6 A/B firmware
/// (<c>../hs611-min-ab/src/protocol.h</c>), plus the legacy v1..v5 frame sizes so
/// older captures and the previous firmware still parse.
///
/// v6 frame (168 bytes, little-endian):
/// <code>
///   off  size  field
///   0    2     magic 'H','S'
///   2    1     version (6)
///   3    1     seq
///   4    2     flags (FrameFlags)
///   6    1     backend (Backend*)
///   7    1     estimator (Estimator*)
///   8    2     x position, 1/256 channel (1-based)
///   10   2     y position, 1/256 channel (1-based)
///   12   1     drive frequency index (1..12)
///   13   1     burst periods (6..32)
///   14   1     adc samples per channel (1..7)
///   15   1     adc clock select (0..3)
///   16   1     x window coils (0 = full scan)
///   17   1     y window coils (0 = full scan)
///   18   1     ramp-mitigation mode (RampMode*)
///   19   1     re-centre mode (Recenter*)
///   20   1     scan order (ScanOrder*)
///   21   1     warm-up reads per axis
///   22   1     x peak loop (1..41, 0 = none)
///   23   1     y peak loop (1..27, 0 = none)
///   24   4     device time us (u32 LE)
///   28   4     scan duration us
///   32   82    x amplitudes: 41 x u16 LE
///   114  54    y amplitudes: 27 x u16 LE
/// </code>
/// </summary>
public static class ProtocolConstants
{
    public const int Nx = 41;
    public const int Ny = 27;
    public const int NCoils = Nx + Ny;

    public const int HeaderLen = 32;
    public const int FrameLen = HeaderLen + Nx * 2 + Ny * 2; // 168

    // Legacy headers (v1..v5), kept for the read path.
    public const int HeaderLenV4 = 24;
    public const int HeaderLenV3 = 20;
    public const int HeaderLenV2 = 16;
    public const int HeaderLenV1 = 12;

    public const int FrameLenV5 = FrameLen; // v5 header is also 32
    public const int FrameLenV4 = HeaderLenV4 + Nx * 2 + Ny * 2; // 160
    public const int FrameLenV3 = HeaderLenV3 + Nx * 2 + Ny * 2; // 156
    public const int FrameLenV2 = HeaderLenV2 + Nx * 2 + Ny * 2; // 152
    public const int FrameLenV1 = HeaderLenV1 + Nx * 2 + Ny * 2; // 148

    public const int CommandLen = 64;

    public const byte Magic0 = 0x48; // 'H'
    public const byte Magic1 = 0x53; // 'S'

    /// <summary>Peak amplitude above which the frame reports pen coupling.</summary>
    public const int NearThreshold = 100;

    // ---- v6 frame field offsets ------------------------------------------
    public const int OffFlags = 4;
    public const int OffBackend = 6;
    public const int OffEstimator = 7;
    public const int OffXPos = 8;
    public const int OffYPos = 10;
    public const int OffFrequency = 12;
    public const int OffBurst = 13;
    public const int OffAdcSamples = 14;
    public const int OffAdcClock = 15;
    public const int OffWindowX = 16;
    public const int OffWindowY = 17;
    public const int OffRampMode = 18;
    public const int OffRecenterMode = 19;
    public const int OffScanOrder = 20;
    public const int OffWarmup = 21;
    public const int OffXPeak = 22;
    public const int OffYPeak = 23;
    public const int OffDeviceTimeUs = 24;
    public const int OffScanUs = 28;
    public const int OffAmpX = 32;
    public const int OffAmpY = OffAmpX + Nx * 2; // 114

    // Backwards-compatible backend aliases (frame offset 6).
    public const int ModeSoftware = Backend.Software;
    public const int ModeHardware = Backend.Hardware;
    public const int ModeRepeat = Backend.Repeat;

    // Backwards-compatible pacing aliases.
    public const int PacingSof = Pacing.Sof;
    public const int PacingContinuous = Pacing.Continuous;
    public const int PacingAuto = Pacing.Auto;
}

/// <summary>v6 frame flag bits (u16 at offset 4).</summary>
public static class FrameFlags
{
    public const int Pen = 0x0001;
    public const int Windowed = 0x0002;
    public const int Reacquired = 0x0004;
    public const int SaturateRetry = 0x0008;
    public const int FollowPeak = 0x0010;
    public const int Sticky = 0x0020;
    public const int Descending = 0x0040;
    public const int CarryOver = 0x0080;
}

/// <summary>Acquisition backend (v6 frame offset 6).</summary>
public static class Backend
{
    public const int Software = 0;
    public const int Hardware = 1;
    public const int Repeat = 2;
}

/// <summary>Sub-pixel estimator (v6 frame offset 7); mirrors the firmware.</summary>
public static class Estimator
{
    public const int NCentroid = 0;
    public const int Vendor = 1;
    public const int Gaomon = 2;
    public const int LogGaussian = 3;
}

/// <summary>Re-centre modes (v6 frame offset 19).</summary>
public static class Recenter
{
    public const int Edge = 0;
    public const int Follow = 1;
    public const int Sticky = 2;
    public const int Deadband = 3;
}

/// <summary>Scan orders (v6 frame offset 20).</summary>
public static class ScanOrder
{
    public const int Ascending = 0;
    public const int Descending = 1;
    public const int OutsideIn = 2;
    public const int CenterOut = 3;
}

/// <summary>Ramp-mitigation modes (v6 frame offset 18).</summary>
public static class RampMode
{
    public const int None = 0;
    public const int CarryOver = 1;
    public const int PrimeWindow = 2;
    public const int ReadSteady = 3;
    public const int Bidirectional = 4;
    public const int Slope = 5;
    public const int TargetedReverse = 6;
    public const int FrameAverage = 7;
}

/// <summary>Pacing modes (SET_PACING).</summary>
public static class Pacing
{
    public const int Sof = 0;
    public const int Continuous = 1;
    public const int Auto = 2;
}

/// <summary>Protocol versions understood by <see cref="FrameParser"/>.</summary>
public static class ProtocolVersion
{
    public const int V1 = 1;
    public const int V2 = 2;
    public const int V3 = 3;
    public const int V4 = 4;
    public const int V5 = 5;
    public const int V6 = 6;

    /// <summary>Version emitted by the current (hs611-min-ab) firmware.</summary>
    public const int Current = V6;
}

/// <summary>Host -&gt; device command ids for protocol v6 (first byte of the buffer).</summary>
public static class ProtocolCommands
{
    public const byte Ping = 0x01;
    public const byte SetFrequency = 0x02;
    public const byte SetFrequencyArr = 0x03;
    public const byte SetBackend = 0x04;
    public const byte SetBurst = 0x05;
    public const byte SetSettle = 0x06;
    public const byte SetAdc = 0x07;
    public const byte SetRecovery = 0x08;
    public const byte SetWindow = 0x09;
    public const byte SetRecenter = 0x0A;
    public const byte SetScanOrder = 0x0B;
    public const byte SetEstimator = 0x0C;
    public const byte SetLogGaussian = 0x0D;
    public const byte SetCentroidBase = 0x0E;
    public const byte SetReacquire = 0x0F;
    public const byte SetRamp = 0x10;
    public const byte SetPacing = 0x11;
    public const byte SetWarmup = 0x12;
    public const byte SetFlatTolerance = 0x13;
    public const byte RepeatCoil = 0x14;
}
