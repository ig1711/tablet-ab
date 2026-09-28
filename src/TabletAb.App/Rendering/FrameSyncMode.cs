namespace TabletAb.App.Rendering;

/// <summary>
/// How frames are paced, mirroring osu!framework's <c>FrameSync</c> setting so
/// the app runs under the same conditions as osu!lazer.
/// </summary>
public enum FrameSyncMode
{
    /// <summary>No pacing; present as fast as the GPU/compositor allows.</summary>
    Unlimited,

    /// <summary>Present synchronised to the display's vertical blank.</summary>
    VSync,

    /// <summary>Limit to 2x the display refresh rate.</summary>
    Limit2x,

    /// <summary>Limit to 4x the display refresh rate.</summary>
    Limit4x,

    /// <summary>Limit to 8x the display refresh rate.</summary>
    Limit8x,
}
