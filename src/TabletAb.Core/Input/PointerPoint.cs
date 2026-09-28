namespace TabletAb.Core.Input;

/// <summary>
/// One cursor sample from any input mode.
/// </summary>
/// <param name="NormalizedX">X within the active area, 0..1.</param>
/// <param name="NormalizedY">Y within the active area, 0..1.</param>
/// <param name="RawX">Device-native X (tablet units, or 1/256 channel for debug frames).</param>
/// <param name="RawY">Device-native Y.</param>
/// <param name="MappedX">lazer-mapped output X in pixels (equals raw when no transform applies).</param>
/// <param name="MappedY">lazer-mapped output Y in pixels.</param>
/// <param name="Pressure">Normalised pressure, 0..1.</param>
/// <param name="TimestampMs">Host monotonic timestamp (milliseconds).</param>
public readonly record struct PointerPoint(
    double NormalizedX,
    double NormalizedY,
    float RawX,
    float RawY,
    float MappedX,
    float MappedY,
    float Pressure,
    double TimestampMs);
