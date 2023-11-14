using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dalamud.Interface.GameFonts;
using SharpDX.DirectWrite;

namespace DynamicFontAtlasLib.FontIdentificationStructs;

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

    /// <summary>
    /// Gets the value indicating whether this <see cref="FontIdent"/> is empty.
    /// </summary>
    public bool IsEmpty => this is {
        BundledFont: BundledFonts.None,
        Game: GameFontFamily.Undefined,
        File: null,
        Memory: null,
        System: null,
    };

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(this.BundledFont, this.Game, this.File, this.Memory, this.System);

    /// <inheritdoc/>
    public override string ToString() => this switch {
        { BundledFont: not BundledFonts.None and var g } => $"{g}",
        { Game: not GameFontFamily.Undefined and var g } => $"{g}",
        { File: ({ } path, var index) } => $"{path}#{index}",
        { Memory: ({ } name, var index) } => $"\"{name}\"#{index}",
        { System: ({ } name, var variant) } => $"{name}({variant})",
        _ => "-",
    };

    public static IEnumerable<(
        (string Language, string Name)[] Names,
        FontIdent[] Variants)> GetSystemFonts(
        bool refreshSystem = false,
        Func<FontFamily, bool>? familyExcludeTest = null,
        Func<Font, bool>? fontExcludeTest = null,
        CancellationToken cancellationToken = default) {
        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(refreshSystem);

        var names = new List<(string Language, string Name)>();
        var variants = new List<FontIdent>();
        foreach (var familyIndex in Enumerable.Range(0, collection.FontFamilyCount)) {
            cancellationToken.ThrowIfCancellationRequested();

            using var family = collection.GetFontFamily(familyIndex);
            if (family.FontCount == 0 || familyExcludeTest?.Invoke(family) is true)
                continue;

            using var familyNames = family.FamilyNames;
            names.Clear();
            names.EnsureCapacity(familyNames.Count);
            names.AddRange(Enumerable.Range(0, familyNames.Count)
                .Select(x => (Language: familyNames.GetLocaleName(x), familyNames: familyNames.GetString(x))));

            if (!names.Any())
                continue;

            var englishName = names
                    .FirstOrDefault(x => x.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    .Name
                ?? names.First().Name;

            variants.Clear();
            variants.EnsureCapacity(family.FontCount);
            foreach (var fontIndex in Enumerable.Range(0, family.FontCount)) {
                cancellationToken.ThrowIfCancellationRequested();

                using var font = family.GetFont(fontIndex);
                if (fontExcludeTest?.Invoke(font) is true)
                    continue;

                variants.Add(FromSystem(englishName, font.Weight, font.Stretch, font.Style));
            }

            yield return (Names: names.ToArray(), Variants: variants.ToArray());
        }
    }
}
