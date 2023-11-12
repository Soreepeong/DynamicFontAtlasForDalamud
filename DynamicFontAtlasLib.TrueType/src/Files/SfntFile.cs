using System.Buffers.Binary;
using System.Collections;
using System.Runtime.CompilerServices;
using DynamicFontAtlasLib.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.TrueType.Files;

#pragma warning disable CS0649
public struct SfntFile : IReadOnlyDictionary<TagStruct, PointerSpan<byte>> {
    // http://formats.kaitai.io/ttf/ttf.svg

    public PointerSpan<byte> Memory;
    public int OffsetInCollection;
    public ushort TableCount;

    public SfntFile(PointerSpan<byte> memory, int offsetInCollection = 0) {
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
            var dte = new DirectoryTableEntry(this.Memory[offset..]);
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
        public PointerSpan<byte> Memory;

        public DirectoryTableEntry(PointerSpan<byte> span) => this.Memory = span;

        public TagStruct Tag => new(this.Memory);
        public uint Checksum => this.Memory.ReadU32Big(4);
        public int Offset => this.Memory.ReadI32Big(8);
        public int Length => this.Memory.ReadI32Big(12);
    }
}
