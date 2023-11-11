using DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.Internal.TrueType;

public struct Fixed {
    public ushort Major;
    public ushort Minor;

    public Fixed(PointerSpan<byte> span) {
        var offset = 0;
        span.ReadBE(ref offset, out this.Major);
        span.ReadBE(ref offset, out this.Minor);
    }
}
