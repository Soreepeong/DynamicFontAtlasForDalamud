namespace DynamicFontAtlasLib.TrueType.Tables;

[Flags]
public enum ValueFormat : ushort {
    PlacementX = 1 << 0,
    PlacementY = 1 << 1,
    AdvanceX = 1 << 2,
    AdvanceY = 1 << 3,
    PlacementDeviceOffsetX = 1 << 4,
    PlacementDeviceOffsetY = 1 << 5,
    AdvanceDeviceOffsetX = 1 << 6,
    AdvanceDeviceOffsetY = 1 << 7,

    ValidBits = PlacementX | PlacementY
        | AdvanceX | AdvanceY
        | PlacementDeviceOffsetX | PlacementDeviceOffsetY
        | AdvanceDeviceOffsetX | AdvanceDeviceOffsetY,
}
