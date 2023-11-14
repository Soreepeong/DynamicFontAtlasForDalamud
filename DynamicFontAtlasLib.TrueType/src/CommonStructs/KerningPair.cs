using System.Buffers.Binary;

namespace DynamicFontAtlasLib.TrueType.CommonStructs;

public struct KerningPair : IEquatable<KerningPair> {
    public ushort Left;
    public ushort Right;
    public short Value;

    public KerningPair(PointerSpan<byte> span) {
        var offset = 0;
        span.ReadBig(ref offset, out this.Left);
        span.ReadBig(ref offset, out this.Right);
        span.ReadBig(ref offset, out this.Value);
    }

    public KerningPair(ushort left, ushort right, short value) {
        this.Left = left;
        this.Right = right;
        this.Value = value;
    }

    public bool Equals(KerningPair other) =>
        this.Left == other.Left && this.Right == other.Right && this.Value == other.Value;

    public override bool Equals(object? obj) => obj is KerningPair other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(this.Left, this.Right, this.Value);

    public override string ToString() => $"KerningPair[{this.Left}, {this.Right}] = {this.Value}";

    public static bool operator ==(KerningPair left, KerningPair right) => left.Equals(right);

    public static bool operator !=(KerningPair left, KerningPair right) => !left.Equals(right);

    public static KerningPair ReverseEndianness(KerningPair pair) => new() {
        Left = BinaryPrimitives.ReverseEndianness(pair.Left),
        Right = BinaryPrimitives.ReverseEndianness(pair.Right),
        Value = BinaryPrimitives.ReverseEndianness(pair.Value),
    };
}
