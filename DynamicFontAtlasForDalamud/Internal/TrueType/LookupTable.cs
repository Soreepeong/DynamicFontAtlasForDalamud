using System;
using System.Runtime.CompilerServices;

namespace DynamicFontAtlasLib.Internal.TrueType;

#pragma warning disable CS0649
public readonly ref struct LookupTable {
    private readonly Span<byte> data;

    public LookupTable(Span<byte> data) => this.data = data;

    public ref HeaderStruct Header => ref this.data.AsRef<HeaderStruct>();

    public Span<ushort> SubtableOffsets =>
        this.data[Unsafe.SizeOf<HeaderStruct>()..].AsSpan<ushort>(this.Header.SubtableCount);

    public struct HeaderStruct {
        private ushort LookupTypeUInt16;
        public byte MarkAttachmentType;
        public LookupFlags LookupFlag;
        public ushort SubtableCount;

        public LookupType LookupType {
            get => (LookupType)this.LookupTypeUInt16;
            set => this.LookupTypeUInt16 = (ushort)value;
        }
    }
}
