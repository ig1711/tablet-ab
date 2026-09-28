namespace TabletAb.Core;

/// <summary>
/// Which input path is in use. Mirrors the firmware build matrix plus the
/// osu!lazer-style external-driver mode.
/// </summary>
public enum DeviceKind
{
    /// <summary>DEBUG_MIN / DEBUG_DUMP firmware (vendor bulk, full command set).</summary>
    DebugVendor,

    /// <summary>RELEASE + REL_DEBUG: release acquisition streamed over vendor bulk (no commands).</summary>
    ReleaseVendor,

    /// <summary>RELEASE HID firmware: report id 8, parsed like OpenTabletDriver does.</summary>
    ReleaseHid,

    /// <summary>External driver / OS pointer; the app only observes.</summary>
    ExternalPointer,
}
