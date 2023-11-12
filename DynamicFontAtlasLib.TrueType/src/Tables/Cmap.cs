using System.Buffers.Binary;
using System.Collections;
using System.Runtime.CompilerServices;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Enums;
using DynamicFontAtlasLib.TrueType.Files;

namespace DynamicFontAtlasLib.TrueType.Tables;

public struct Cmap {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/cmap
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html

    public static readonly TagStruct DirectoryTableTag = new('c', 'm', 'a', 'p');

    public PointerSpan<byte> Memory;

    public Cmap(SfntFile file) : this(file[DirectoryTableTag]) { }

    public Cmap(PointerSpan<byte> memory) => this.Memory = memory;

    public ushort Version => this.Memory.ReadU16Big(0);

    public ushort RecordCount => this.Memory.ReadU16Big(2);

    public BigEndianPointerSpan<EncodingRecord> Records => new(
        this.Memory[4..].As<EncodingRecord>(this.RecordCount),
        EncodingRecord.ReverseEndianness);

    public EncodingRecord? UnicodeEncodingRecord =>
        this.Records.Select(x => (EncodingRecord?)x).FirstOrDefault(x => x!.Value.PlatformAndEncoding is
            { Platform: PlatformId.Unicode, UnicodeEncoding: UnicodeEncodingId.Unicode_2_0_Bmp })
        ??
        this.Records.Select(x => (EncodingRecord?)x).FirstOrDefault(x => x!.Value.PlatformAndEncoding is
            { Platform: PlatformId.Unicode, UnicodeEncoding: UnicodeEncodingId.Unicode_2_0_Full })
        ??
        this.Records.Select(x => (EncodingRecord?)x).FirstOrDefault(x => x!.Value.PlatformAndEncoding is
            { Platform: PlatformId.Unicode, UnicodeEncoding: UnicodeEncodingId.UnicodeFullRepertoire })
        ??
        this.Records.Select(x => (EncodingRecord?)x).FirstOrDefault(x => x!.Value.PlatformAndEncoding is
            { Platform: PlatformId.Windows, WindowsEncoding: WindowsEncodingId.UnicodeBmp })
        ??
        this.Records.Select(x => (EncodingRecord?)x).FirstOrDefault(x => x!.Value.PlatformAndEncoding is
            { Platform: PlatformId.Windows, WindowsEncoding: WindowsEncodingId.UnicodeFullRepertoire });

    public CmapFormat? UnicodeTable => this.GetTable(this.UnicodeEncodingRecord);

    public CmapFormat? GetTable(EncodingRecord? encodingRecord) =>
        encodingRecord is { } record
            ? this.Memory.ReadU16Big(record.SubtableOffset) switch {
                0 => new CmapFormat0(this.Memory[record.SubtableOffset..]),
                2 => new CmapFormat2(this.Memory[record.SubtableOffset..]),
                4 => new CmapFormat4(this.Memory[record.SubtableOffset..]),
                6 => new CmapFormat6(this.Memory[record.SubtableOffset..]),
                8 => new CmapFormat8(this.Memory[record.SubtableOffset..]),
                10 => new CmapFormat10(this.Memory[record.SubtableOffset..]),
                12 or 13 => new CmapFormat12And13(this.Memory[record.SubtableOffset..]),
                _ => null,
            }
            : null;

    public struct EncodingRecord {
        public PlatformAndEncoding PlatformAndEncoding;
        public int SubtableOffset;

        public EncodingRecord(PointerSpan<byte> span) {
            this.PlatformAndEncoding = new(span);
            var offset = Unsafe.SizeOf<PlatformAndEncoding>();
            span.ReadBig(ref offset, out this.SubtableOffset);
        }

        public static EncodingRecord ReverseEndianness(EncodingRecord value) => new() {
            PlatformAndEncoding = PlatformAndEncoding.ReverseEndianness(value.PlatformAndEncoding),
            SubtableOffset = BinaryPrimitives.ReverseEndianness(value.SubtableOffset),
        };
    }

    public struct MapGroup : IComparable<MapGroup> {
        public int StartCharCode;
        public int EndCharCode;
        public int GlyphId;

        public MapGroup(PointerSpan<byte> span) {
            var offset = 0;
            span.ReadBig(ref offset, out this.StartCharCode);
            span.ReadBig(ref offset, out this.EndCharCode);
            span.ReadBig(ref offset, out this.GlyphId);
        }

        public int CompareTo(MapGroup other) {
            var endCharCodeComparison = this.EndCharCode.CompareTo(other.EndCharCode);
            if (endCharCodeComparison != 0) return endCharCodeComparison;
            var startCharCodeComparison = this.StartCharCode.CompareTo(other.StartCharCode);
            if (startCharCodeComparison != 0) return startCharCodeComparison;
            return this.GlyphId.CompareTo(other.GlyphId);
        }

        public static MapGroup ReverseEndianness(MapGroup obj) => new() {
            StartCharCode = BinaryPrimitives.ReverseEndianness(obj.StartCharCode),
            EndCharCode = BinaryPrimitives.ReverseEndianness(obj.EndCharCode),
            GlyphId = BinaryPrimitives.ReverseEndianness(obj.GlyphId),
        };
    }

    public abstract class CmapFormat : IReadOnlyDictionary<int, ushort> {
        public int Count => this.Count(x => x.Value != 0);

        public IEnumerable<int> Keys => this.Select(x => x.Key);

        public IEnumerable<ushort> Values => this.Select(x => x.Value);

        public ushort this[int key] => throw new NotImplementedException();

        public abstract ushort CharToGlyph(int c);

        public abstract IEnumerator<KeyValuePair<int, ushort>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool ContainsKey(int key) => this.CharToGlyph(key) != 0;

        public bool TryGetValue(int key, out ushort value) {
            value = this.CharToGlyph(key);
            return value != 0;
        }
    }

    public class CmapFormat0 : CmapFormat {
        public PointerSpan<byte> Memory;

        public CmapFormat0(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);
        public ushort Length => this.Memory.ReadU16Big(2);
        public ushort Language => this.Memory.ReadU16Big(4);
        public PointerSpan<byte> GlyphIdArray => this.Memory.Slice(6, 256);

        public override ushort CharToGlyph(int c) => c is >= 0 and < 256 ? this.GlyphIdArray[c] : (byte)0;

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            for (var codepoint = 0; codepoint < 256; codepoint++)
                if (this.GlyphIdArray[codepoint] is var glyphId and not 0)
                    yield return new(codepoint, glyphId);
        }
    }

    public class CmapFormat2 : CmapFormat {
        public PointerSpan<byte> Memory;

        public CmapFormat2(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);
        public ushort Length => this.Memory.ReadU16Big(2);
        public ushort Language => this.Memory.ReadU16Big(4);

        public BigEndianPointerSpan<ushort> SubHeaderKeys => new(
            this.Memory[6..].As<ushort>(256),
            BinaryPrimitives.ReverseEndianness);

        public PointerSpan<byte> Data => this.Memory[518..];

        public bool TryGetSubHeader(int keyIndex, out SubHeader subheader, out BigEndianPointerSpan<ushort> glyphSpan) {
            if (keyIndex < 0 || keyIndex >= this.SubHeaderKeys.Count) {
                subheader = default;
                glyphSpan = default;
                return false;
            }

            var offset = this.SubHeaderKeys[keyIndex];
            if (offset + Unsafe.SizeOf<SubHeader>() > this.Data.Length) {
                subheader = default;
                glyphSpan = default;
                return false;
            }

            subheader = new(this.Data[offset..]);
            glyphSpan = new(
                this.Data[(offset + Unsafe.SizeOf<SubHeader>() + subheader.IdRangeOffset)..]
                    .As<ushort>(subheader.EntryCount),
                BinaryPrimitives.ReverseEndianness);

            return true;
        }

        public override ushort CharToGlyph(int c) {
            if (!TryGetSubHeader(c >> 8, out var sh, out var glyphSpan))
                return 0;

            c = (c & 0xFF) - sh.FirstCode;
            if (0 < c || c >= glyphSpan.Count)
                return 0;

            var res = glyphSpan[c];
            return res == 0 ? (ushort)0 : unchecked((ushort)(res + sh.IdDelta));
        }

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            for (var i = 0; i < this.SubHeaderKeys.Count; i++) {
                if (!this.TryGetSubHeader(i, out var sh, out var glyphSpan))
                    continue;

                for (var j = 0; j < glyphSpan.Count; j++) {
                    var res = glyphSpan[j];
                    if (res == 0)
                        continue;

                    var glyphId = unchecked((ushort)(res + sh.IdDelta));
                    if (glyphId == 0)
                        continue;

                    var codepoint = (i << 8) | (sh.FirstCode + j);
                    yield return new(codepoint, glyphId);
                }
            }
        }

        public struct SubHeader {
            public ushort FirstCode;
            public ushort EntryCount;
            public ushort IdDelta;
            public ushort IdRangeOffset;

            public SubHeader(PointerSpan<byte> span) {
                var offset = 0;
                span.ReadBig(ref offset, out this.FirstCode);
                span.ReadBig(ref offset, out this.EntryCount);
                span.ReadBig(ref offset, out this.IdDelta);
                span.ReadBig(ref offset, out this.IdRangeOffset);
            }
        }
    }

    public class CmapFormat4 : CmapFormat {
        public const int EndCodesOffset = 14;

        public PointerSpan<byte> Memory;
        public ushort Format => this.Memory.ReadU16Big(0);
        public ushort Length => this.Memory.ReadU16Big(2);
        public ushort Language => this.Memory.ReadU16Big(4);
        public ushort SegCountX2 => this.Memory.ReadU16Big(6);
        public ushort SearchRange => this.Memory.ReadU16Big(8);
        public ushort EntrySelector => this.Memory.ReadU16Big(10);
        public ushort RangeShift => this.Memory.ReadU16Big(12);

        public CmapFormat4(PointerSpan<byte> memory) => this.Memory = memory;

        public BigEndianPointerSpan<ushort> EndCodes => new(
            this.Memory.Slice(EndCodesOffset, this.SegCountX2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public BigEndianPointerSpan<ushort> StartCodes => new(
            this.Memory.Slice(EndCodesOffset + 2 + (1 * this.SegCountX2), this.SegCountX2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public BigEndianPointerSpan<ushort> IdDeltas => new(
            this.Memory.Slice(EndCodesOffset + 2 + (2 * this.SegCountX2), this.SegCountX2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public BigEndianPointerSpan<ushort> IdRangeOffsets => new(
            this.Memory.Slice(EndCodesOffset + 2 + (3 * this.SegCountX2), this.SegCountX2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public BigEndianPointerSpan<ushort> GlyphIds => new(
            this.Memory.Slice(EndCodesOffset + 2 + (4 * this.SegCountX2), this.SegCountX2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public override ushort CharToGlyph(int c) {
            if (c is < 0 or >= 0x10000)
                return 0;

            var i = this.EndCodes.BinarySearch((ushort)c);
            if (i < 0)
                return 0;

            var startCode = this.StartCodes[i];
            var endCode = this.EndCodes[i];
            if (c < startCode || c > endCode)
                return 0;

            var idRangeOffset = this.IdRangeOffsets[i];
            var idDelta = this.IdDeltas[i];
            if (idRangeOffset == 0)
                return unchecked((ushort)(c + idDelta));

            var ptr = EndCodesOffset + 2 + (3 * this.SegCountX2) + i * 2 + idRangeOffset;
            if (ptr > this.Memory.Length)
                return 0;

            var glyphs = new BigEndianPointerSpan<ushort>(
                this.Memory[ptr..].As<ushort>(endCode - startCode + 1),
                BinaryPrimitives.ReverseEndianness);

            var glyph = glyphs[c - startCode];
            return unchecked(glyph == 0 ? (ushort)0 : (ushort)(idDelta + glyph));
        }

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            var startCodes = this.StartCodes;
            var endCodes = this.EndCodes;
            var idDeltas = this.IdDeltas;
            var idRangeOffsets = this.IdRangeOffsets;

            for (var i = 0; i < this.SegCountX2 / 2; i++) {
                var startCode = startCodes[i];
                var endCode = endCodes[i];
                var idRangeOffset = idRangeOffsets[i];
                var idDelta = idDeltas[i];

                if (idRangeOffset == 0) {
                    for (var c = (int)startCode; c <= endCode; c++)
                        yield return new(c, (ushort)(c + idDelta));
                } else {
                    var ptr = EndCodesOffset + 2 + (3 * this.SegCountX2) + i * 2 + idRangeOffset;
                    if (ptr >= this.Memory.Length)
                        continue;

                    var glyphs = new BigEndianPointerSpan<ushort>(
                        this.Memory[ptr..].As<ushort>(endCode - startCode + 1),
                        BinaryPrimitives.ReverseEndianness);

                    for (var j = 0; j < glyphs.Count; j++) {
                        var glyphId = glyphs[j];
                        if (glyphId == 0)
                            continue;

                        glyphId += idDelta;
                        if (glyphId == 0)
                            continue;

                        yield return new(startCode + j, glyphId);
                    }
                }
            }
        }
    }

    public class CmapFormat6 : CmapFormat {
        public PointerSpan<byte> Memory;

        public CmapFormat6(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);
        public ushort Length => this.Memory.ReadU16Big(2);
        public ushort Language => this.Memory.ReadU16Big(4);
        public ushort FirstCode => this.Memory.ReadU16Big(6);
        public ushort EntryCount => this.Memory.ReadU16Big(8);

        public BigEndianPointerSpan<ushort> GlyphIds => new(
            this.Memory[10..].As<ushort>(this.EntryCount),
            BinaryPrimitives.ReverseEndianness);

        public override ushort CharToGlyph(int c) {
            var glyphIds = this.GlyphIds;
            if (c < this.FirstCode || c >= this.FirstCode + this.GlyphIds.Count)
                return 0;

            return glyphIds[c - this.FirstCode];
        }

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            var glyphIds = this.GlyphIds;
            for (var i = 0; i < this.GlyphIds.Length; i++) {
                var g = glyphIds[i];
                if (g != 0)
                    yield return new(this.FirstCode + i, g);
            }
        }
    }

    public class CmapFormat8 : CmapFormat {
        public PointerSpan<byte> Memory;

        public CmapFormat8(PointerSpan<byte> memory) => this.Memory = memory;

        public int Format => this.Memory.ReadI32Big(0);
        public int Length => this.Memory.ReadI32Big(4);
        public int Language => this.Memory.ReadI32Big(8);
        public PointerSpan<byte> Is32 => this.Memory.Slice(12, 8192);
        public int NumGroups => this.Memory.ReadI32Big(8204);

        public BigEndianPointerSpan<MapGroup> Groups =>
            new(this.Memory[8208..].As<MapGroup>(), MapGroup.ReverseEndianness);

        public override ushort CharToGlyph(int c) {
            var groups = this.Groups;

            var i = groups.BinarySearch((in MapGroup value) => c.CompareTo(value.EndCharCode));
            if (i < 0)
                return 0;

            var group = groups[i];
            if (c < group.StartCharCode || c > group.EndCharCode)
                return 0;

            return unchecked((ushort)(group.GlyphId + c - group.StartCharCode));
        }

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            foreach (var group in this.Groups) {
                for (var j = group.StartCharCode; j <= group.EndCharCode; j++) {
                    var glyphId = (ushort)(group.GlyphId + j - group.StartCharCode);
                    if (glyphId == 0)
                        continue;

                    yield return new(j, glyphId);
                }
            }
        }
    }

    public class CmapFormat10 : CmapFormat {
        public PointerSpan<byte> Memory;

        public CmapFormat10(PointerSpan<byte> memory) => this.Memory = memory;

        public int Format => this.Memory.ReadI32Big(0);
        public int Length => this.Memory.ReadI32Big(4);
        public int Language => this.Memory.ReadI32Big(8);
        public int StartCharCode => this.Memory.ReadI32Big(12);
        public int NumChars => this.Memory.ReadI32Big(16);

        public BigEndianPointerSpan<ushort> GlyphIdArray => new(
            this.Memory.Slice(20, this.NumChars * 2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public override ushort CharToGlyph(int c) {
            if (c < this.StartCharCode || c >= this.StartCharCode + this.GlyphIdArray.Count)
                return 0;

            return this.GlyphIdArray[c];
        }

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            for (var i = 0; i < this.GlyphIdArray.Count; i++) {
                var glyph = this.GlyphIdArray[i];
                if (glyph != 0)
                    yield return new(this.StartCharCode + i, glyph);
            }
        }
    }

    public class CmapFormat12And13 : CmapFormat {
        public PointerSpan<byte> Memory;

        public CmapFormat12And13(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16Big(0);
        public int Length => this.Memory.ReadI32Big(4);
        public int Language => this.Memory.ReadI32Big(8);
        public int NumGroups => this.Memory.ReadI32Big(12);

        public BigEndianPointerSpan<MapGroup> Groups => new(
            this.Memory[16..].As<MapGroup>(this.NumGroups),
            MapGroup.ReverseEndianness);

        public override ushort CharToGlyph(int c) {
            var groups = this.Groups;

            var i = groups.BinarySearch(new MapGroup() { EndCharCode = c });
            if (i < 0)
                return 0;

            var group = groups[i];
            if (c < group.StartCharCode || c > group.EndCharCode)
                return 0;

            if (this.Format == 12)
                return (ushort)(group.GlyphId + c - group.StartCharCode);
            else
                return (ushort)group.GlyphId;
        }

        public override IEnumerator<KeyValuePair<int, ushort>> GetEnumerator() {
            var groups = this.Groups;
            if (this.Format == 12) {
                foreach (var group in groups) {
                    for (var j = group.StartCharCode; j <= group.EndCharCode; j++) {
                        var glyphId = (ushort)(group.GlyphId + j - group.StartCharCode);
                        if (glyphId == 0)
                            continue;

                        yield return new(j, glyphId);
                    }
                }
            } else {
                foreach (var group in groups) {
                    if (group.GlyphId == 0)
                        continue;

                    for (var j = group.StartCharCode; j <= group.EndCharCode; j++)
                        yield return new(j, (ushort)group.GlyphId);
                }
            }
        }
    }
}
