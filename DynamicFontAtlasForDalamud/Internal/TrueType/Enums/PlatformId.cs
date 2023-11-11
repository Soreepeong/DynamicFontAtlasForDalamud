namespace DynamicFontAtlasLib.Internal.TrueType;

public enum PlatformId : ushort {
    Unicode = 0,
    Macintosh = 1, // discouraged
    Iso = 2,       // deprecated
    Windows = 3,
    Custom = 4, // OTF Windows NT compatibility mapping
}
