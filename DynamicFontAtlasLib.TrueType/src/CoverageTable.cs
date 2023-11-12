using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace DynamicFontAtlasLib.TrueType;

#pragma warning disable CS0649
public readonly ref struct CoverageTable {
    private readonly Span<byte> data;

    public CoverageTable(Span<byte> data) => this.data = data;

    public ref HeaderStruct Header => ref this.data.AsRef<HeaderStruct>();

    public Span<ushort> Glyphs =>
        this.data[Unsafe.SizeOf<HeaderStruct>()..].AsSpan<ushort>(this.Header.Count);

    public Span<RangeRecord> RangeRecords =>
        this.data[Unsafe.SizeOf<HeaderStruct>()..].AsSpan<RangeRecord>(this.Header.Count);
    
    [Pure]
    public int GetCoverageIndex(ushort glyphId) {
        switch (this.Header.FormatId) {
            case 1:
                return this.Glyphs.BinarySearch(glyphId);

            case 2:
            {
                var index = this.RangeRecords.BinarySearch(new RangeRecord { EndGlyphId = glyphId });
                if (index >= 0 && this.RangeRecords[index].ContainsGlyph(glyphId))
                    return index;

                return -1;
            }
            default:
                return -1;
        }
    }

    public struct HeaderStruct {
        public ushort FormatId;
        public ushort Count;
    }

    public struct RangeRecord : IComparable<RangeRecord> {
        public ushort StartGlyphId;
        public ushort EndGlyphId;
        public ushort StartCoverageIndex;
        
        public int CompareTo(RangeRecord other) => this.EndGlyphId.CompareTo(other.EndGlyphId);

        public bool ContainsGlyph(ushort glyphId) =>
            this.StartGlyphId <= glyphId && glyphId <= this.EndGlyphId;
    }
}
