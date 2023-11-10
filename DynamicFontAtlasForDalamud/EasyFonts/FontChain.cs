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
        this.FontsNullable = ImmutableList<FontChainEntry>.Empty;
        this.LineHeight = 1f;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontChain"/> struct.
    /// </summary>
    /// <param name="fonts">Fonts to include.</param>
    public FontChain(IEnumerable<FontChainEntry> fonts) {
        this.FontsNullable = fonts.ToImmutableList();
        this.LineHeight = 1f;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontChain"/> struct.
    /// </summary>
    /// <param name="font">Font to include.</param>
    public FontChain(FontChainEntry font) : this(new[] { font }) { }

    /// <summary>
    /// Gets or sets the entries in the font chain.
    /// </summary>
    public ImmutableList<FontChainEntry>? FontsNullable { get; set; }

    /// <summary>
    /// Gets the entries in the font chain, in a non-null manner.
    /// </summary>
    [JsonIgnore]
    public ImmutableList<FontChainEntry> Fonts => this.FontsNullable ?? ImmutableList<FontChainEntry>.Empty;

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
        this.Fonts.Count == other.Fonts.Count
        && this.LineHeight == other.LineHeight
        && this.GlyphRatio == other.GlyphRatio
        && this.VerticalAlignment == other.VerticalAlignment
        && this.Fonts.Zip(other.Fonts).All(x => x.First == x.Second);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.Fonts.Aggregate(
            HashCode.Combine(this.LineHeight, this.GlyphRatio, this.VerticalAlignment),
            (p, e) => HashCode.Combine(p, e.GetHashCode()));
}
