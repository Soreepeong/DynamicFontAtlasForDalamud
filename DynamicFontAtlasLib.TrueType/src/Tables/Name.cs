using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Enums;
using DynamicFontAtlasLib.TrueType.Files;

namespace DynamicFontAtlasLib.TrueType.Tables;

public struct Name {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/name
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6name.html

    public static readonly TagStruct DirectoryTableTag = new('n', 'a', 'm', 'e');

    public PointerSpan<byte> Memory;

    public Name(SfntFile file) : this(file[DirectoryTableTag]) { }

    public Name(PointerSpan<byte> memory) => this.Memory = memory;

    public ushort Version => this.Memory.ReadU16Big(0);
    public ushort Count => this.Memory.ReadU16Big(2);
    public ushort StorageOffset => this.Memory.ReadU16Big(4);

    public BigEndianPointerSpan<NameRecord> NameRecords => new(
        this.Memory[6..].As<NameRecord>(this.Count),
        NameRecord.ReverseEndianness);

    public ushort LanguageCount =>
        this.Version == 0 ? (ushort)0 : this.Memory.ReadU16Big(6 + this.NameRecords.NumBytes);

    public BigEndianPointerSpan<LanguageRecord> LanguageRecords => this.Version == 0
        ? default
        : new(
            this.Memory[(8 + this.NameRecords.NumBytes)..].As<LanguageRecord>(this.LanguageCount),
            LanguageRecord.ReverseEndianness);

    public PointerSpan<byte> Storage => this.Memory[this.StorageOffset..];

    public string this[in NameRecord record] =>
        record.PlatformAndEncoding.Decode(this.Storage.Span.Slice(record.StringOffset, record.Length));

    public string this[in LanguageRecord record] =>
        Encoding.ASCII.GetString(this.Storage.Span.Slice(record.LanguageTagOffset, record.Length));

    public struct NameRecord {
        public PlatformAndEncoding PlatformAndEncoding;
        public ushort LanguageId;
        public NameId NameId;
        public ushort Length;
        public ushort StringOffset;

        public NameRecord(PointerSpan<byte> span) {
            this.PlatformAndEncoding = new(span);
            var offset = Unsafe.SizeOf<PlatformAndEncoding>();
            span.ReadBig(ref offset, out this.LanguageId);
            span.ReadBig(ref offset, out this.NameId);
            span.ReadBig(ref offset, out this.Length);
            span.ReadBig(ref offset, out this.StringOffset);
        }

        public static NameRecord ReverseEndianness(NameRecord value) => new() {
            PlatformAndEncoding = PlatformAndEncoding.ReverseEndianness(value.PlatformAndEncoding),
            LanguageId = BinaryPrimitives.ReverseEndianness(value.LanguageId),
            NameId = (NameId)BinaryPrimitives.ReverseEndianness((ushort)value.NameId),
            Length = BinaryPrimitives.ReverseEndianness(value.Length),
            StringOffset = BinaryPrimitives.ReverseEndianness(value.StringOffset),
        };
    }

    public struct LanguageRecord {
        public ushort Length;
        public ushort LanguageTagOffset;

        public LanguageRecord(PointerSpan<byte> span) {
            var offset = 0;
            span.ReadBig(ref offset, out this.Length);
            span.ReadBig(ref offset, out this.LanguageTagOffset);
        }

        public static LanguageRecord ReverseEndianness(LanguageRecord value) => new() {
            Length = BinaryPrimitives.ReverseEndianness(value.Length),
            LanguageTagOffset = BinaryPrimitives.ReverseEndianness(value.LanguageTagOffset),
        };
    }
}
