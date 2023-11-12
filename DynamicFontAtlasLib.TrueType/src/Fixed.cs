using DynamicFontAtlasLib.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.TrueType;

public struct Fixed {
    public ushort Major;
    public ushort Minor;

    public Fixed(PointerSpan<byte> span) {
        var offset = 0;
        span.ReadBig(ref offset, out this.Major);
        span.ReadBig(ref offset, out this.Minor);
    }
}
