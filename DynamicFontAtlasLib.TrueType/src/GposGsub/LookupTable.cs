using System.Buffers.Binary;
using System.Collections;
using DynamicFontAtlasLib.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.TrueType.GposGsub;

public readonly struct LookupTable : IEnumerable<PointerSpan<byte>> {
    public readonly PointerSpan<byte> Memory;

    public LookupTable(PointerSpan<byte> memory) => this.Memory = memory;

    public LookupType Type => this.Memory.ReadEnumBig<LookupType>(0);
    public byte MarkAttachmentType => this.Memory[2];
    public LookupFlags Flags => (LookupFlags)this.Memory[3];
    public ushort SubtableCount => this.Memory.ReadU16Big(4);

    public BigEndianPointerSpan<ushort> SubtableOffsets => new(
        this.Memory[6..].As<ushort>(this.SubtableCount),
        BinaryPrimitives.ReverseEndianness);

    public PointerSpan<byte> this[int index] => this.Memory[this.SubtableOffsets[this.EnsureIndex(index)]..];

    public IEnumerator<PointerSpan<byte>> GetEnumerator() {
        foreach (var i in Enumerable.Range(0, this.SubtableCount))
            yield return this.Memory[this.SubtableOffsets[i]..];
    }

    private int EnsureIndex(int index) => index >= 0 && index < this.SubtableCount
        ? index
        : throw new IndexOutOfRangeException();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
