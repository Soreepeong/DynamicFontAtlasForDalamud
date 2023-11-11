using Dalamud.Interface.GameFonts;

namespace DynamicFontAtlasLib.Internal;

internal static class Constants {
    public static readonly string?[] GameFontFileNames = {
        null,
        "AXIS_96", "AXIS_12", "AXIS_14", "AXIS_18", "AXIS_36",
        "Jupiter_16", "Jupiter_20", "Jupiter_23", "Jupiter_45", "Jupiter_46", "Jupiter_90",
        "Meidinger_16", "Meidinger_20", "Meidinger_40",
        "MiedingerMid_10", "MiedingerMid_12", "MiedingerMid_14", "MiedingerMid_18", "MiedingerMid_36",
        "TrumpGothic_184", "TrumpGothic_23", "TrumpGothic_34", "TrumpGothic_68",
    };

    public static string GetFdtPath(this GameFontFamilyAndSize gffas) =>
        $"common/font/{GameFontFileNames[(int)gffas]}.fdt";

    /// <summary>
    /// Primary fallback codepoint.
    /// Geta mark; FFXIV uses this to indicate that a glyph is missing.
    /// </summary>
    public const ushort Fallback1Codepoint = 0x3013;

    /// <summary>
    /// Secondary fallback codepoint.
    /// FFXIV uses dash if Geta mark is unavailable.
    /// </summary>
    public const ushort Fallback2Codepoint = '-';
}
