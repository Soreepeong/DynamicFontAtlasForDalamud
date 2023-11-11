using System;
using System.Buffers.Binary;

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Head {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/head
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6head.html

    public const uint MagicNumberValue = 0x5F0F3CF5;
    public static readonly TagStruct DirectoryTableTag = new('h', 'e', 'a', 'd');

    public Fixed Version;
    public Fixed FontRevision;
    public uint ChecksumAdjustment;
    public uint MagicNumber;
    public HeadFlags Flags;
    public ushort UnitsPerEm;
    public ulong CreatedTimestamp;
    public ulong ModifiedTimestamp;
    public ushort MinX;
    public ushort MinY;
    public ushort MaxX;
    public ushort MaxY;
    public MacStyleFlags MacStyle;
    public ushort LowestRecommendedPpem;
    public ushort FontDirectionHint;
    public ushort IndexToLocFormat;
    public ushort GlyphDataFormat;

    public Head(Span<byte> span) {
        this.Version = new(span);
        this.FontRevision = new(span[4..]);
        this.ChecksumAdjustment = BinaryPrimitives.ReadUInt32BigEndian(span[8..]);
        this.MagicNumber = BinaryPrimitives.ReadUInt32BigEndian(span[12..]);
        this.Flags = (HeadFlags)BinaryPrimitives.ReadUInt16BigEndian(span[16..]);
        this.UnitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(span[18..]);
        this.CreatedTimestamp = BinaryPrimitives.ReadUInt64BigEndian(span[20..]);
        this.ModifiedTimestamp = BinaryPrimitives.ReadUInt64BigEndian(span[28..]);
        this.MinX = BinaryPrimitives.ReadUInt16BigEndian(span[36..]);
        this.MinY = BinaryPrimitives.ReadUInt16BigEndian(span[38..]);
        this.MaxX = BinaryPrimitives.ReadUInt16BigEndian(span[40..]);
        this.MaxY = BinaryPrimitives.ReadUInt16BigEndian(span[42..]);
        this.MacStyle = (MacStyleFlags)BinaryPrimitives.ReadUInt16BigEndian(span[44..]);
        this.LowestRecommendedPpem = BinaryPrimitives.ReadUInt16BigEndian(span[46..]);
        this.FontDirectionHint = BinaryPrimitives.ReadUInt16BigEndian(span[48..]);
        this.IndexToLocFormat = BinaryPrimitives.ReadUInt16BigEndian(span[50..]);
        this.GlyphDataFormat = BinaryPrimitives.ReadUInt16BigEndian(span[52..]);
    }

    [Flags]
    public enum HeadFlags : ushort {
        BaselineForFontAtZeroY = 1 << 0,
        LeftSideBearingAtZeroX = 1 << 1,
        InstructionsDependOnPointSize = 1 << 2,
        ForcePpemsInteger = 1 << 3,
        InstructionsAlterAdvanceWidth = 1 << 4,
        VerticalLayout = 1 << 5,
        Reserved6 = 1 << 6,
        RequiresLayoutForCorrectLinguisticRendering = 1 << 7,
        IsAatFont = 1 << 8,
        ContainsRtlGlyph = 1 << 9,
        ContainsIndicStyleRearrangementEffects = 1 << 10,
        Lossless = 1 << 11,
        ProduceCompatibleMetrics = 1 << 12,
        OptimizedForClearType = 1 << 13,
        IsLastResortFont = 1 << 14,
        Reserved15 = 1 << 15,
    }

    [Flags]
    public enum MacStyleFlags : ushort {
        Bold = 1 << 0,
        Italic = 1 << 1,
        Underline = 1 << 2,
        Outline = 1 << 3,
        Shadow = 1 << 4,
        Condensed = 1 << 5,
        Extended = 1 << 6,
    }
}
