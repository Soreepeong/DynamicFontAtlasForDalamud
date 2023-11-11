using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

// ReSharper disable All

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Cmap {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/cmap
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html

    public static readonly TagStruct DirectoryTableTag = new('c', 'm', 'a', 'p');

    public Memory<byte> Memory;
    public ushort Version;
    public EncodingRecord[] Records;

    public Cmap(Memory<byte> memory) {
        this.Memory = memory;
        var span = memory.Span;
        this.Version = BinaryPrimitives.ReadUInt16BigEndian(span);
        var numTables = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
        span = span[4..];

        this.Records = new EncodingRecord[numTables];
        for (var i = 0; i < this.Records.Length; i++) {
            this.Records[i] = new(span);
            span = span[Unsafe.SizeOf<EncodingRecord>()..];
        }
    }

    public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
        foreach (var record in this.Records) {
            var pae = record.PlatformAndEncoding;
            if (false
                || (pae.Platform == PlatformId.Unicode)
                || (pae.Platform == PlatformId.Windows && pae.WindowsEncoding == WindowsPlatformEncodingId.UnicodeBmp)
                || (pae.Platform == PlatformId.Windows && pae.WindowsEncoding == WindowsPlatformEncodingId.UnicodeFullRepertoire)) {
                var memory = this.Memory[record.SubtableOffset..];
                ICmapFormat? format = BinaryPrimitives.ReadUInt16BigEndian(memory.Span) switch {
                    0 => new Format0(memory.Span),
                    2 => new Format2(memory),
                    4 => new Format4(memory),
                    6 => new Format6(memory),
                    8 => new Format8(memory),
                    10 => new Format10(memory),
                    12 or 13 => new Format12And13(memory),
                    _ => null
                };

                if (format is null)
                    continue;

                foreach (var e in format)
                    yield return e;
            }
        }
    }

    public struct EncodingRecord {
        public PlatformAndEncoding PlatformAndEncoding;
        public int SubtableOffset;

        public EncodingRecord(Span<byte> span) {
            this.PlatformAndEncoding = new(span);
            this.SubtableOffset = BinaryPrimitives.ReadInt32BigEndian(span[4..]);
        }
    }

    public struct MapGroup : IComparable<MapGroup> {
        public int StartCharCode;
        public int EndCharCode;
        public int GlyphId;

        public MapGroup(Span<byte> span) {
            this.StartCharCode = BinaryPrimitives.ReadInt32BigEndian(span);
            this.EndCharCode = BinaryPrimitives.ReadInt32BigEndian(span[4..]);
            this.GlyphId = BinaryPrimitives.ReadInt32BigEndian(span[8..]);
        }

        public int CompareTo(MapGroup other) {
            var endCharCodeComparison = this.EndCharCode.CompareTo(other.EndCharCode);
            if (endCharCodeComparison != 0) return endCharCodeComparison;
            var startCharCodeComparison = this.StartCharCode.CompareTo(other.StartCharCode);
            if (startCharCodeComparison != 0) return startCharCodeComparison;
            return this.GlyphId.CompareTo(other.GlyphId);
        }
    }

    public interface ICmapFormat : IEnumerable<(ushort GlyphId, int Codepoint)> {
        public ushort CharToGlyph(int c);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public struct Format0 : ICmapFormat {
        public ushort Length;
        public ushort Language;
        public Bytes256 GlyphIdArray;

        public Format0(Span<byte> span) {
            this.Length = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            this.Language = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            this.GlyphIdArray = new(span[6..]);
        }

        public ushort CharToGlyph(int c) => c is >= 0 and < 256 ? this.GlyphIdArray[c] : (byte)0;

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var codepoint = 0; codepoint < 256; codepoint++)
                if (GlyphIdArray[codepoint] is var glyphId and not 0)
                    yield return (glyphId, codepoint);
        }
    }

    public struct Format2 : ICmapFormat {
        public ushort Length;
        public ushort Language; // Only used for Macintosh platforms
        public UShorts256 SubHeaderKeys;
        public Memory<byte> Data;

        public Format2(Memory<byte> memory) {
            var span = memory.Span;
            this.Length = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            this.Language = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            this.SubHeaderKeys = new(span[6..]);
            this.Data = memory[518..];
        }

        public bool TryGetSubHeader(int keyIndex, out SubHeader subheader, out Memory<byte> glyphArrayMemory) {
            if (keyIndex < 0 || keyIndex >= this.SubHeaderKeys.Count) {
                subheader = default;
                glyphArrayMemory = Memory<byte>.Empty;
                return false;
            }

            var offset = this.SubHeaderKeys[keyIndex];
            if (offset < 0 || offset + Unsafe.SizeOf<SubHeader>() > this.Data.Length) {
                subheader = default;
                glyphArrayMemory = Memory<byte>.Empty;
                return false;
            }

            subheader = new(this.Data.Span[offset..]);
            glyphArrayMemory = this.Data[(offset + Unsafe.SizeOf<SubHeader>())..];
            glyphArrayMemory = glyphArrayMemory[..Math.Min(glyphArrayMemory.Length, subheader.EntryCount * 2)];
            subheader.EntryCount = unchecked((ushort)(glyphArrayMemory.Length / 2));
            return true;
        }

        public ushort CharToGlyph(int c) {
            if (!TryGetSubHeader(c >> 8, out var sh, out var gam))
                return 0;

            c &= 0xFF;
            if (sh.FirstCode < c || c >= sh.FirstCode + sh.EntryCount)
                return 0;

            var res = BinaryPrimitives.ReadUInt16BigEndian(gam.Span[((c - sh.FirstCode) * 2)..]);
            return res == 0 ? (ushort)0 : unchecked((ushort)(res + sh.IdDelta));
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var i = 0; i < this.SubHeaderKeys.Count; i++) {
                if (!this.TryGetSubHeader(i, out var sh, out var gam))
                    continue;

                for (var j = 0; j < sh.EntryCount; j++) {
                    var res = BinaryPrimitives.ReadUInt16BigEndian(gam.Span[(j * 2)..]);
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

            public SubHeader(Span<byte> span) {
                this.FirstCode = BinaryPrimitives.ReadUInt16BigEndian(span);
                this.EntryCount = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
                this.IdDelta = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
                this.IdRangeOffset = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
            }
        }
    }

    public struct Format4 : ICmapFormat {
        public const int EndCodesOffset = 14;

        public Memory<byte> Data;
        public ushort FormatId;
        public ushort Length;
        public ushort Language; // Only used for Macintosh platforms
        public ushort SegCountX2;

        public Format4(Memory<byte> memory) {
            var span = memory.Span;
            this.Data = memory;
            this.Length = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            this.Language = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            this.SegCountX2 = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
        }

        public int StartCodesOffset => EndCodesOffset + 2 + (1 * this.SegCountX2);
        public int IdDeltasOffset => EndCodesOffset + 2 + (2 * this.SegCountX2);
        public int IdRangeOffsetsOffset => EndCodesOffset + 2 + (3 * this.SegCountX2);

        public ushort CharToGlyph(int c) {
            if (c < 0 || c >= 0x10000)
                return 0;

            var span = this.Data.Span;
            var startCodes = span.Slice(this.StartCodesOffset, this.SegCountX2);
            var endCodes = span.Slice(EndCodesOffset, this.SegCountX2);
            var idDeltas = span.Slice(this.IdDeltasOffset, this.SegCountX2);
            var idRangeOffsets = span.Slice(this.IdRangeOffsetsOffset, this.SegCountX2);

            var i = endCodes.BinarySearchBE((ushort)c);
            if (i < 0)
                return 0;

            var startCode = startCodes.UInt16At(i);
            var endCode = endCodes.UInt16At(i);
            if (c < startCode || c > endCode)
                return 0;

            var idRangeOffset = idRangeOffsets.UInt16At(i);
            var idDelta = idDeltas.UInt16At(i);
            if (idRangeOffset == 0)
                return unchecked((ushort)(c + idDelta));

            var ptr = this.IdRangeOffsetsOffset + i * 2 + idRangeOffset;
            if (ptr > span.Length)
                return 0;

            var glyphs = span[ptr..];
            var innerIndex = c - startCode;
            if (glyphs.Length < innerIndex * 2)
                return 0;

            var glyph = glyphs.UInt16At(innerIndex);
            return unchecked(glyph == 0 ? (ushort)0 : (ushort)(idDelta + glyph));
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var startCodes = this.Data.Slice(this.StartCodesOffset, this.SegCountX2);
            var endCodes = this.Data.Slice(EndCodesOffset, this.SegCountX2);
            var idDeltas = this.Data.Slice(this.IdDeltasOffset, this.SegCountX2);
            var idRangeOffsets = this.Data.Slice(this.IdRangeOffsetsOffset, this.SegCountX2);

            for (var i = 0; i < this.SegCountX2 / 2; i++) {
                var startCode = startCodes.Span.UInt16At(i);
                var endCode = endCodes.Span.UInt16At(i);
                var idRangeOffset = idRangeOffsets.Span.UInt16At(i);
                var idDelta = idDeltas.Span.UInt16At(i);

                var ptr = this.IdRangeOffsetsOffset + i * 2 + idRangeOffset;
                if (ptr > this.Data.Length)
                    continue;

                var glyphs = this.Data[ptr..];
                for (var j = startCode; j <= endCode; j++) {
                    var innerIndex = j - startCode;
                    if (glyphs.Length < innerIndex * 2)
                        continue;

                    var glyph = glyphs.Span.UInt16At(innerIndex);
                    if (glyph == 0)
                        continue;

                    yield return ((ushort)(glyph + idDelta), j);
                }
            }
        }
    }

    public struct Format6 : ICmapFormat {
        public Memory<byte> Data;
        public ushort Length;
        public ushort Language; // Only used for Macintosh platforms
        public ushort FirstCode;
        public ushort EntryCount;

        public Format6(Memory<byte> memory) {
            var span = memory.Span;
            this.Length = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            this.Language = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            this.FirstCode = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
            this.EntryCount = BinaryPrimitives.ReadUInt16BigEndian(span[8..]);
            this.Data = memory[10..];
            this.EntryCount = (ushort)Math.Min(this.EntryCount, this.Data.Length / 2u);
        }

        public ushort CharToGlyph(int c) {
            if (c < this.FirstCode || c >= this.FirstCode + this.EntryCount)
                return 0;

            return this.Data.Span.UInt16At(c - this.FirstCode);
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var i = 0; i < this.EntryCount; i++) {
                var g = this.Data.Span.UInt16At(i);
                if (g != 0)
                    yield return (g, this.FirstCode + i);
            }
        }
    }

    public struct Format8 : ICmapFormat {
        public int Length;
        public int Language;
        public int NumGroups;
        public Memory<byte> Is32;
        public Memory<byte> GroupsBytes;

        public Format8(Memory<byte> memory) {
            var span = memory.Span;
            this.Length = BinaryPrimitives.ReadInt32BigEndian(span[4..]);
            this.Language = BinaryPrimitives.ReadInt32BigEndian(span[8..]);
            this.Is32 = memory[12..];
            this.NumGroups = BinaryPrimitives.ReadInt32BigEndian(span[8204..]);
            this.GroupsBytes = memory.Slice(
                8208,
                Math.Min(memory.Length - 8208, this.NumGroups * Unsafe.SizeOf<MapGroup>()));
        }

        public ushort CharToGlyph(int c) {
            var tmp = new MapGroup() { EndCharCode = c };
            tmp.EndCharCode = c;

            var span = this.GroupsBytes.Span;
            var elementSize = Unsafe.SizeOf<MapGroup>();

            var i = span.BinarySearchBE(tmp, (s, i) => new(s[(i * elementSize)..]));
            if (i < 0)
                return 0;

            tmp = new(span[(i * elementSize)..]);
            if (c < tmp.StartCharCode || c > tmp.EndCharCode)
                return 0;

            return unchecked((ushort)(tmp.GlyphId + c - tmp.StartCharCode));
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var elementSize = Unsafe.SizeOf<MapGroup>();
            for (var i = 0; i < this.GroupsBytes.Length; i += elementSize) {
                var tmp = new MapGroup(this.GroupsBytes.Span[i..]);
                for (var j = tmp.StartCharCode; j <= tmp.EndCharCode; j++)
                    yield return ((ushort)(tmp.GlyphId + j - tmp.StartCharCode), j);
            }
        }
    }

    public struct Format10 : ICmapFormat {
        public int Length;
        public int Language;
        public int StartCharCode;
        public int NumChars;
        public Memory<byte> GlyphIdArrayBytes;

        public Format10(Memory<byte> memory) {
            var span = memory.Span;
            this.Length = BinaryPrimitives.ReadInt32BigEndian(span[4..]);
            this.Language = BinaryPrimitives.ReadInt32BigEndian(span[8..]);
            this.StartCharCode = BinaryPrimitives.ReadInt32BigEndian(span[12..]);
            this.NumChars = BinaryPrimitives.ReadInt32BigEndian(span[16..]);
            this.GlyphIdArrayBytes = memory.Slice(20, Math.Min(memory.Length - 20, this.NumChars * 2));
        }

        public ushort CharToGlyph(int c) {
            if (c < this.StartCharCode || c >= this.StartCharCode + this.NumChars)
                return 0;

            return this.GlyphIdArrayBytes.Span.UInt16At(c);
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            for (var i = 0; i < this.NumChars; i++) {
                var glyph = this.GlyphIdArrayBytes.Span.UInt16At(i);
                if (glyph != 0)
                    yield return (glyph, this.StartCharCode + i);
            }
        }
    }

    public struct Format12And13 : ICmapFormat {
        public ushort Format;
        public int Length;
        public int Language;
        public int NumGroups;
        public Memory<byte> GroupsBytes;

        public Format12And13(Memory<byte> memory) {
            var span = memory.Span;
            this.Format = BinaryPrimitives.ReadUInt16BigEndian(span[0..]);
            this.Length = BinaryPrimitives.ReadInt32BigEndian(span[4..]);
            this.Language = BinaryPrimitives.ReadInt32BigEndian(span[8..]);
            this.NumGroups = BinaryPrimitives.ReadInt32BigEndian(span[12..]);
            this.GroupsBytes = memory.Slice(
                16,
                Math.Min(memory.Length - 16, this.NumGroups * Unsafe.SizeOf<MapGroup>()));
        }

        public ushort CharToGlyph(int c) {
            var tmp = new MapGroup() { EndCharCode = c };
            tmp.EndCharCode = c;

            var span = this.GroupsBytes.Span;
            var elementSize = Unsafe.SizeOf<MapGroup>();

            var i = span.BinarySearchBE(tmp, (s, i) => new(s[(i * elementSize)..]));
            if (i < 0)
                return 0;

            tmp = new(span[(i * elementSize)..]);
            if (c < tmp.StartCharCode || c > tmp.EndCharCode)
                return 0;

            if (this.Format == 12)
                return (ushort)(tmp.GlyphId + c - tmp.StartCharCode);
            else
                return (ushort)tmp.GlyphId;
        }

        public IEnumerator<(ushort GlyphId, int Codepoint)> GetEnumerator() {
            var elementSize = Unsafe.SizeOf<MapGroup>();
            if (this.Format == 12) {
                for (var i = 0; i < this.GroupsBytes.Length; i += elementSize) {
                    var tmp = new MapGroup(this.GroupsBytes.Span[i..]);
                    for (var j = tmp.StartCharCode; j <= tmp.EndCharCode; j++)
                        yield return ((ushort)(tmp.GlyphId + j - tmp.StartCharCode), j);
                }
            } else {
                for (var i = 0; i < this.GroupsBytes.Length; i += elementSize) {
                    var tmp = new MapGroup(this.GroupsBytes.Span[i..]);
                    for (var j = tmp.StartCharCode; j <= tmp.EndCharCode; j++)
                        yield return ((ushort)tmp.GlyphId, j);
                }
            }
        }
    }
}
