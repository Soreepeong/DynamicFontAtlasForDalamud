using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace DynamicFontAtlasLib.Internal.TrueType;

#pragma warning disable CS0649
public readonly ref struct ClassDefTable {
    private readonly Span<byte> data;

    public ClassDefTable(Span<byte> data) => this.data = data;

    public ref ushort FormatId => ref this.data.AsRef<ushort>();

    public Format1ClassArray Format1 => new(this.data);

    public Format2ClassRanges Format2 => new(this.data);

    public readonly ref struct Format1ClassArray {
        private readonly Span<byte> data;

        public Format1ClassArray(Span<byte> data) => this.data = data;

        public ref HeaderStruct Header => ref this.data.AsRef<HeaderStruct>();
        
        public Span<ushort> ClassValueArray =>
            this.data[Unsafe.SizeOf<HeaderStruct>()..].AsSpan<ushort>(this.Header.GlyphCount);

        public struct HeaderStruct {
            public ushort FormatId;
            public ushort StartGlyphId;
            public ushort GlyphCount;
        }
    }

    public readonly ref struct Format2ClassRanges {
        private readonly Span<byte> data;

        public Format2ClassRanges(Span<byte> data) => this.data = data;

        public ref HeaderStruct Header => ref this.data.AsRef<HeaderStruct>();
        
        public Span<ClassRangeRecord> ClassValueArray =>
            this.data[Unsafe.SizeOf<HeaderStruct>()..].AsSpan<ClassRangeRecord>(this.Header.ClassRangeCount);

        public struct HeaderStruct {
            public ushort FormatId;
            public ushort ClassRangeCount;
        }

        public struct ClassRangeRecord  : IComparable<ClassRangeRecord> {
            public ushort StartGlyphId;
            public ushort EndGlyphId;
            public ushort Class;
        
            public int CompareTo(ClassRangeRecord other) => this.EndGlyphId.CompareTo(other.EndGlyphId);

            public bool ContainsGlyph(ushort glyphId) =>
                this.StartGlyphId <= glyphId && glyphId <= this.EndGlyphId;
        }
    }

    [Pure]
    public Dictionary<ushort, SortedSet<ushort>> ClassToGlyphMap() {
        var res = new Dictionary<ushort, SortedSet<ushort>>();
        switch (this.FormatId) {
            case 1:
            {
                var startId = this.Format1.Header.StartGlyphId;
                var count = this.Format1.Header.GlyphCount;
                for (var i = 0; i < count; i++) {
                    var @class = this.Format1.ClassValueArray[i];
                    if (!res.TryGetValue(@class, out var set))
                        res.Add(@class, set = new());

                    set.Add((ushort)(startId + i));
                }

                break;
            }

            case 2:
            {
                foreach(ref var range in this.Format2.ClassValueArray) {
                    var @class = range.Class;
                    if (!res.TryGetValue(@class, out var set))
                        res.Add(@class, set = new());

                    for (int i = range.StartGlyphId, to = range.EndGlyphId; i <= to; i++)
                        set.Add((ushort)i);
                }
                break;
            }
        }
        return res;
    }

    [Pure]
    public Dictionary<ushort, ushort> GlyphToClassMap() {
        var res = new Dictionary<ushort, ushort>();
        switch (this.FormatId) {
            case 1:
            {
                var startId = this.Format1.Header.StartGlyphId;
                var count = this.Format1.Header.GlyphCount;
                for (var i = 0; i < count; i++)
                    res[(ushort)(startId + i)] = this.Format1.ClassValueArray[i];
                break;
            }

            case 2:
            {
                foreach(ref var range in this.Format2.ClassValueArray) {
                    var @class = range.Class;
                    for (int i = range.StartGlyphId, to = range.EndGlyphId; i <= to; i++)
                        res[(ushort)i] = @class;
                }
                break;
            }
        }
        return res;
    }

    [Pure]
    public ushort GetClass(ushort glyphId) {
        switch (this.FormatId) {
            case 1:
            {
                var startId = this.Format1.Header.StartGlyphId;
                if (startId <= glyphId && glyphId < startId + this.Format1.Header.GlyphCount)
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
