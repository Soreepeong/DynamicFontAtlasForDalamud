using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DynamicFontAtlasLib.Internal.TrueType.CommonStructs;
using DynamicFontAtlasLib.Internal.TrueType.Enums;

// ReSharper disable All

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Cmap {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/cmap
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html

    public static readonly TagStruct DirectoryTableTag = new('c', 'm', 'a', 'p');

    public PointerSpan<byte> Memory;
    public ushort Version;
    public EncodingRecord[] Records;

    public Cmap(PointerSpan<byte> memory) {
        this.Memory = memory;
        this.Memory.ReadBE(0, out this.Version);
        this.Memory.ReadBE(2, out ushort numTables);
        memory = memory[4..];

        this.Records = new EncodingRecord[numTables];
        for (var i = 0; i < this.Records.Length; i++) {
            this.Records[i] = new(memory);
            memory = memory[Unsafe.SizeOf<EncodingRecord>()..];
        }
    }

    public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
        foreach (var record in this.Records) {
            var pae = record.PlatformAndEncoding;
            if (false
                || (pae.Platform == PlatformId.Unicode)
                || (pae.Platform == PlatformId.Windows && pae.WindowsEncoding == WindowsPlatformEncodingId.UnicodeBmp)
                || (pae.Platform == PlatformId.Windows &&
                    pae.WindowsEncoding == WindowsPlatformEncodingId.UnicodeFullRepertoire)) {
                var memory = this.Memory[record.SubtableOffset..];
                ICmapFormat? formatReader = memory.ReadU16BE(0) switch {
                    0 => new Format0(memory),
                    2 => new Format2(memory),
                    4 => new Format4(memory),
                    6 => new Format6(memory),
                    8 => new Format8(memory),
                    10 => new Format10(memory),
                    12 or 13 => new Format12And13(memory),
                    _ => null
                };

                if (formatReader is null)
                    continue;

                foreach (var e in formatReader)
                    yield return e;
            }
        }
    }

    public struct EncodingRecord {
        public PlatformAndEncoding PlatformAndEncoding;
        public int SubtableOffset;

        public EncodingRecord(PointerSpan<byte> span) {
            this.PlatformAndEncoding = new(span);
            var offset = Unsafe.SizeOf<PlatformAndEncoding>();
            span.ReadBE(ref offset, out this.SubtableOffset);
        }
    }

    public struct MapGroup : IComparable<MapGroup> {
        public int StartCharCode;
        public int EndCharCode;
        public int GlyphId;

        public MapGroup(PointerSpan<byte> span) {
            var offset = 0;
            span.ReadBE(ref offset, out this.StartCharCode);
            span.ReadBE(ref offset, out this.EndCharCode);
            span.ReadBE(ref offset, out this.GlyphId);
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

    public interface ICmapFormat : IEnumerable<(ushort GlyphId, int Codepoint)> {
        public ushort CharToGlyph(int c);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public struct Format0 : ICmapFormat {
        public PointerSpan<byte> Memory;

        public Format0(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16BE(0);
        public ushort Length => this.Memory.ReadU16BE(2);
        public ushort Language => this.Memory.ReadU16BE(4);
        public PointerSpan<byte> GlyphIdArray => this.Memory.Slice(6, 256);

        public ushort CharToGlyph(int c) => c is >= 0 and < 256 ? this.GlyphIdArray[c] : (byte)0;

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var codepoint = 0; codepoint < 256; codepoint++)
                if (this.GlyphIdArray[codepoint] is var glyphId and not 0)
                    yield return (glyphId, codepoint);
        }
    }

    public struct Format2 : ICmapFormat {
        public PointerSpan<byte> Memory;

        public Format2(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16BE(0);
        public ushort Length => this.Memory.ReadU16BE(2);
        public ushort Language => this.Memory.ReadU16BE(4);
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
            if (offset < 0 || offset + Unsafe.SizeOf<SubHeader>() > this.Data.Length) {
                subheader = default;
                glyphSpan = default;
                return false;
            }

            subheader = new(this.Data[offset..]);
            glyphSpan = new(
                this.Data[(offset + Unsafe.SizeOf<SubHeader>())..].As<ushort>(subheader.EntryCount),
                BinaryPrimitives.ReverseEndianness);
            return true;
        }

        public ushort CharToGlyph(int c) {
            if (!TryGetSubHeader(c >> 8, out var sh, out var glyphSpan))
                return 0;

            c = (c & 0xFF) - sh.FirstCode;
            if (0 < c || c >= glyphSpan.Count)
                return 0;

            var res = glyphSpan[c];
            return res == 0 ? (ushort)0 : unchecked((ushort)(res + sh.IdDelta));
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var i = 0; i < this.SubHeaderKeys.Count; i++) {
                if (!this.TryGetSubHeader(i, out var sh, out var glyphSpan))
                    continue;

                for (var j = 0; j < glyphSpan.Count; j++) {
                    var res = glyphSpan[j];
                    if (res == 0)
                        continue;

                    var glyphId = unchecked((ushort)(res + sh.IdDelta));
                    var codepoint = (i << 8) | (sh.FirstCode + j);
                    yield return (glyphId, codepoint);
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
                span.ReadBE(ref offset, out this.FirstCode);
                span.ReadBE(ref offset, out this.EntryCount);
                span.ReadBE(ref offset, out this.IdDelta);
                span.ReadBE(ref offset, out this.IdRangeOffset);
            }
        }
    }

    public struct Format4 : ICmapFormat {
        public const int EndCodesOffset = 14;

        public PointerSpan<byte> Memory;
        public ushort Format => this.Memory.ReadU16BE(0);
        public ushort Length => this.Memory.ReadU16BE(2);
        public ushort Language => this.Memory.ReadU16BE(4);
        public ushort SegCountX2 => this.Memory.ReadU16BE(6);
        public ushort SearchRange => this.Memory.ReadU16BE(8);
        public ushort EntrySelector => this.Memory.ReadU16BE(10);
        public ushort RangeShift => this.Memory.ReadU16BE(12);

        public Format4(PointerSpan<byte> memory) => this.Memory = memory;

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

        public ushort CharToGlyph(int c) {
            if (c < 0 || c >= 0x10000)
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
                this.Memory[ptr..].As<ushort>(),
                BinaryPrimitives.ReverseEndianness);
            var innerIndex = c - startCode;
            if (glyphs.Count < innerIndex * 2)
                return 0;

            var glyph = glyphs[innerIndex];
            return unchecked(glyph == 0 ? (ushort)0 : (ushort)(idDelta + glyph));
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var startCodes = this.StartCodes;
            var endCodes = this.EndCodes;
            var idDeltas = this.IdDeltas;
            var idRangeOffsets = this.IdRangeOffsets;

            for (var i = 0; i < this.SegCountX2 / 2; i++) {
                var startCode = startCodes[i];
                var endCode = endCodes[i];
                var idRangeOffset = idRangeOffsets[i];
                var idDelta = idDeltas[i];

                var ptr = EndCodesOffset + 2 + (3 * this.SegCountX2) + i * 2 + idRangeOffset;
                if (ptr > this.Memory.Length)
                    continue;

                var glyphs = new BigEndianPointerSpan<ushort>(
                    this.Memory[ptr..].As<ushort>(),
                    BinaryPrimitives.ReverseEndianness);
                for (var j = startCode; j <= endCode; j++) {
                    var innerIndex = j - startCode;
                    if (glyphs.Count < innerIndex * 2)
                        continue;

                    var glyph = glyphs[innerIndex];
                    if (glyph == 0)
                        continue;

                    yield return ((ushort)(glyph + idDelta), j);
                }
            }
        }
    }

    public struct Format6 : ICmapFormat {
        public PointerSpan<byte> Memory;
        
        public Format6(PointerSpan<byte> memory) {
            var span = memory.Span;
            this.Memory = memory[10..];
        }
        
        public ushort Format => this.Memory.ReadU16BE(0);
        public ushort Length => this.Memory.ReadU16BE(2);
        public ushort Language => this.Memory.ReadU16BE(4);
        public ushort FirstCode => this.Memory.ReadU16BE(6);
        public ushort EntryCount => this.Memory.ReadU16BE(8);

        public BigEndianPointerSpan<ushort> GlyphIds => new(
            this.Memory[10..].As<ushort>(this.EntryCount),
            BinaryPrimitives.ReverseEndianness);

        public ushort CharToGlyph(int c) {
            var glyphIds = this.GlyphIds;
            if (c < this.FirstCode || c >= this.FirstCode + this.GlyphIds.Count)
                return 0;

            return glyphIds[c - this.FirstCode];
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var glyphIds = this.GlyphIds;
            for (var i = 0; i < this.GlyphIds.Length; i++) {
                var g = glyphIds[i];
                if (g != 0)
                    yield return (g, this.FirstCode + i);
            }
        }
    }

    public struct Format8 : ICmapFormat {
        public PointerSpan<byte> Memory;

        public Format8(PointerSpan<byte> memory) => this.Memory = memory;

        public int Format => this.Memory.ReadI32BE(0);
        public int Length => this.Memory.ReadI32BE(4);
        public int Language => this.Memory.ReadI32BE(8);
        public PointerSpan<byte> Is32 => this.Memory.Slice(12, 8192);
        public int NumGroups => this.Memory.ReadI32BE(8204);

        public BigEndianPointerSpan<MapGroup> Groups => new(this.Memory[8208..].As<MapGroup>(), MapGroup.ReverseEndianness);

        public ushort CharToGlyph(int c) {
            var groups = this.Groups;
            var elementSize = Unsafe.SizeOf<MapGroup>();

            var i = groups.BinarySearch(new() { EndCharCode = c });
            if (i < 0)
                return 0;

            var group = groups[i];
            if (c < group.StartCharCode || c > group.EndCharCode)
                return 0;

            return unchecked((ushort)(group.GlyphId + c - group.StartCharCode));
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var groups = this.Groups;
            foreach (var group in this.Groups) {
                for (var j = group.StartCharCode; j <= group.EndCharCode; j++)
                    yield return ((ushort)(group.GlyphId + j - group.StartCharCode), j);
            }
        }
    }

    public struct Format10 : ICmapFormat {
        public PointerSpan<byte> Memory;

        public Format10(PointerSpan<byte> memory) => this.Memory = memory;

        public int Format => this.Memory.ReadI32BE(0);
        public int Length => this.Memory.ReadI32BE(4);
        public int Language => this.Memory.ReadI32BE(8);
        public int StartCharCode => this.Memory.ReadI32BE(12);
        public int NumChars => this.Memory.ReadI32BE(16);
        public BigEndianPointerSpan<ushort> GlyphIdArray => new(
            this.Memory.Slice(20, this.NumChars * 2).As<ushort>(),
            BinaryPrimitives.ReverseEndianness);

        public ushort CharToGlyph(int c) {
            if (c < this.StartCharCode || c >= this.StartCharCode + this.GlyphIdArray.Count)
                return 0;

            return this.GlyphIdArray[c];
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var i = 0; i < this.GlyphIdArray.Count; i++) {
                var glyph = this.GlyphIdArray[i];
                if (glyph != 0)
                    yield return (glyph, this.StartCharCode + i);
            }
        }
    }

    public struct Format12And13 : ICmapFormat {
        public PointerSpan<byte> Memory;

        public Format12And13(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Format => this.Memory.ReadU16BE(0);
        public int Length => this.Memory.ReadI32BE(4);
        public int Language => this.Memory.ReadI32BE(8);
        public int NumGroups => this.Memory.ReadI32BE(12);
        public BigEndianPointerSpan<MapGroup> Groups => new(
            this.Memory[16..].As<MapGroup>(this.NumGroups),
            MapGroup.ReverseEndianness);
        
        public ushort CharToGlyph(int c) {
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

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var elementSize = Unsafe.SizeOf<MapGroup>();
            var groups = this.Groups;
            if (this.Format == 12) {
                for (var i = 0; i < groups.Length; i++) {
                    var group = groups[i];
                    for (var j = group.StartCharCode; j <= group.EndCharCode; j++)
                        yield return ((ushort)(group.GlyphId + j - group.StartCharCode), j);
                }
            } else {
                for (var i = 0; i < groups.Length; i++) {
                    var group = groups[i];
                    for (var j = group.StartCharCode; j <= group.EndCharCode; j++)
                        yield return ((ushort)group.GlyphId, j);
                }
            }
        }
    }
}
