using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Files;

namespace DynamicFontAtlasLib.TrueType.Tables;

public struct Head {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/head
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6head.html

    public const uint MagicNumberValue = 0x5F0F3CF5;
    public static readonly TagStruct DirectoryTableTag = new('h', 'e', 'a', 'd');

    public PointerSpan<byte> Memory;

    public Head(SfntFile file) : this(file[DirectoryTableTag]) { }

    public Head(PointerSpan<byte> memory) => this.Memory = memory;
    
    public Fixed Version => new(this.Memory);
    public Fixed FontRevision => new(this.Memory[4..]);
    public uint ChecksumAdjustment => this.Memory.ReadU32Big(8);
    public uint MagicNumber => this.Memory.ReadU32Big(12);
    public HeadFlags Flags => this.Memory.ReadEnumBig<HeadFlags>(16);
    public ushort UnitsPerEm => this.Memory.ReadU16Big(18);
    public ulong CreatedTimestamp => this.Memory.ReadU64Big(20);
    public ulong ModifiedTimestamp => this.Memory.ReadU64Big(28);
    public ushort MinX => this.Memory.ReadU16Big(36);
    public ushort MinY => this.Memory.ReadU16Big(38);
    public ushort MaxX => this.Memory.ReadU16Big(40);
    public ushort MaxY => this.Memory.ReadU16Big(42);
    public MacStyleFlags MacStyle => this.Memory.ReadEnumBig<MacStyleFlags>(44);
    public ushort LowestRecommendedPpem => this.Memory.ReadU16Big(46);
    public ushort FontDirectionHint => this.Memory.ReadU16Big(48);
    public ushort IndexToLocFormat => this.Memory.ReadU16Big(50);
    public ushort GlyphDataFormat => this.Memory.ReadU16Big(52);

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
