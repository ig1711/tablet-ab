using System.Numerics;

namespace TabletAb.Core.Input;

/// <summary>
/// A rectangular area with a centre position and rotation, mirroring
/// OpenTabletDriver's <c>Area</c> (used for both the input tablet area in
/// millimetres and the output area in pixels).
/// </summary>
public readonly record struct TabletArea(float Width, float Height, Vector2 Position, float RotationDegrees)
{
    /// <summary>The full area with its origin at the top-left and no rotation.</summary>
    public static TabletArea Full(float width, float height)
        => new(width, height, new Vector2(width / 2f, height / 2f), 0f);
}

/// <summary>Digitizer dimensions: active size in millimetres and raw unit range.</summary>
public readonly record struct DigitizerArea(float WidthMm, float HeightMm, float MaxX, float MaxY);
