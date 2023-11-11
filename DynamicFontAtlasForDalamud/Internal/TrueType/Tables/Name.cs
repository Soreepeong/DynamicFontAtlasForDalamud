using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Name {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/name
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6name.html

    public static readonly TagStruct DirectoryTableTag = new('n', 'a', 'm', 'e');

    public ushort Version;
    public NameRecord[] NameRecords;
    public LanguageRecord[] LanguageRecords;
    public Memory<byte> Storage;

    public Name(Memory<byte> memory) {
        var span = memory.Span;

        this.Version = BinaryPrimitives.ReadUInt16BigEndian(span);
        var nameCount = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
        var storageOffset = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
        span = span[6..];

        this.NameRecords = new NameRecord[nameCount];
        for (var i = 0; i < this.NameRecords.Length; i++) {
            this.NameRecords[i] = new(span);
            span = span[Unsafe.SizeOf<NameRecord>()..];
        }

        if (this.Version == 0) {
            this.LanguageRecords = Array.Empty<LanguageRecord>();
        } else {
            var languageCount = BinaryPrimitives.ReadUInt16BigEndian(span);
            span = span[2..];

            this.LanguageRecords = new LanguageRecord[languageCount];
            for (var i = 0; i < this.LanguageRecords.Length; i++) {
                this.LanguageRecords[i] = new(span);
                span = span[Unsafe.SizeOf<LanguageRecord>()..];
            }
        }

        this.Storage = memory[storageOffset..];
    }

    public string GetNameByIndex(int nameIndex) {
        ref var record = ref this.NameRecords[nameIndex];
        return record.PlatformAndEncoding.Decode(this.Storage.Span.Slice(record.StringOffset, record.Length));
    }

    public string GetLanguageName(int languageIndex) {
        ref var record = ref this.LanguageRecords[languageIndex];
        return Encoding.ASCII.GetString(this.Storage.Span.Slice(record.LanguageTagOffset, record.Length));
    }

    public struct NameRecord {
        public PlatformAndEncoding PlatformAndEncoding;
        public ushort LanguageId;
        public NameId NameId;
        public ushort Length;
        public ushort StringOffset;

        public NameRecord(Span<byte> span) {
            this.PlatformAndEncoding = new(span);
            this.LanguageId = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            this.NameId = (NameId)BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
            this.Length = BinaryPrimitives.ReadUInt16BigEndian(span[8..]);
            this.StringOffset = BinaryPrimitives.ReadUInt16BigEndian(span[10..]);
        }
    }

    public struct LanguageRecord {
        public ushort Length;
        public ushort LanguageTagOffset;

        public LanguageRecord(Span<byte> span) {
            this.Length = BinaryPrimitives.ReadUInt16BigEndian(span);
            this.LanguageTagOffset = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
        }
    }
}
