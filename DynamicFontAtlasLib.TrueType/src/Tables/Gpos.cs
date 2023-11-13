using System.Buffers.Binary;
using System.Collections.Immutable;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Files;
using DynamicFontAtlasLib.TrueType.Tables.GposGsub;

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

    public struct ValueRecord {
        public short PlacementX;
        public short PlacementY;
        public short AdvanceX;
        public short AdvanceY;
        public short PlacementDeviceOffsetX;
        public short PlacementDeviceOffsetY;
        public short AdvanceDeviceOffsetX;
        public short AdvanceDeviceOffsetY;

        public ValueRecord(PointerSpan<byte> pointerSpan, ValueFormat valueFormat) {
            var offset = 0;
            if ((valueFormat & ValueFormat.PlacementX) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementX);

            if ((valueFormat & ValueFormat.PlacementY) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementY);

            if ((valueFormat & ValueFormat.AdvanceX) != 0) pointerSpan.ReadBig(ref offset, out this.AdvanceX);
            if ((valueFormat & ValueFormat.AdvanceY) != 0) pointerSpan.ReadBig(ref offset, out this.AdvanceY);
            if ((valueFormat & ValueFormat.PlacementDeviceOffsetX) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementDeviceOffsetX);

            if ((valueFormat & ValueFormat.PlacementDeviceOffsetY) != 0)
                pointerSpan.ReadBig(ref offset, out this.PlacementDeviceOffsetY);

            if ((valueFormat & ValueFormat.AdvanceDeviceOffsetX) != 0)
                pointerSpan.ReadBig(ref offset, out this.AdvanceDeviceOffsetX);

            if ((valueFormat & ValueFormat.AdvanceDeviceOffsetY) != 0)
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
            public ValueFormat ValueFormat1 => this.Memory.ReadEnumBig<ValueFormat>(4);
            public ValueFormat ValueFormat2 => this.Memory.ReadEnumBig<ValueFormat>(6);
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
                if ((this.ValueFormat1 & ValueFormat.AdvanceX) == 0 &&
                    (this.ValueFormat2 & ValueFormat.AdvanceX) == 0) {
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
                                if (adj >= 10000)
                                    System.Diagnostics.Debugger.Break();

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

            public readonly struct PairSet {
                public readonly PointerSpan<byte> Memory;
                public readonly ValueFormat ValueFormat1;
                public readonly ValueFormat ValueFormat2;
                public readonly int PairValue1Size;
                public readonly int PairValue2Size;
                public readonly int PairSize;

                public PairSet(
                    PointerSpan<byte> memory,
                    ValueFormat valueFormat1,
                    ValueFormat valueFormat2) {
                    this.Memory = memory;
                    this.ValueFormat1 = valueFormat1;
                    this.ValueFormat2 = valueFormat2;
                    this.PairValue1Size = this.ValueFormat1.NumBytes();
                    this.PairValue2Size = this.ValueFormat2.NumBytes();
                    this.PairSize = 2 + this.PairValue1Size + this.PairValue2Size;
                }

                public ushort Count => this.Memory.ReadU16Big(0);

                public PairValueRecord this[int index] {
                    get {
                        var pvr = this.Memory.Slice(2 + (this.PairSize * index), this.PairSize);
                        return new() {
                            SecondGlyph = pvr.ReadU16Big(0),
                            Record1 = new(pvr.Slice(2, this.PairValue1Size), this.ValueFormat1),
                            Record2 = new(pvr.Slice(2 + this.PairValue1Size, this.PairValue2Size), this.ValueFormat2),
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

        public readonly struct Format2 {
            public readonly PointerSpan<byte> Memory;

            public Format2(PointerSpan<byte> memory) {
                this.Memory = memory;
                this.PairValue1Size = this.ValueFormat1.NumBytes();
                this.PairValue2Size = this.ValueFormat2.NumBytes();
                this.PairSize = this.PairValue1Size + this.PairValue2Size;
            }

            public ushort Format => this.Memory.ReadU16Big(0);
            public ushort CoverageOffset => this.Memory.ReadU16Big(2);
            public ValueFormat ValueFormat1 => this.Memory.ReadEnumBig<ValueFormat>(4);
            public ValueFormat ValueFormat2 => this.Memory.ReadEnumBig<ValueFormat>(6);
            public ushort ClassDef1Offset => this.Memory.ReadU16Big(8);
            public ushort ClassDef2Offset => this.Memory.ReadU16Big(10);
            public ushort Class1Count => this.Memory.ReadU16Big(12);
            public ushort Class2Count => this.Memory.ReadU16Big(14);

            public readonly int PairValue1Size;
            public readonly int PairValue2Size;
            public readonly int PairSize;

            public ClassDefTable ClassDefTable1 => new(this.Memory[this.ClassDef1Offset..]);
            public ClassDefTable ClassDefTable2 => new(this.Memory[this.ClassDef2Offset..]);

            public (ValueRecord, ValueRecord) this[(int, int) v] => this[v.Item1, v.Item2];

            public (ValueRecord, ValueRecord) this[int class1Index, int class2Index] {
                get {
                    if (class1Index < 0 || class1Index >= this.Class1Count)
                        throw new IndexOutOfRangeException();

                    if (class2Index < 0 || class2Index >= this.Class2Count)
                        throw new IndexOutOfRangeException();

                    var offset = 16 + (this.PairSize * ((class1Index * this.Class2Count) + class2Index));
                    return (
                        new(this.Memory.Slice(offset, this.PairValue1Size), this.ValueFormat1),
                        new(this.Memory.Slice(offset + this.PairValue1Size, this.PairValue2Size), this.ValueFormat2));
                }
            }

            public IEnumerable<KerningPair> ExtractAdvanceX() {
                if ((this.ValueFormat1 & ValueFormat.AdvanceX) == 0 &&
                    (this.ValueFormat2 & ValueFormat.AdvanceX) == 0) {
                    yield break;
                }

                var classes1 = this.ClassDefTable1.Enumerate()
                    .GroupBy(x => x.Class, x => x.GlyphId)
                    .ToImmutableDictionary(x => x.Key, x => x.ToImmutableSortedSet());

                var classes2 = this.ClassDefTable2.Enumerate()
                    .GroupBy(x => x.Class, x => x.GlyphId)
                    .ToImmutableDictionary(x => x.Key, x => x.ToImmutableSortedSet());

                foreach (var class1 in Enumerable.Range(0, this.Class1Count)) {
                    if (!classes1.TryGetValue((ushort)class1, out var glyphs1))
                        continue;

                    foreach (var class2 in Enumerable.Range(0, this.Class2Count)) {
                        if (!classes2.TryGetValue((ushort)class2, out var glyphs2))
                            continue;

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
