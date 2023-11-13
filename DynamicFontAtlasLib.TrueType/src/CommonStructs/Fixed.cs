namespace DynamicFontAtlasLib.TrueType.CommonStructs;

public struct Fixed : IComparable<Fixed> {
    public ushort Major;
    public ushort Minor;

    public Fixed(ushort major, ushort minor) {
        this.Major = major;
        this.Minor = minor;
    }

    public Fixed(PointerSpan<byte> span) {
        var offset = 0;
        span.ReadBig(ref offset, out this.Major);
        span.ReadBig(ref offset, out this.Minor);
    }

    public int CompareTo(Fixed other) {
        var majorComparison = this.Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : this.Minor.CompareTo(other.Minor);
    }
}
