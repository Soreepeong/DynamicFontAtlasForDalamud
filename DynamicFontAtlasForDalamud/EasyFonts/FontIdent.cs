using System;
using Dalamud.Interface.GameFonts;
using SharpDX.DirectWrite;

namespace DynamicFontAtlasLib.EasyFonts;

/// <summary>
/// Indicates a font family and variant, or a font path and inner font index inside a font file.
/// </summary>
/// <remarks>
/// If a font file and the inner index are specified, and the corresponding file exists,
/// font family name and variant information will not be used.
/// </remarks>
[Serializable]
public record struct FontIdent {
    /// <summary>
    /// Initializes a new instance of the <see cref="FontIdent"/> class.
    /// </summary>
    public FontIdent() { }

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a built-in font.
    /// </summary>
    /// <returns></returns>
    public static FontIdent From(BundledFonts bundledFont) =>
        bundledFont is BundledFonts.None || !Enum.IsDefined(bundledFont)
            ? throw new ArgumentOutOfRangeException(null, nameof(bundledFont))
            : new() { BundledFont = bundledFont };

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a game font.
    /// </summary>
    /// <param name="gameFontFamily">Game font family to use.</param>
    public static FontIdent From(GameFontFamily gameFontFamily) =>
        gameFontFamily is GameFontFamily.Undefined || !Enum.IsDefined(gameFontFamily)
            ? throw new ArgumentOutOfRangeException(null, nameof(gameFontFamily))
            : new() { Game = gameFontFamily };

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a system font.
    /// </summary>
    /// <param name="name">Name of the font family. A font may have multiple names, and it can be one of those.</param>
    public static FontIdent FromSystem(string name) => FromSystem(name, new());

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a system font.
    /// </summary>
    /// <param name="name">Name of the font family. A font may have multiple names, and it can be one of those.</param>
    /// <param name="variant">Variant of the font.</param>
    public static FontIdent FromSystem(string name, FontVariant variant) => new() { System = (name, variant) };

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a system font.
    /// </summary>
    /// <param name="name">Name of the font family. A font may have multiple names, and it can be one of those.</param>
    /// <param name="weight">Weight of the font.</param>
    /// <param name="stretch">Stretch of the font.</param>
    /// <param name="style">Style of the font.</param>
    public static FontIdent FromSystem(
        string name,
        FontWeight weight,
        FontStretch stretch = FontStretch.Normal,
        FontStyle style = FontStyle.Normal) => new() { System = (name, new(weight, stretch, style)) };

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a font from a file.
    /// </summary>
    /// <param name="path">Path of the font file.</param>
    /// <param name="index">Index of the font within.</param>
    public static FontIdent FromFile(string path, int index) => new() { File = (path, index) };

    /// <summary>
    /// Creates a new instance of the <see cref="FontIdent"/> class indicating a font from a named byte array.
    /// </summary>
    /// <param name="name">Name of the stream.</param>
    /// <param name="index">Index of the font within.</param>
    public static FontIdent FromNamedBytes(string name, int index) => new() { Memory = (name, index) };

    /// <summary>
    /// Gets or sets the bundled font.
    /// </summary>
    public BundledFonts BundledFont { get; set; } = BundledFonts.None;

    /// <summary>
    /// Gets or sets the game font.
    /// </summary>
    public GameFontFamily Game { get; set; } = GameFontFamily.Undefined;

    /// <summary>
    /// Gets or sets the path of the font file and the font index within.
    /// </summary>
    public (string Path, int Index)? File { get; set; }

    /// <summary>
    /// Gets or sets the path of the font file and the font index within.
    /// </summary>
    public (string Name, int Index)? Memory { get; set; }

    /// <summary>
    /// Gets or sets the name and variant of a font.
    /// </summary>
    public (string Name, FontVariant Variant)? System { get; set; }

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(this.BundledFont, this.Game, this.File, this.Memory, this.System);

    /// <inheritdoc/>
    public override string ToString() => this switch {
        { BundledFont: not BundledFonts.None and var g } => $"Built-in: {g}",
        { Game: not GameFontFamily.Undefined and var g } => $"Game: {g}",
        { File: ({ } path, var index) } => $"File: {path}#{index}",
        { Memory: ({ } name, var index) } => $"Memory: {name}#{index}",
        { System: ({ } name, var variant) } => $"System: {name}({variant})",
        _ => "-",
    };
}
