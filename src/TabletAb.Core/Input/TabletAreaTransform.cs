using System.Numerics;

namespace TabletAb.Core.Input;

/// <summary>
/// Port of OpenTabletDriver 0.6.7's <c>AbsoluteOutputMode</c>
/// raw -&gt; millimetre -&gt; output-pixel transform, so the app maps tablet
/// reports to cursor positions exactly as osu!lazer does. Pure math, no
/// dependency on the OTD runtime.
/// </summary>
public sealed class TabletAreaTransform
{
    public TabletAreaTransform(DigitizerArea digitizer, TabletArea input, TabletArea output)
    {
        Digitizer = digitizer;
        Input = input;
        Output = output;
    }

    public DigitizerArea Digitizer { get; }

    /// <summary>Active input area on the tablet, in millimetres.</summary>
    public TabletArea Input { get; }

    /// <summary>Output area in pixels (position is the mapped area's centre).</summary>
    public TabletArea Output { get; }

    /// <summary>Build the raw -&gt; output-pixel matrix (OTD order: scale, offset, rotate, scale, translate).</summary>
    public Matrix3x2 BuildMatrix()
    {
        Matrix3x2 matrix = Matrix3x2.CreateScale(Digitizer.WidthMm / Digitizer.MaxX, Digitizer.HeightMm / Digitizer.MaxY);
        matrix *= Matrix3x2.CreateTranslation(-Input.Position.X, -Input.Position.Y);
        matrix *= Matrix3x2.CreateRotation((float)(-Input.RotationDegrees * Math.PI / 180.0));
        matrix *= Matrix3x2.CreateScale(Output.Width / Input.Width, Output.Height / Input.Height);
        matrix *= Matrix3x2.CreateTranslation(Output.Position.X, Output.Position.Y);
        return matrix;
    }

    /// <summary>Output clipping bounds (min inclusive, max inclusive), as OTD computes them.</summary>
    public (Vector2 Min, Vector2 Max) OutputBounds()
    {
        float halfWidth = Output.Width / 2f;
        float halfHeight = Output.Height / 2f;
        return (
            new Vector2(Output.Position.X - halfWidth, Output.Position.Y - halfHeight),
            new Vector2(Output.Position.X + halfWidth, Output.Position.Y + halfHeight));
    }

    /// <summary>Map a raw tablet position to an output pixel coordinate.</summary>
    public Vector2 Transform(Vector2 raw) => Vector2.Transform(raw, BuildMatrix());

    /// <summary>Map and clamp a raw position to the output area.</summary>
    public Vector2 TransformClamped(Vector2 raw)
    {
        Vector2 position = Transform(raw);
        (Vector2 min, Vector2 max) = OutputBounds();
        return Vector2.Clamp(position, min, max);
    }

    /// <summary>Map a raw position to 0..1 across the output area (for the trail view).</summary>
    public Vector2 Normalized(Vector2 raw)
    {
        Vector2 position = TransformClamped(raw);
        (Vector2 min, _) = OutputBounds();
        return new Vector2(
            Output.Width > 0 ? (position.X - min.X) / Output.Width : 0f,
            Output.Height > 0 ? (position.Y - min.Y) / Output.Height : 0f);
    }
}
