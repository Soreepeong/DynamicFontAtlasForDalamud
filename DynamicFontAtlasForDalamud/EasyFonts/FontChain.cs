using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;

namespace DynamicFontAtlasLib.EasyFonts;

/// <summary>
/// Indicates a whole font chain.
/// </summary>
[Serializable]
public struct FontChain : IEquatable<FontChain> {
    /// <summary>
    /// Initializes a new instance of the <see cref="FontChain"/> struct.
    /// </summary>
    public FontChain() {
        this.SecondaryFontsNullable = ImmutableList<FontChainEntry>.Empty;
        this.LineHeight = 1f;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontChain"/> struct.
    /// </summary>
    /// <param name="fonts">Fonts to include.</param>
    public FontChain(IEnumerable<FontChainEntry> fonts) {
        using var en = fonts.GetEnumerator();
        if (!en.MoveNext())
            throw new ArgumentException(null, nameof(fonts));

        this.PrimaryFont = en.Current;
        if (en.MoveNext()) {
            var r = new List<FontChainEntry>();
            do {
                r.Add(en.Current);
            } while (en.MoveNext());

            this.SecondaryFontsNullable = r.ToImmutableList();
        } else {
            this.SecondaryFontsNullable = ImmutableList<FontChainEntry>.Empty;
        }

        this.LineHeight = 1f;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontChain"/> struct.
    /// </summary>
    /// <param name="font">Font to include.</param>
    public FontChain(FontChainEntry font) : this() {
        this.PrimaryFont = font;
        this.SecondaryFontsNullable = ImmutableList<FontChainEntry>.Empty;
    }

    /// <summary>
    /// Gets or sets the primary font.
    /// </summary>
    public FontChainEntry PrimaryFont { get; set; }

    /// <summary>
    /// Gets or sets the entries in the font chain.
    /// </summary>
    public ImmutableList<FontChainEntry>? SecondaryFontsNullable { get; set; }

    /// <summary>
    /// Gets the entries in the font chain, in a non-null manner.
    /// </summary>
    [JsonIgnore]
    public ImmutableList<FontChainEntry> SecondaryFonts =>
        this.SecondaryFontsNullable ?? ImmutableList<FontChainEntry>.Empty;

    /// <summary>
    /// Gets or sets the ratio of line height of the final font, relative to the first font of the chain.
    /// Error if not a positive number.
    /// </summary>
    public float LineHeight { get; set; }

    /// <summary>
    /// Gets or sets the forced height-to-width ratio of each glyph. Only applicable if a positive number is specified.
    /// Error if not a positive number nor a zero.
    /// </summary>
    public float GlyphRatio { get; set; }

    /// <summary>
    /// Gets or sets the vertical alignment of secondary entries in the font chain.
    /// </summary>
    public FontChainVerticalAlignment VerticalAlignment = FontChainVerticalAlignment.Baseline;

    public static bool operator ==(FontChain left, FontChain right) => left.Equals(right);

    public static bool operator !=(FontChain left, FontChain right) => !(left == right);

    /// <inheritdoc />
    public override bool Equals(object? other) => other is FontChain o && this.Equals(o);

    /// <inheritdoc cref="object.Equals(object?)" />
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "It's an Equals function")]
    public bool Equals(FontChain other) =>
        this.LineHeight == other.LineHeight
        && this.GlyphRatio == other.GlyphRatio
        && this.VerticalAlignment == other.VerticalAlignment
        && this.PrimaryFont == other.PrimaryFont
        && this.SecondaryFonts.Count == other.SecondaryFonts.Count
        && this.SecondaryFonts.Zip(other.SecondaryFonts).All(x => x.First == x.Second);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.SecondaryFonts.Aggregate(
            HashCode.Combine(this.LineHeight, this.GlyphRatio, this.VerticalAlignment, this.PrimaryFont),
            (p, e) => HashCode.Combine(p, e.GetHashCode()));
}
