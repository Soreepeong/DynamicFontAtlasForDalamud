using System.Buffers.Binary;
using System.Diagnostics.Contracts;
using DynamicFontAtlasLib.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.TrueType.Tables.GposGsub;

public readonly struct ClassDefTable {
    public readonly PointerSpan<byte> Memory;

    public ClassDefTable(PointerSpan<byte> memory) => this.Memory = memory;

    public ushort Format => this.Memory.ReadU16Big(0);

    public Format1ClassArray Format1 => new(this.Memory);

    public Format2ClassRanges Format2 => new(this.Memory);

    public readonly struct Format1ClassArray {
        public readonly PointerSpan<byte> Memory;

        public Format1ClassArray(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);
        public ushort StartGlyphId => this.Memory.ReadU16Big(2);
        public ushort GlyphCount => this.Memory.ReadU16Big(4);

        public BigEndianPointerSpan<ushort> ClassValueArray => new(
            this.Memory[6..].As<ushort>(this.GlyphCount),
            BinaryPrimitives.ReverseEndianness);
    }

    public readonly struct Format2ClassRanges {
        public readonly PointerSpan<byte> Memory;

        public Format2ClassRanges(PointerSpan<byte> memory) => this.Memory = memory;
        public ushort ClassRangeCount => this.Memory.ReadU16Big(2);

        public BigEndianPointerSpan<ClassRangeRecord> ClassValueArray => new(
            this.Memory[4..].As<ClassRangeRecord>(this.ClassRangeCount),
            ClassRangeRecord.ReverseEndianness);

        public struct ClassRangeRecord  : IComparable<ClassRangeRecord> {
            public ushort StartGlyphId;
            public ushort EndGlyphId;
            public ushort Class;
        
            public int CompareTo(ClassRangeRecord other) => this.EndGlyphId.CompareTo(other.EndGlyphId);

            public bool ContainsGlyph(ushort glyphId) =>
                this.StartGlyphId <= glyphId && glyphId <= this.EndGlyphId;

            public static ClassRangeRecord ReverseEndianness(ClassRangeRecord value) => new() {
                StartGlyphId = BinaryPrimitives.ReverseEndianness(value.StartGlyphId),
                EndGlyphId = BinaryPrimitives.ReverseEndianness(value.EndGlyphId),
                Class = BinaryPrimitives.ReverseEndianness(value.Class),
            };
        }
    }

    public IEnumerable<(ushort Class, ushort GlyphId)> Enumerate() {
        switch (this.Format) {
            case 1: {
                var format1 = this.Format1;
                var startId = format1.StartGlyphId;
                var count = format1.GlyphCount;
                var classes = format1.ClassValueArray;
                for (var i = 0; i < count; i++)
                    yield return (classes[i], (ushort)(i + startId));

                break;
            }

            case 2:
            {
                foreach(var range in this.Format2.ClassValueArray) {
                    var @class = range.Class;
                    var startId = range.StartGlyphId;
                    var count = range.EndGlyphId - startId + 1;
                    for (var i = 0; i < count; i++)
                        yield return (@class, (ushort)(startId + i));
                }
                break;
            }
        }
    } 

    [Pure]
    public ushort GetClass(ushort glyphId) {
        switch (this.Format) {
            case 1: {
                var format1 = this.Format1;
                var startId = format1.StartGlyphId;
                if (startId <= glyphId && glyphId < startId + format1.GlyphCount)
                    return this.Format1.ClassValueArray[glyphId - startId];

                break;
            }

            case 2:
            {
                var rangeSpan = this.Format2.ClassValueArray;
                var i = rangeSpan.BinarySearch(new Format2ClassRanges.ClassRangeRecord { EndGlyphId = glyphId });
                if (i >= 0 && rangeSpan[i].ContainsGlyph(glyphId))
                    return rangeSpan[i].Class;

                break;
            }
        }

        return 0;
    }
}
