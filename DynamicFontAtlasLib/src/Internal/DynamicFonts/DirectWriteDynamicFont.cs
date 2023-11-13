using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal.DynamicFonts.DirectWriteHelpers;
using DynamicFontAtlasLib.TrueType.Tables;
using DynamicFontAtlasLib.Internal.Utilities;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;
using ImGuiNET;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using Factory = SharpDX.DirectWrite.Factory;
using Factory2 = SharpDX.DirectWrite.Factory2;
using TextAntialiasMode = SharpDX.DirectWrite.TextAntialiasMode;
using UnicodeRange = System.Text.Unicode.UnicodeRange;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal class DirectWriteDynamicFont : DynamicFont {
    private const MeasuringMode MeasuringMode = SharpDX.Direct2D1.MeasuringMode.GdiNatural;
    private const GridFitMode GridFitMode = SharpDX.DirectWrite.GridFitMode.Enabled;

    private readonly DisposeStack disposeStack = new();
    private readonly Factory factory;
    private readonly Factory2? factory2;
    private readonly Font font;
    private readonly FontFace face;
    private readonly RenderingMode renderMode;
    private readonly float sizePt;
    private readonly float multiplier;

    private DirectWriteDynamicFont(
        FontIdent ident,
        DynamicFontAtlas atlas,
        DynamicFont? fallbackFont,
        Factory factory,
        Font font,
        float sizePx)
        : base(atlas, fallbackFont, null) {
        try {
            this.factory = this.disposeStack.Add(factory.QueryInterface<Factory>());
            this.factory2 = this.disposeStack.Add(factory.QueryInterfaceOrNull<Factory2>());
            this.font = this.disposeStack.Add(font.QueryInterface<Font>());
            this.face = this.disposeStack.Add(new FontFace(this.font));
            using (var renderingParams = this.disposeStack.Add(new RenderingParams(this.factory)))
                this.renderMode = this.face.GetRecommendedRenderingMode(this.sizePt, 1, MeasuringMode, renderingParams);

            this.Metrics = this.face.Metrics;
            this.sizePt = (sizePx * 3) / 4;
            this.multiplier = this.sizePt / this.Metrics.DesignUnitsPerEm;
            this.Font.FontSize = MathF.Round(sizePx);
            this.Font.FallbackChar = this.FirstAvailableChar(
                (char)Constants.Fallback1Codepoint,
                (char)Constants.Fallback2Codepoint,
                ' ');

            this.Font.EllipsisChar = this.FirstAvailableChar('…', char.MaxValue);
            this.Font.DotChar = this.FirstAvailableChar('.', char.MaxValue);
            this.Font.DirtyLookupTables = 0;
            this.Font.Scale = 1f;
            this.Font.Ascent = MathF.Ceiling(this.Metrics.Ascent * this.multiplier);
            this.Font.Descent = MathF.Ceiling(this.Metrics.Descent * this.multiplier);
            this.LoadGlyphs(' ', (char)this.Font.FallbackChar, (char)this.Font.EllipsisChar, (char)this.Font.DotChar);

            var rawDistances = atlas.Cache.Get(ident, () => ExtractRawKerningDistances(this.face));
            this.KerningPairs.EnsureCapacity(rawDistances.Length);
            this.ReplaceKerningPairs(rawDistances
                .Select(x => new ImFontKerningPair {
                    Left = x.Left,
                    Right = x.Right,
                    AdvanceXAdjustment = MathF.Round(x.Value * this.multiplier),
                }).ToArray());
        } catch (Exception) {
            this.disposeStack.Dispose();
            throw;
        }
    }

    public FontMetrics Metrics { get; }

    public static DirectWriteDynamicFont FromSystem(
        DynamicFontAtlas atlas,
        DynamicFont? fallbackFont,
        string name,
        FontVariant variant,
        float sizePx) {
        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        if (!collection.FindFamilyName(name, out var fontFamilyIndex))
            throw new FileNotFoundException("Corresponding font family not found");

        using var fontFamily = collection.GetFontFamily(fontFamilyIndex);
        using var font = fontFamily.GetFirstMatchingFont(variant.Weight, variant.Stretch, variant.Style);
        return new(FontIdent.FromSystem(name, variant), atlas, fallbackFont, factory, font, sizePx);
    }

    public static DirectWriteDynamicFont FromFile(
        DynamicFontAtlas atlas,
        DynamicFont? fallbackFont,
        string path,
        int fontIndex,
        float sizePx) {
        using var factory = new Factory();
        using var loader = new MemoryFontLoader(factory, File.ReadAllBytes(path));
        using var col = new FontCollection(factory, loader, loader.Key);
        foreach (var i in Enumerable.Range(0, col.FontFamilyCount)) {
            using var family = col.GetFontFamily(i);
            foreach (var j in Enumerable.Range(0, family.FontCount)) {
                using var font = family.GetFont(j);
                using var fontFace = new FontFace(font);
                if (fontFace.Index == fontIndex)
                    return new(FontIdent.FromFile(path, fontIndex), atlas, fallbackFont, factory, font, sizePx);
            }
        }

        throw new IndexOutOfRangeException();
    }

    public static DirectWriteDynamicFont FromMemory(
        DynamicFontAtlas atlas,
        DynamicFont? fallbackFont,
        string name,
        Memory<byte> streamOpener,
        int fontIndex,
        float sizePx,
        FontIdent? customIdent) {
        using var factory = new Factory();
        using var loader = new MemoryFontLoader(factory, streamOpener);
        using var col = new FontCollection(factory, loader, loader.Key);
        foreach (var i in Enumerable.Range(0, col.FontFamilyCount)) {
            using var family = col.GetFontFamily(i);
            foreach (var j in Enumerable.Range(0, family.FontCount)) {
                using var font = family.GetFont(j);
                using var fontFace = new FontFace(font);
                if (fontFace.Index == fontIndex) {
                    return new(
                        customIdent ?? FontIdent.FromNamedBytes(name, fontIndex),
                        atlas,
                        fallbackFont,
                        factory,
                        font,
                        sizePx);
                }
            }
        }

        throw new IndexOutOfRangeException();
    }

    /// <inheritdoc/>
    public override bool IsCharAvailable(char c) => this.font.HasCharacter(c);

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<char> chars) {
        if (chars is not ICollection<char> coll)
            coll = chars.ToArray();

        if (!coll.Any())
            return;

        this.EnsureIndex(coll.Max());

        var analyses = new GlyphRunAnalysis?[coll.Count];
        var glyphMetrics = new GlyphMetrics[coll.Count];
        var advances = new float[coll.Count];
        var offset = new GlyphOffset[coll.Count];
        var tmpCodepoints = new int[1];
        var tmpAdvances = new float[1];
        var tmpOffsets = new GlyphOffset[1];
        var tmpBuffer = Array.Empty<byte>();
        var glyphTextureTypes = Array.Empty<TextureType>();
        var changed = false;
        Span<bool> changedTextures = new bool[256];
        try {
            foreach (var (i, c) in Enumerable.Range(0, coll.Count).Zip(coll)) {
                if (this.LoadAttemptedGlyphs[c])
                    continue;

                tmpCodepoints[0] = c;
                var glyphIndices = this.face.GetGlyphIndices(tmpCodepoints);
                if (glyphIndices[0] == 0) {
                    changed |= this.ApplyFallbackGlyph(c);
                    continue;
                }

                this.LoadAttemptedGlyphs[c] = true;

                glyphMetrics[i] = this.face.GetGdiCompatibleGlyphMetrics(
                    this.sizePt,
                    1f,
                    null,
                    true,
                    glyphIndices,
                    false)[0];

                var glyphRun = new GlyphRun {
                    FontFace = this.face,
                    FontSize = this.sizePt,
                    Indices = glyphIndices,
                    Advances = tmpAdvances,
                    Offsets = tmpOffsets,
                };

                if (this.factory2 is not null) {
                    this.factory2.CreateGlyphRunAnalysis(
                        glyphRun,
                        null,
                        this.renderMode,
                        MeasuringMode,
                        GridFitMode,
                        TextAntialiasMode.Grayscale,
                        0f,
                        0f,
                        out analyses[i]);
                } else {
                    analyses[i] = new(
                        this.factory,
                        glyphRun,
                        1f,
                        null,
                        this.renderMode,
                        MeasuringMode,
                        0f,
                        0f);
                }

                advances[i] = tmpAdvances[0];
                offset[i] = tmpOffsets[0];
            }

            var baseGlyphIndex = this.Glyphs.Length;
            var validCount = analyses.Count(x => x is not null);
            changed |= this.Glyphs.EnsureCapacity(this.Glyphs.Length + validCount);

            var maxArea = 0;
            glyphTextureTypes = ArrayPool<TextureType>.Shared.Rent(coll.Count);
            foreach (var (i, c) in Enumerable.Range(0, coll.Count).Zip(coll)) {
                if (analyses[i] is not { } analysis)
                    continue;

                glyphTextureTypes[i] = TextureType.Aliased1x1;
                var bound = analysis.GetAlphaTextureBounds(TextureType.Aliased1x1);
                var area = (bound.Right - bound.Left) * (bound.Bottom - bound.Top);
                if (area == 0) {
                    glyphTextureTypes[i] = TextureType.Cleartype3x1;
                    bound = analysis.GetAlphaTextureBounds(TextureType.Cleartype3x1);
                    area = (bound.Right - bound.Left) * (bound.Bottom - bound.Top);
                }

                maxArea = Math.Max(maxArea, area);

                ref var metrics = ref glyphMetrics[i];
                var glyph = new ImFontGlyphReal {
                    AdvanceX = MathF.Round(metrics.AdvanceWidth * this.multiplier),
                    Codepoint = c,
                    Colored = false,
                    Visible = area != 0,
                    X0 = bound.Left,
                    Y0 = bound.Top,
                    X1 = bound.Right,
                    Y1 = bound.Bottom,
                };

                this.IndexLookup[c] = unchecked((ushort)this.Glyphs.Length);
                this.Glyphs.Add(glyph);
                this.Mark4KPageUsed(glyph);
                changed = true;

                ref var indexedHotData = ref this.IndexedHotData[glyph.Codepoint];
                indexedHotData.AdvanceX = glyph.AdvanceX;
                indexedHotData.OccupiedWidth = Math.Max(glyph.AdvanceX, glyph.X1);
            }

            this.AllocateGlyphSpaces(baseGlyphIndex, validCount);

            tmpBuffer = ArrayPool<byte>.Shared.Rent(maxArea * 3);
            var multTable = this.Atlas.GammaMappingTable;
            foreach (var i in Enumerable.Range(0, analyses.Length)) {
                var analysis = analyses[i];
                if (analysis is null)
                    continue;

                ref var glyph = ref this.Glyphs[baseGlyphIndex++];
                if (!glyph.Visible)
                    continue;

                var width = (int)(glyph.X1 - glyph.X0);
                var height = (int)(glyph.Y1 - glyph.Y0);

                analysis.CreateAlphaTexture(
                    glyphTextureTypes[i],
                    new((int)glyph.X0, (int)glyph.Y0, (int)glyph.X1, (int)glyph.Y1),
                    tmpBuffer,
                    tmpBuffer.Length);

                glyph.Y0 += this.Font.Ascent;
                glyph.Y1 += this.Font.Ascent;

                var wrap = (RectpackingTextureWrap)this.Atlas.TextureWraps[glyph.TextureIndex];
                var u0 = (int)MathF.Round((glyph.U0 % 1) * wrap.Width);
                var v0 = (int)MathF.Round((glyph.V0 % 1) * wrap.Height);
                var channel = (int)Math.Floor(glyph.U0) - 1;
                if (glyphTextureTypes[i] == TextureType.Cleartype3x1) {
                    var widthBy3 = width * 3;
                    if (channel == -1) {
                        for (var y = 0; y < height; y++) {
                            var src = tmpBuffer.AsSpan(y * widthBy3, widthBy3);
                            var dst = wrap.Data.AsSpan((((v0 + y) * wrap.Width) + u0) * 4, width * 4);
                            for (int rx = 0, wx = 0; rx < widthBy3; rx += 3, wx += 4) {
                                dst[wx + 0] = 0xFF;
                                dst[wx + 1] = 0xFF;
                                dst[wx + 2] = 0xFF;
                                dst[wx + 3] = (byte)
                                    ((multTable[src[rx]] + multTable[src[rx + 1]] + multTable[src[rx + 2]]) / 3);
                            }
                        }
                    } else {
                        channel = channel == 3 ? 3 : 2 - channel;
                        for (var y = 0; y < height; y++) {
                            var src = tmpBuffer.AsSpan(y * widthBy3, widthBy3);
                            var dst = wrap.Data.AsSpan((((v0 + y) * wrap.Width) + u0) * 4, width * 4);
                            for (int rx = 0, wx = channel; rx < widthBy3; rx += 3, wx += 4) {
                                dst[wx] = (byte)
                                    ((multTable[src[rx]] + multTable[src[rx + 1]] + multTable[src[rx + 2]]) / 3);
                            }
                        }
                    }
                } else {
                    if (channel == -1) {
                        for (var y = 0; y < height; y++) {
                            var src = tmpBuffer.AsSpan(y * width, width);
                            var dst = wrap.Data.AsSpan((((v0 + y) * wrap.Width) + u0) * 4, width * 4);
                            for (int rx = 0, wx = 0; rx < width; rx++, wx += 4) {
                                dst[wx + 0] = 0xFF;
                                dst[wx + 1] = 0xFF;
                                dst[wx + 2] = 0xFF;
                                dst[wx + 3] = multTable[src[rx]];
                            }
                        }
                    } else {
                        channel = channel == 3 ? 3 : 2 - channel;
                        for (var y = 0; y < height; y++) {
                            var src = tmpBuffer.AsSpan(y * width, width);
                            var dst = wrap.Data.AsSpan((((v0 + y) * wrap.Width) + u0) * 4, width * 4);
                            for (int rx = 0, wx = channel; rx < width; rx++, wx += 4)
                                dst[wx] = multTable[src[rx]];
                        }
                    }
                }

                changedTextures[glyph.TextureIndex] = true;
            }
        } finally {
            ArrayPool<byte>.Shared.Return(tmpBuffer);
            ArrayPool<TextureType>.Shared.Return(glyphTextureTypes);
            analyses.DisposeItems();

            foreach (var i in Enumerable.Range(0, changedTextures.Length)) {
                if (changedTextures[i])
                    ((RectpackingTextureWrap)this.Atlas.TextureWraps[i]).MarkChanged();
            }

            if (changed)
                this.UpdateReferencesToVectorItems();
        }
    }

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<UnicodeRange> ranges) =>
        this.LoadGlyphs(
            ranges.SelectMany(x => Enumerable.Range(x.FirstCodePoint, x.Length))
                .Where(x => x <= char.MaxValue)
                .Select(x => (char)x));

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if (this.IsDisposed)
            return;

        if (disposing)
            this.disposeStack.Dispose();

        base.Dispose(disposing);
    }

    private static (char Left, char Right, short Value)[] ExtractRawKerningDistances(FontFace face) {
        using var disposeStack = new DisposeStack();
        if (!face.TryGetFontTable((int)Cmap.DirectoryTableTag.NativeValue,
                out var cmapTable,
                out var cmapContext)) {
            return Array.Empty<(char, char, short)>();
        }
        
        disposeStack.Add(() => face.ReleaseFontTable(cmapContext));
        var cmap = new Cmap(cmapTable.ToPointerSpan());
        if (cmap.UnicodeTable is not { } unicodeTable)
            return Array.Empty<(char, char, short)>();

        var glyphToCodepoints = unicodeTable
            .GroupBy(x => x.Value, x => x.Key)
            .OrderBy(x => x.Key)
            .ToDictionary(
                x => x.Key,
                x => x.Where(y => y <= ushort.MaxValue)
                    .Select(y => (char)y)
                    .ToImmutableSortedSet());

        if (face.TryGetFontTable((int)Kern.DirectoryTableTag.NativeValue,
                out var kernTable,
                out var kernContext)) {
            disposeStack.Add(() => face.ReleaseFontTable(kernContext));
            var kern = new Kern(kernTable.ToPointerSpan());
            return kern.EnumerateHorizontalPairs()
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Left, ImmutableSortedSet<char>.Empty)
                    .Select(lc => (Left: lc, x.Right, x.Value)))
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Right, ImmutableSortedSet<char>.Empty)
                    .Select(rc => (x.Left, Right: rc, x.Value)))
                .OrderBy(x => x.Right)
                .ThenBy(x => x.Left)
                .ToArray();
        }

        if (face.TryGetFontTable((int)Gpos.DirectoryTableTag.NativeValue,
                out var gposTable,
                out var gposContext)) {
            disposeStack.Add(() => face.ReleaseFontTable(gposContext));
            var gpos = new Gpos(gposTable.ToPointerSpan());
            return gpos.ExtractAdvanceX()
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Left, ImmutableSortedSet<char>.Empty)
                    .Select(lc => (Left: lc, x.Right, x.Value)))
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Right, ImmutableSortedSet<char>.Empty)
                    .Select(rc => (x.Left, Right: rc, x.Value)))
                .OrderBy(x => x.Right)
                .ThenBy(x => x.Left)
                .ToArray();
        }

        return Array.Empty<(char, char, short)>();
    }
}
