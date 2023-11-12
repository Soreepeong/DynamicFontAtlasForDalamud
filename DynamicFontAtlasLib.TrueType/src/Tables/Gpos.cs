using System.Buffers.Binary;
using System.Collections.Immutable;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Files;
using DynamicFontAtlasLib.TrueType.GposGsub;

namespace DynamicFontAtlasLib.TrueType.Tables;

public readonly struct Gpos {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/gpos

    public static readonly TagStruct DirectoryTableTag = new('G', 'P', 'O', 'S');

    public readonly PointerSpan<byte> Memory;

    public Gpos(SfntFile file) : this(file[DirectoryTableTag]) { }

    public Gpos(PointerSpan<byte> memory) => this.Memory = memory;

    public Fixed Version => new(Memory);
    public ushort ScriptListOffset => this.Memory.ReadU16Big(4);
    public ushort FeatureListOffset => this.Memory.ReadU16Big(6);
    public ushort LookupListOffset => this.Memory.ReadU16Big(8);

    public uint FeatureVariationsOffset => this.Version.CompareTo(new(1, 1)) >= 0
        ? this.Memory.ReadU32Big(10)
        : 0;

    public BigEndianPointerSpan<ushort> LookupOffsetList => new(
        this.Memory[(this.LookupListOffset + 2)..].As<ushort>(
            this.Memory.ReadU16Big(this.LookupListOffset)),
        BinaryPrimitives.ReverseEndianness);

    public IEnumerable<LookupTable> EnumerateLookupTables() {
        foreach (var offset in this.LookupOffsetList)
            yield return new(this.Memory[(this.LookupListOffset + offset)..]);
    }

    [Flags]
    public enum ValueFormatFlags : ushort {
        PlacementX = 1 << 0,
        PlacementY = 1 << 1,
        AdvanceX = 1 << 2,
        AdvanceY = 1 << 3,
        PlacementDeviceOffsetX = 1 << 4,
        PlacementDeviceOffsetY = 1 << 5,
        AdvanceDeviceOffsetX = 1 << 6,
        AdvanceDeviceOffsetY = 1 << 7,
    }

    public struct ValueRecord {
        public short PlacementX;
        public short PlacementY;
        public short AdvanceX;
        public short AdvanceY;
        public short PlacementDeviceOffsetX;
        public short PlacementDeviceOffsetY;
        public short AdvanceDeviceOffsetX;
        public short AdvanceDeviceOffsetY;

        public ValueRecord(PointerSpan<byte> pointerSpan, ValueFormatFlags valueFormatFlags) {
            var offset = 0;
            if ((valueFormatFlags & ValueFormatFlags.PlacementX) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementX);

            if ((valueFormatFlags & ValueFormatFlags.PlacementY) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementY);

            if ((valueFormatFlags & ValueFormatFlags.AdvanceX) != 0) pointerSpan.ReadBig(ref offset, out this.AdvanceX);
            if ((valueFormatFlags & ValueFormatFlags.AdvanceY) != 0) pointerSpan.ReadBig(ref offset, out this.AdvanceY);
            if ((valueFormatFlags & ValueFormatFlags.PlacementDeviceOffsetX) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementDeviceOffsetX);

            if ((valueFormatFlags & ValueFormatFlags.PlacementDeviceOffsetY) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementDeviceOffsetY);

            if ((valueFormatFlags & ValueFormatFlags.AdvanceDeviceOffsetX) != 0)
                pointerSpan.ReadBig(ref offset, out this.AdvanceDeviceOffsetX);

            if ((valueFormatFlags & ValueFormatFlags.AdvanceDeviceOffsetY) != 0)
                pointerSpan.ReadBig(ref offset, out this.AdvanceDeviceOffsetY);
        }
    }

    public struct PairAdjustmentPositioning {
        public PointerSpan<byte> Memory;

        public PairAdjustmentPositioning(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);

        public IEnumerable<KerningPair> ExtractAdvanceX() => this.Format switch {
            1 => new Format1(this.Memory).ExtractAdvanceX(),
            2 => new Format2(this.Memory).ExtractAdvanceX(),
            _ => Array.Empty<KerningPair>()
        };

        public struct Format1 {
            public PointerSpan<byte> Memory;

            public Format1(PointerSpan<byte> memory) => this.Memory = memory;

            public ushort Format => this.Memory.ReadU16Big(0);
            public ushort CoverageOffset => this.Memory.ReadU16Big(2);
            public ValueFormatFlags ValueFormat1 => this.Memory.ReadEnumBig<ValueFormatFlags>(4);
            public ValueFormatFlags ValueFormat2 => this.Memory.ReadEnumBig<ValueFormatFlags>(6);
            public ushort PairSetCount => this.Memory.ReadU16Big(8);

            public BigEndianPointerSpan<ushort> PairSetOffsets => new(
                this.Memory[10..].As<ushort>(this.PairSetCount),
                BinaryPrimitives.ReverseEndianness);

            public PairSet this[int index] => new(
                this.Memory[this.PairSetOffsets[index]..],
                this.ValueFormat1,
                this.ValueFormat2);

            public CoverageTable CoverageTable => new(this.Memory[this.CoverageOffset..]);

            public IEnumerable<KerningPair> ExtractAdvanceX() {
                if ((this.ValueFormat1 & ValueFormatFlags.AdvanceX) == 0 &&
                    (this.ValueFormat2 & ValueFormatFlags.AdvanceX) == 0) {
                    yield break;
                }

                var coverageTable = this.CoverageTable;
                switch (coverageTable.Format) {
                    case CoverageTable.CoverageFormat.Glyphs: {
                        var glyphSpan = coverageTable.Glyphs;
                        foreach (var coverageIndex in Enumerable.Range(0, glyphSpan.Count)) {
                            var glyph1Id = glyphSpan[coverageIndex];
                            PairSet pairSetView;
                            try {
                                pairSetView = this[coverageIndex];
                            } catch (ArgumentOutOfRangeException) {
                                yield break;
                            } catch (IndexOutOfRangeException) {
                                yield break;
                            }

                            foreach (var pairIndex in Enumerable.Range(0, pairSetView.Count)) {
                                var pair = pairSetView[pairIndex];
                                var adj = (short)(pair.Record1.AdvanceX + pair.Record2.PlacementX);
                                if (adj != 0)
                                    yield return new(glyph1Id, pair.SecondGlyph, adj);
                            }
                        }

                        break;
                    }
                    case CoverageTable.CoverageFormat.RangeRecords: {
                        foreach (var rangeRecord in coverageTable.RangeRecords) {
                            var startGlyphId = rangeRecord.StartGlyphId;
                            var endGlyphId = rangeRecord.EndGlyphId;
                            var startCoverageIndex = rangeRecord.StartCoverageIndex;
                            var glyphCount = endGlyphId - startGlyphId + 1;
                            foreach (var glyph1Id in Enumerable.Range(startGlyphId, glyphCount)) {
                                PairSet pairSetView;
                                try {
                                    pairSetView = this[startCoverageIndex + glyph1Id - startGlyphId];
                                } catch (ArgumentOutOfRangeException) {
                                    yield break;
                                } catch (IndexOutOfRangeException) {
                                    yield break;
                                }

                                foreach (var pairIndex in Enumerable.Range(0, pairSetView.Count)) {
                                    var pair = pairSetView[pairIndex];
                                    var adj = (short)(pair.Record1.AdvanceX + pair.Record2.PlacementX);
                                    if (adj != 0)
                                        yield return new((ushort)glyph1Id, pair.SecondGlyph, adj);
                                }
                            }
                        }

                        break;
                    }
                }
            }

            public struct PairSet {
                public PointerSpan<byte> Memory;
                public ValueFormatFlags ValueFormatFlags1;
                public ValueFormatFlags ValueFormatFlags2;

                public PairSet(
                    PointerSpan<byte> memory,
                    ValueFormatFlags valueFormatFlags1,
                    ValueFormatFlags valueFormatFlags2) {
                    this.Memory = memory;
                    this.ValueFormatFlags1 = valueFormatFlags1;
                    this.ValueFormatFlags2 = valueFormatFlags2;
                }

                public ushort Count => this.Memory.ReadU16Big(0);

                public int RecordElementSize =>
                    (1 + ushort.PopCount((ushort)ValueFormatFlags1) + ushort.PopCount((ushort)ValueFormatFlags2)) * 2;

                public PairValueRecord this[int index] {
                    get {
                        var pvr = this.Memory[(2 + (this.RecordElementSize * index))..];
                        return new() {
                            SecondGlyph = pvr.ReadU16Big(0),
                            Record1 = new(pvr[2..], this.ValueFormatFlags1),
                            Record2 = new(pvr[(2 + this.RecordElementSize)..], this.ValueFormatFlags2)
                        };
                    }
                }

                public struct PairValueRecord {
                    public ushort SecondGlyph;
                    public ValueRecord Record1;
                    public ValueRecord Record2;
                }
            }
        }

        public struct Format2 {
            public PointerSpan<byte> Memory;

            public Format2(PointerSpan<byte> memory) => this.Memory = memory;

            public ushort Format => this.Memory.ReadU16Big(0);
            public ushort CoverageOffset => this.Memory.ReadU16Big(2);
            public ValueFormatFlags ValueFormat1 => this.Memory.ReadEnumBig<ValueFormatFlags>(4);
            public ValueFormatFlags ValueFormat2 => this.Memory.ReadEnumBig<ValueFormatFlags>(6);
            public ushort ClassDef1Offset => this.Memory.ReadU16Big(8);
            public ushort ClassDef2Offset => this.Memory.ReadU16Big(10);
            public ushort Class1Count => this.Memory.ReadU16Big(12);
            public ushort Class2Count => this.Memory.ReadU16Big(14);

            public int Value1Size => 2 * ushort.PopCount((ushort)this.ValueFormat1);
            public int Value2Size => 2 * ushort.PopCount((ushort)this.ValueFormat2);
            public int ValueSize => this.Value1Size + this.Value2Size;

            public ClassDefTable ClassDefTable1 => new(this.Memory[this.ClassDef1Offset..]);
            public ClassDefTable ClassDefTable2 => new(this.Memory[this.ClassDef2Offset..]);

            public (ValueRecord, ValueRecord) this[(int, int) v] => this[v.Item1, v.Item2];

            public (ValueRecord, ValueRecord) this[int class1Index, int class2Index] {
                get {
                    if (class1Index < 0 || class1Index >= this.Class1Count)
                        throw new IndexOutOfRangeException();

                    if (class2Index < 0 || class2Index >= this.Class2Count)
                        throw new IndexOutOfRangeException();

                    var offset = 16 + (2 * this.ValueSize * ((class1Index * this.Class2Count) + class2Index));
                    return (
                        new(this.Memory[offset..], this.ValueFormat1),
                        new(this.Memory[(offset + this.Value1Size)..], this.ValueFormat2));
                }
            }

            public IEnumerable<KerningPair> ExtractAdvanceX() {
                if ((this.ValueFormat1 & ValueFormatFlags.AdvanceX) == 0 &&
                    (this.ValueFormat2 & ValueFormatFlags.AdvanceX) == 0) {
                    yield break;
                }

                var class1Count = this.Class1Count;
                var class2Count = this.Class2Count;
                var classes1 = this.ClassDefTable1.Enumerate()
                    .Where(x => x.Class < class1Count)
                    .GroupBy(x => x.Class, x => x.GlyphId)
                    .ToImmutableDictionary(x => x.Key, x => x.ToImmutableSortedSet());

                var classes2 = this.ClassDefTable2.Enumerate()
                    .Where(x => x.Class < class2Count)
                    .GroupBy(x => x.Class, x => x.GlyphId)
                    .ToImmutableDictionary(x => x.Key, x => x.ToImmutableSortedSet());

                foreach (var (class1, glyphs1) in classes1) {
                    foreach (var (class2, glyphs2) in classes2) {
                        (ValueRecord, ValueRecord) record;
                        try {
                            record = this[class1, class2];
                        } catch (ArgumentOutOfRangeException) {
                            yield break;
                        } catch (IndexOutOfRangeException) {
                            yield break;
                        }

                        var val = record.Item1.AdvanceX + record.Item2.PlacementX;
                        if (val == 0)
                            continue;

                        foreach (var glyph1 in glyphs1) {
                            foreach (var glyph2 in glyphs2) {
                                yield return new(glyph1, glyph2, (short)val);
                            }
                        }
                    }
                }
            }
        }
    }

    public struct ExtensionPositioningSubtableFormat1 {
        public PointerSpan<byte> Memory;

        public ExtensionPositioningSubtableFormat1(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);
        public LookupType ExtensionLookupType => this.Memory.ReadEnumBig<LookupType>(2);
        public int ExtensionOffset => this.Memory.ReadI32Big(4);

        public PointerSpan<byte> ExtensionData => this.Memory[this.ExtensionOffset..];
    }

    public IEnumerable<KerningPair> ExtractAdvanceX() => this.EnumerateLookupTables()
        .SelectMany(lookupTable => lookupTable.Type switch {
            LookupType.PairAdjustment =>
                lookupTable
                    .SelectMany(y => new PairAdjustmentPositioning(y).ExtractAdvanceX()),
            LookupType.ExtensionPositioning =>
                lookupTable
                    .Where(y => y.ReadU16Big(0) == 1)
                    .Select(y => new ExtensionPositioningSubtableFormat1(y))
                    .Where(y => y.ExtensionLookupType == LookupType.PairAdjustment)
                    .SelectMany(y => new PairAdjustmentPositioning(y.ExtensionData).ExtractAdvanceX()),
            _ => Array.Empty<KerningPair>(),
        });
}
