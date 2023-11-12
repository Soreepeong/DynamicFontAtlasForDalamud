namespace DynamicFontAtlasLib.TrueType;

#pragma warning disable CS0649
public enum LookupType : ushort {
    SingleAdjustment = 1,
    PairAdjustment = 2,
    CursiveAttachment = 3,
    MarkToBaseAttachment = 4,
    MarkToLigatureAttachment = 5,
    MarkToMarkAttachment = 6,
    ContextPositioning = 7,
    ChainedContextPositioning = 8,
    ExtensionPositioning = 9,
}
