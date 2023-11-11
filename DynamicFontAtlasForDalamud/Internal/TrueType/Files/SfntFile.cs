using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.Internal.TrueType.Files;

#pragma warning disable CS0649
public struct SfntFile : IReadOnlyDictionary<TagStruct, PointerSpan<byte>> {
    // http://formats.kaitai.io/ttf/ttf.svg

    public PointerSpan<byte> Memory;
    public int OffsetInCollection;
    public ushort TableCount;

    public SfntFile(PointerSpan<byte> memory, int offsetInCollection) {
        var span = memory.Span;
        this.Memory = memory;
        this.OffsetInCollection = offsetInCollection;
        this.TableCount = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
    }

    public int Count => this.TableCount;

    public IEnumerable<TagStruct> Keys => this.Select(x => x.Key);

    public IEnumerable<PointerSpan<byte>> Values => this.Select(x => x.Value);

    public PointerSpan<byte> this[TagStruct key] => this.First(x => x.Key == key).Value;

    public IEnumerator<KeyValuePair<TagStruct, PointerSpan<byte>>> GetEnumerator() {
        var offset = 12;
        for (var i = 0; i < this.TableCount; i++) {
            var dte = new DirectoryTableEntry(this.Memory.Span[offset..]);
            yield return new(dte.Tag, this.Memory.Slice(dte.Offset - this.OffsetInCollection, dte.Length));
            offset += Unsafe.SizeOf<DirectoryTableEntry>();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public bool ContainsKey(TagStruct key) => this.Any(x => x.Key == key);

    public bool TryGetValue(TagStruct key, out PointerSpan<byte> value) {
        foreach (var (k, v) in this) {
            if (k == key) {
                value = v;
                return true;
            }
        }

        value = default;
        return false;
    }

    public struct DirectoryTableEntry {
        public TagStruct Tag;
        public uint Checksum;
        public int Offset;
        public int Length;

        public DirectoryTableEntry(Span<byte> span) {
            this.Tag = new(span);
            this.Checksum = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            this.Offset = BinaryPrimitives.ReadInt32BigEndian(span[8..]);
            this.Length = BinaryPrimitives.ReadInt32BigEndian(span[12..]);
        }
    }
}
