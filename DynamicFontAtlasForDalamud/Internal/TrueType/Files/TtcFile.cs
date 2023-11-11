using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;

namespace DynamicFontAtlasLib.Internal.TrueType.Files;

public struct TtcFile : IReadOnlyList<SfntFile> {
    public static readonly TagStruct FileTag = new('t', 't', 'c', 'f');

    public Memory<byte> Memory;
    public TagStruct Tag;
    public ushort MajorVersion;
    public ushort MinorVersion;
    public int FontCount;

    public TtcFile(Memory<byte> memory) {
        var span = memory.Span;
        this.Memory = memory;
        this.Tag = new(span);
        if (this.Tag != FileTag)
            throw new InvalidOperationException();

        this.MajorVersion = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
        this.MinorVersion = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
        this.FontCount = BinaryPrimitives.ReadInt32BigEndian(span[8..]);
    }

    public IEnumerator<SfntFile> GetEnumerator() {
        for (var i = 0; i < this.FontCount; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public int Count => this.FontCount;

    public SfntFile this[int index] {
        get {
            if (index < 0 || index >= this.FontCount)
                throw new IndexOutOfRangeException();

            var offset = BinaryPrimitives.ReadInt32BigEndian(this.Memory.Span[(12 + 4 * index)..]);
            return new(this.Memory[offset..], offset);
        }
    }
}
