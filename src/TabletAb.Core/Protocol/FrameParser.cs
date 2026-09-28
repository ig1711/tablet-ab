using System.Buffers.Binary;

namespace TabletAb.Core.Protocol;

/// <summary>
/// Decodes one acquisition frame. Protocol v7 (self-describing: the header
/// carries the coil geometry) and v6 (hs611-min-ab) are the primary formats;
/// legacy v1..v5 frames from the previous firmware are still accepted so older
/// captures keep working. A desynchronised stream (short buffer, bad magic or
/// unknown version) yields <c>null</c> rather than a mis-decoded frame.
/// </summary>
public static class FrameParser
{
    /// <summary>Parse using the current monotonic host time.</summary>
    public static Frame? Parse(ReadOnlySpan<byte> data) => Parse(data, MonotonicClock.NowMs());

    /// <summary>Parse with an explicit host timestamp (milliseconds).</summary>
    public static Frame? Parse(ReadOnlySpan<byte> data, double hostTimeMs)
    {
        if (data.Length < 3)
            return null;

        if (data[0] != ProtocolConstants.Magic0 || data[1] != ProtocolConstants.Magic1)
            return null;

        int version = data[2];

        if (version == ProtocolVersion.V7)
            return ParseV7(data, hostTimeMs);

        if (version == ProtocolVersion.V6)
            return ParseV6(data, hostTimeMs);

        if (version is >= ProtocolVersion.V1 and <= ProtocolVersion.V5)
            return ParseLegacy(data, version, hostTimeMs);

        return null;
    }

    private static Frame? ParseV6(ReadOnlySpan<byte> data, double hostTimeMs)
        => ParseV6Fields(data, hostTimeMs, ProtocolVersion.V6, FrameGeometry.V6);

    private static Frame? ParseV7(ReadOnlySpan<byte> data, double hostTimeMs)
    {
        // The geometry block sits at offsets 32/33; a valid v7 frame is at least
        // 36 bytes, so a shorter buffer cannot be one.
        if (data.Length < ProtocolConstants.HeaderLenV7)
            return null;

        int nx = data[ProtocolConstants.GeomNxOffset];
        int ny = data[ProtocolConstants.GeomNyOffset];
        var geometry = FrameGeometry.V7(nx, ny);

        if (data.Length < geometry.FrameLen)
            return null;

        return ParseV6Fields(data, hostTimeMs, ProtocolVersion.V7, geometry);
    }

    private static Frame? ParseV6Fields(ReadOnlySpan<byte> data, double hostTimeMs, int version, FrameGeometry g)
    {
        if (data.Length < g.FrameLen)
            return null;

        var x = new ushort[g.Nx];
        for (int i = 0; i < g.Nx; i++)
            x[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(g.OffAmpX + i * 2, 2));

        var y = new ushort[g.Ny];
        for (int i = 0; i < g.Ny; i++)
            y[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(g.OffAmpY + i * 2, 2));

        int flags = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(ProtocolConstants.OffFlags, 2));
        int adcClock = data[ProtocolConstants.OffAdcClock];
        int windowX = data[ProtocolConstants.OffWindowX];
        int estimator = data[ProtocolConstants.OffEstimator];
        bool windowed = (flags & FrameFlags.Windowed) != 0;
        bool reacquired = (flags & FrameFlags.Reacquired) != 0;
        bool vendorEstimator = estimator == Protocol.Estimator.Vendor;
        bool satRetry = (flags & FrameFlags.SaturateRetry) != 0;
        bool follow = (flags & FrameFlags.FollowPeak) != 0;

        // Synthetic legacy options byte, so version-agnostic consumers keep working.
        int options = ((adcClock & 0x3) << 1)
                      | (windowed ? 0x08 : 0)
                      | (reacquired ? 0x10 : 0)
                      | (vendorEstimator ? 0x20 : 0)
                      | (satRetry ? 0x40 : 0)
                      | (follow ? 0x80 : 0);

        return new Frame
        {
            Version = version,
            Seq = data[3],
            Flags = flags,
            Backend = data[ProtocolConstants.OffBackend],
            Estimator = estimator,
            Frequency = data[ProtocolConstants.OffFrequency],
            Burst = data[ProtocolConstants.OffBurst],
            AdcSamples = data[ProtocolConstants.OffAdcSamples],
            AdcClock = adcClock,
            WindowX = windowX,
            WindowY = data[ProtocolConstants.OffWindowY],
            RampMode = data[ProtocolConstants.OffRampMode],
            RecenterMode = data[ProtocolConstants.OffRecenterMode],
            ScanOrder = data[ProtocolConstants.OffScanOrder],
            Warmup = data[ProtocolConstants.OffWarmup],
            XPeak = data[ProtocolConstants.OffXPeak],
            YPeak = data[ProtocolConstants.OffYPeak],
            DeviceTimeUs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(ProtocolConstants.OffDeviceTimeUs, 4)),
            ScanUs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(ProtocolConstants.OffScanUs, 4)),
            XPos = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(ProtocolConstants.OffXPos, 2)),
            YPos = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(ProtocolConstants.OffYPos, 2)),
            Options = options,
            HostTimeMs = hostTimeMs,
            X = x,
            Y = y,
        };
    }

    private static Frame? ParseLegacy(ReadOnlySpan<byte> data, int version, double hostTimeMs)
    {
        if (data.Length < ProtocolConstants.FrameLenV1)
            return null;

        int header = version switch
        {
            ProtocolVersion.V5 => ProtocolConstants.HeaderLen,
            ProtocolVersion.V4 => ProtocolConstants.HeaderLenV4,
            ProtocolVersion.V3 => ProtocolConstants.HeaderLenV3,
            ProtocolVersion.V2 => ProtocolConstants.HeaderLenV2,
            _ => ProtocolConstants.HeaderLenV1,
        };

        if (data.Length < header + (ProtocolConstants.Nx + ProtocolConstants.Ny) * 2)
            return null;

        bool v2Plus = version >= ProtocolVersion.V2;
        bool v3Plus = version >= ProtocolVersion.V3;
        bool v4Plus = version >= ProtocolVersion.V4;
        bool v5 = version >= ProtocolVersion.V5;

        int options = v2Plus ? data[11] : 0;
        int modeByte = v2Plus ? data[8] : Backend.Software;
        int backend = modeByte & 0x07;
        int rampMode = (modeByte >> 4) & 0x07;
        int windowed = (options & 0x08) != 0 ? 1 : 0;

        var x = new ushort[ProtocolConstants.Nx];
        for (int i = 0; i < ProtocolConstants.Nx; i++)
            x[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(header + i * 2, 2));

        var y = new ushort[ProtocolConstants.Ny];
        for (int i = 0; i < ProtocolConstants.Ny; i++)
            y[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(header + ProtocolConstants.Nx * 2 + i * 2, 2));

        return new Frame
        {
            Version = version,
            Seq = data[3],
            Flags = data[4],
            Backend = backend,
            Estimator = (options & 0x20) != 0 ? Protocol.Estimator.Vendor : Protocol.Estimator.NCentroid,
            Frequency = data[5],
            XPeak = data[6],
            YPeak = data[7],
            RampMode = rampMode,
            RecenterMode = ((data[4] & 0x80) != 0) ? Protocol.Recenter.Sticky : Protocol.Recenter.Edge,
            ScanOrder = ((data[4] & 0x02) != 0) ? Protocol.ScanOrder.Descending : Protocol.ScanOrder.Ascending,
            Burst = v2Plus ? data[9] : 29,
            AdcSamples = v2Plus ? data[10] : 3,
            AdcClock = (options >> 1) & 0x3,
            WindowX = windowed,
            WindowY = windowed,
            Warmup = (data[4] >> 2) & 0x0F,
            Options = options,
            DeviceTimeUs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4)),
            ScanUs = v3Plus ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4)) : 0,
            XPos = v4Plus ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(20, 2)) : 0,
            YPos = v4Plus ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(22, 2)) : 0,
            BuildUs = v5 ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(24, 4)) : 0,
            SendUs = v5 ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(28, 4)) : 0,
            HostTimeMs = hostTimeMs,
            X = x,
            Y = y,
        };
    }
}
