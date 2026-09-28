namespace TabletAb.Core;

/// <summary>
/// Product-level constants shared across the app.
/// </summary>
public static class ProductInfo
{
    public const string Name = "tablet-ab";

    public const string DisplayName = "tablet-ab — tablet A/B testing interface";

    /// <summary>Kept in step with the phase tracker; bumped as phases land.</summary>
    public const string Version = "0.1.0-dev";
}
