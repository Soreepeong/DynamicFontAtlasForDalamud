using System.Buffers.Binary;

namespace DynamicFontAtlasLib.TrueType;

#pragma warning disable CS0649
public readonly ref struct LookupOffsetArrayView {
    private readonly Span<byte> data;

    public int Count {
        get => BinaryPrimitives.ReadUInt16BigEndian(this.data);
        set => BinaryPrimitives.WriteUInt16BigEndian(this.data, checked((ushort)value));
    }

    public ushort this[int index] {
        get => BinaryPrimitives.ReadUInt16BigEndian(this.data[2..][(index * 2)..]);
        set => BinaryPrimitives.WriteUInt16BigEndian(this.data[2..][(index * 2)..], value);
    }
}
