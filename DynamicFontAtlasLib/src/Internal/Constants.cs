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

    /// <summary>
    /// Gets the recommend GameFontFamilyAndSize given family and size.
    /// </summary>
    /// <param name="family">Font family.</param>
    /// <param name="size">Font size in points.</param>
    /// <returns>Recommended GameFontFamilyAndSize.</returns>
    public static GameFontFamilyAndSize GetRecommendedFamilyAndSize(GameFontFamily family, float size) =>
        family switch {
            _ when size <= 0 => GameFontFamilyAndSize.Undefined,
            GameFontFamily.Undefined => GameFontFamilyAndSize.Undefined,
            GameFontFamily.Axis => size switch {
                <= ((int)((9.6f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Axis96,
                <= ((int)((12f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Axis12,
                <= ((int)((14f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Axis14,
                <= ((int)((18f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Axis18,
                _ => GameFontFamilyAndSize.Axis36,
            },
            GameFontFamily.Jupiter => size switch {
                <= ((int)((16f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Jupiter16,
                <= ((int)((20f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Jupiter20,
                <= ((int)((23f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Jupiter23,
                _ => GameFontFamilyAndSize.Jupiter46,
            },
            GameFontFamily.JupiterNumeric => size switch {
                <= ((int)((45f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Jupiter45,
                _ => GameFontFamilyAndSize.Jupiter90,
            },
            GameFontFamily.Meidinger => size switch {
                <= ((int)((16f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Meidinger16,
                <= ((int)((20f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.Meidinger20,
                _ => GameFontFamilyAndSize.Meidinger40,
            },
            GameFontFamily.MiedingerMid => size switch {
                <= ((int)((10f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.MiedingerMid10,
                <= ((int)((12f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.MiedingerMid12,
                <= ((int)((14f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.MiedingerMid14,
                <= ((int)((18f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.MiedingerMid18,
                _ => GameFontFamilyAndSize.MiedingerMid36,
            },
            GameFontFamily.TrumpGothic => size switch {
                <= ((int)((18.4f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.TrumpGothic184,
                <= ((int)((23f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.TrumpGothic23,
                <= ((int)((34f * 4f / 3f) + 0.5f) * 3f / 4f) + 0.001f => GameFontFamilyAndSize.TrumpGothic34,
                _ => GameFontFamilyAndSize.TrumpGothic68,
            },
            _ => GameFontFamilyAndSize.Undefined,
        };
}
