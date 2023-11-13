using System.Runtime.InteropServices;

namespace DynamicFontAtlasLib.TrueType.CommonStructs;

[StructLayout(LayoutKind.Explicit)]
public struct TagStruct : IEquatable<TagStruct>, IComparable<TagStruct> {
    [FieldOffset(0)]
    public unsafe fixed byte Tag[4];

    [FieldOffset(0)]
    public uint NativeValue;

    public unsafe TagStruct(char c1, char c2, char c3, char c4) {
        this.Tag[0] = checked((byte)c1);
        this.Tag[1] = checked((byte)c2);
        this.Tag[2] = checked((byte)c3);
        this.Tag[3] = checked((byte)c4);
    }

    public unsafe TagStruct(PointerSpan<byte> span) {
        this.Tag[0] = span[0];
        this.Tag[1] = span[1];
        this.Tag[2] = span[2];
        this.Tag[3] = span[3];
    }

    public unsafe TagStruct(ReadOnlySpan<byte> span) {
        this.Tag[0] = span[0];
        this.Tag[1] = span[1];
        this.Tag[2] = span[2];
        this.Tag[3] = span[3];
    }

    public unsafe byte this[int index] {
        get => this.Tag[index];
        set => this.Tag[index] = value;
    }

    public bool Equals(TagStruct other) => this.NativeValue == other.NativeValue;

    public override bool Equals(object? obj) => obj is TagStruct other && this.Equals(other);

    public override int GetHashCode() => (int)this.NativeValue;

    public static bool operator ==(TagStruct left, TagStruct right) => left.Equals(right);

    public static bool operator !=(TagStruct left, TagStruct right) => !left.Equals(right);

    public int CompareTo(TagStruct other) => this.NativeValue.CompareTo(other.NativeValue);

    public override unsafe string ToString() =>
        $"0x{this.NativeValue:08X} \"{(char)this.Tag[0]}{(char)this.Tag[1]}{(char)this.Tag[2]}{(char)this.Tag[3]}\"";
}
