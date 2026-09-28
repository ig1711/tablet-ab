using System.Numerics;
using TabletAb.Core.Input;

namespace TabletAb.Core.Tests;

public class TabletAreaTransformTests
{
    // HS611 specifications as reported by the custom OTD configuration.
    private const float DigitizerWidthMm = 258.4f;
    private const float DigitizerHeightMm = 161.5f;
    private const int DigitizerMaxX = 51680;
    private const int DigitizerMaxY = 32300;
    private const float OutputWidth = 1920f;
    private const float OutputHeight = 1080f;

    private static readonly DigitizerArea Digitizer =
        new(DigitizerWidthMm, DigitizerHeightMm, DigitizerMaxX, DigitizerMaxY);

    private static TabletAreaTransform MakeTransform(TabletArea? input = null)
        => new(
            Digitizer,
            input ?? TabletArea.Full(DigitizerWidthMm, DigitizerHeightMm),
            TabletArea.Full(OutputWidth, OutputHeight));

    private static void AssertClose(Vector2 expected, Vector2 actual, double tolerance)
    {
        Assert.True(Math.Abs(expected.X - actual.X) <= tolerance,
            $"X: expected {expected.X}, got {actual.X} (tol {tolerance})");
        Assert.True(Math.Abs(expected.Y - actual.Y) <= tolerance,
            $"Y: expected {expected.Y}, got {actual.Y} (tol {tolerance})");
    }

    [Fact]
    public void Transform_MapsRawCornersAndCentreToOutputPixels()
    {
        TabletAreaTransform transform = MakeTransform();

        AssertClose(new Vector2(0f, 0f), transform.Transform(new Vector2(0f, 0f)), 0.1);
        AssertClose(new Vector2(OutputWidth, OutputHeight),
            transform.Transform(new Vector2(DigitizerMaxX, DigitizerMaxY)), 0.1);
        AssertClose(new Vector2(OutputWidth / 2f, OutputHeight / 2f),
            transform.Transform(new Vector2(DigitizerMaxX / 2f, DigitizerMaxY / 2f)), 0.1);
    }

    [Fact]
    public void OutputBounds_MatchOutputArea()
    {
        TabletAreaTransform transform = MakeTransform();

        (Vector2 min, Vector2 max) = transform.OutputBounds();

        AssertClose(new Vector2(0f, 0f), min, 1e-4);
        AssertClose(new Vector2(OutputWidth, OutputHeight), max, 1e-4);
    }

    [Fact]
    public void TransformClamped_KeepsOutOfRangeRawInsideBounds()
    {
        TabletAreaTransform transform = MakeTransform();

        AssertClose(new Vector2(OutputWidth, OutputHeight),
            transform.TransformClamped(new Vector2(DigitizerMaxX * 2f, DigitizerMaxY * 2f)), 0.1);
        AssertClose(new Vector2(0f, 0f),
            transform.TransformClamped(new Vector2(-1000f, -1000f)), 0.1);
    }

    [Fact]
    public void Normalized_MapsCornersAndCentreToUnitRange()
    {
        TabletAreaTransform transform = MakeTransform();

        AssertClose(new Vector2(0f, 0f), transform.Normalized(new Vector2(0f, 0f)), 1e-4);
        AssertClose(new Vector2(1f, 1f),
            transform.Normalized(new Vector2(DigitizerMaxX, DigitizerMaxY)), 1e-4);
        AssertClose(new Vector2(0.5f, 0.5f),
            transform.Normalized(new Vector2(DigitizerMaxX / 2f, DigitizerMaxY / 2f)), 1e-4);
    }

    [Fact]
    public void RotatedInput_ProducesFiniteInBoundsResultThatDiffersFromUnrotated()
    {
        TabletArea unrotatedInput = TabletArea.Full(DigitizerWidthMm, DigitizerHeightMm);
        TabletArea rotatedInput = new(
            DigitizerWidthMm,
            DigitizerHeightMm,
            new Vector2(DigitizerWidthMm / 2f, DigitizerHeightMm / 2f),
            90f);

        TabletAreaTransform unrotated = MakeTransform(unrotatedInput);
        TabletAreaTransform rotated = MakeTransform(rotatedInput);

        Vector2 raw = new(0f, 0f);
        Vector2 rotatedResult = rotated.TransformClamped(raw);
        Vector2 unrotatedResult = unrotated.TransformClamped(raw);

        Assert.True(float.IsFinite(rotatedResult.X) && float.IsFinite(rotatedResult.Y));
        Assert.InRange(rotatedResult.X, 0f, OutputWidth);
        Assert.InRange(rotatedResult.Y, 0f, OutputHeight);
        Assert.NotEqual(unrotatedResult, rotatedResult);
    }

    [Fact]
    public void OffsetInputArea_ShiftsResult()
    {
        TabletArea fullInput = TabletArea.Full(DigitizerWidthMm, DigitizerHeightMm);
        TabletArea offsetInput = fullInput with
        {
            Position = fullInput.Position + new Vector2(10f, 5f),
        };

        TabletAreaTransform full = MakeTransform(fullInput);
        TabletAreaTransform offset = MakeTransform(offsetInput);

        Vector2 raw = new(DigitizerMaxX / 2f, DigitizerMaxY / 2f);
        Vector2 fullResult = full.Transform(raw);
        Vector2 offsetResult = offset.Transform(raw);

        Assert.NotEqual(fullResult, offsetResult);
        Assert.True(offsetResult.X < fullResult.X);
        Assert.True(offsetResult.Y < fullResult.Y);
    }
}
