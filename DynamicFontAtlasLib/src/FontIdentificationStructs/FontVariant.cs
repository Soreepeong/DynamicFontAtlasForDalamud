using Newtonsoft.Json;
using SharpDX.DirectWrite;

namespace DynamicFontAtlasLib.FontIdentificationStructs;

/// <summary>
/// Indicates a set of font variant information.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public record struct FontVariant {
    /// <summary>
    /// Initializes a new instance of the <see cref="FontVariant"/> class.
    /// </summary>
    public FontVariant() {
        this.Weight = FontWeight.Normal;
        this.Stretch = FontStretch.Normal;
        this.Style = FontStyle.Normal;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontVariant"/> class.
    /// </summary>
    /// <param name="weight">Weight of the font.</param>
    /// <param name="stretch">Stretch of the font.</param>
    /// <param name="style">Style of the font.</param>
    public FontVariant(
        FontWeight weight,
        FontStretch stretch = FontStretch.Normal,
        FontStyle style = FontStyle.Normal) {
        this.Weight = weight;
        this.Stretch = stretch;
        this.Style = style;
    }

    /// <summary>
    /// Gets or sets the weight of font.
    /// </summary>
    public FontWeight Weight { get; set; } = FontWeight.Normal;

    /// <summary>
    /// Gets or sets the stretch of font.
    /// </summary>
    public FontStretch Stretch { get; set; } = FontStretch.Normal;

    /// <summary>
    /// Gets or sets the style of font.
    /// </summary>
    public FontStyle Style { get; set; } = FontStyle.Normal;

    /// <inheritdoc/>
    public override string ToString() => $"{this.Weight}, {this.Stretch}, {this.Style}";
}
