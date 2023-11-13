using System.Buffers.Binary;
using DynamicFontAtlasLib.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.TrueType.Tables.GposGsub;

public readonly struct CoverageTable {
    public readonly PointerSpan<byte> Memory;

    public CoverageTable(PointerSpan<byte> memory) => this.Memory = memory;

    public CoverageFormat Format => this.Memory.ReadEnumBig<CoverageFormat>(0);
    public ushort Count => this.Memory.ReadU16Big(2);

    public BigEndianPointerSpan<ushort> Glyphs => this.Format == CoverageFormat.Glyphs
        ? new(
            this.Memory[4..].As<ushort>(this.Count),
            BinaryPrimitives.ReverseEndianness)
        : new();

    public BigEndianPointerSpan<RangeRecord> RangeRecords => this.Format == CoverageFormat.RangeRecords
        ? new(
            this.Memory[4..].As<RangeRecord>(this.Count),
            RangeRecord.ReverseEndianness)
        : new();

    public int GetCoverageIndex(ushort glyphId) {
        switch (this.Format) {
            case CoverageFormat.Glyphs:
                return this.Glyphs.BinarySearch(glyphId);

            case CoverageFormat.RangeRecords: {
                var index = this.RangeRecords.BinarySearch(
                    (in RangeRecord record) => glyphId.CompareTo(record.EndGlyphId));

                if (index >= 0 && this.RangeRecords[index].ContainsGlyph(glyphId))
                    return index;

                return -1;
            }
            default:
                return -1;
        }
    }

    public enum CoverageFormat : ushort {
        Glyphs = 1,
        RangeRecords = 2,
    }

    public struct RangeRecord {
        public ushort StartGlyphId;
        public ushort EndGlyphId;
        public ushort StartCoverageIndex;

        public bool ContainsGlyph(ushort glyphId) =>
            this.StartGlyphId <= glyphId && glyphId <= this.EndGlyphId;

        public static RangeRecord ReverseEndianness(RangeRecord value) => new() {
            StartGlyphId = BinaryPrimitives.ReverseEndianness(value.StartGlyphId),
            EndGlyphId = BinaryPrimitives.ReverseEndianness(value.EndGlyphId),
            StartCoverageIndex = BinaryPrimitives.ReverseEndianness(value.StartCoverageIndex),
        };
    }
}
