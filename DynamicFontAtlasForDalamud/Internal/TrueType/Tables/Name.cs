using System;
using System.Runtime.CompilerServices;
using System.Text;
using DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Name {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/name
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6name.html

    public static readonly TagStruct DirectoryTableTag = new('n', 'a', 'm', 'e');

    public ushort Version;
    public NameRecord[] NameRecords;
    public LanguageRecord[] LanguageRecords;
    public PointerSpan<byte> Storage;

    public Name(PointerSpan<byte> memory) {
        var offset = 0;
        memory.ReadBE(ref offset, out this.Version);
        memory.ReadBE(ref offset, out ushort nameCount);
        memory.ReadBE(ref offset, out ushort storageOffset);

        this.NameRecords = new NameRecord[nameCount];
        for (var i = 0; i < this.NameRecords.Length; i++) {
            this.NameRecords[i] = new(memory[offset..]);
            offset += Unsafe.SizeOf<NameRecord>();
        }

        if (this.Version == 0) {
            this.LanguageRecords = Array.Empty<LanguageRecord>();
        } else {
            memory.ReadBE(ref offset, out ushort languageCount);

            this.LanguageRecords = new LanguageRecord[languageCount];
            for (var i = 0; i < this.LanguageRecords.Length; i++) {
                this.LanguageRecords[i] = new(memory[offset..]);
                offset += Unsafe.SizeOf<LanguageRecord>();
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

        public NameRecord(PointerSpan<byte> span) {
            this.PlatformAndEncoding = new(span);
            var offset = Unsafe.SizeOf<PlatformAndEncoding>();
            span.ReadBE(ref offset, out this.LanguageId);
            span.ReadBE(ref offset, out this.NameId);
            span.ReadBE(ref offset, out this.Length);
            span.ReadBE(ref offset, out this.StringOffset);
        }
    }

    public struct LanguageRecord {
        public ushort Length;
        public ushort LanguageTagOffset;

        public LanguageRecord(PointerSpan<byte> span) {
            var offset = 0;
            span.ReadBE(ref offset, out this.Length); 
            span.ReadBE(ref offset, out this.LanguageTagOffset);
        }
    }
}
