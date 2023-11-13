namespace DynamicFontAtlasLib.TrueType.Tables;

public static class ValueFormatExtensions {
    public static int NumBytes(this ValueFormat value) => ushort.PopCount((ushort)(value & ValueFormat.ValidBits)) * 2;
}
