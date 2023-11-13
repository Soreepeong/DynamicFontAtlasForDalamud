namespace DynamicFontAtlasLib.TrueType.Tables.GposGsub;

[Flags]
public enum LookupFlags : byte {
    RightToLeft = 1 << 0,
    IgnoreBaseGlyphs = 1 << 1,
    IgnoreLigatures = 1 << 2,
    IgnoreMarks = 1 << 3,
    UseMarkFilteringSet = 1 << 4,
}
