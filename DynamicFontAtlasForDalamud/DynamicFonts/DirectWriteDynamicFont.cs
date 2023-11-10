using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DynamicFontAtlasLib.EasyFonts;
using DynamicFontAtlasLib.OnDemandFonts.DirectWriteHelpers;
using DynamicFontAtlasLib.Utilities;
using DynamicFontAtlasLib.Utilities.ImGuiUtilities;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using Factory = SharpDX.DirectWrite.Factory;
using Factory3 = SharpDX.DirectWrite.Factory3;
using TextAntialiasMode = SharpDX.DirectWrite.TextAntialiasMode;
using UnicodeRange = System.Text.Unicode.UnicodeRange;

namespace DynamicFontAtlasLib.OnDemandFonts;

internal class DirectWriteDynamicFont : DynamicFont {
    private const MeasuringMode MeasuringMode = SharpDX.Direct2D1.MeasuringMode.GdiNatural;
    private const GridFitMode GridFitMode = SharpDX.DirectWrite.GridFitMode.Enabled;

    private readonly DisposeStack disposeStack = new();
    private readonly Factory factory;
    private readonly Factory3? factory3;
    private readonly Font font;
    private readonly FontFace face;
    private readonly RenderingMode renderMode;
    private readonly float sizePt;
    private readonly float multiplier;

    private DirectWriteDynamicFont(
        FontIdent ident,
        DynamicFontAtlas atlas,
        Factory factory,
        Font font,
        float sizePx)
        : base(atlas, null) {
        try {
            this.factory = this.disposeStack.Add(factory.QueryInterface<Factory>());
            this.factory3 = this.disposeStack.Add(factory.QueryInterfaceOrNull<Factory3>());
            this.font = this.disposeStack.Add(font.QueryInterface<Font>());
            this.face = this.disposeStack.Add(new FontFace(this.font));
            using (var renderingParams = this.disposeStack.Add(new RenderingParams(this.factory)))
                this.renderMode = face.GetRecommendedRenderingMode(this.sizePt, 1, MeasuringMode, renderingParams);

            this.Ident = ident;
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
        } catch (Exception) {
            this.disposeStack.Dispose();
            throw;
        }
    }

    public FontIdent Ident { get; }

    public FontMetrics Metrics { get; }

    public static DirectWriteDynamicFont FromSystem(
        DynamicFontAtlas atlas,
        string name,
        FontVariant variant,
        float sizePx) {
        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        if (!collection.FindFamilyName(name, out var fontFamilyIndex))
            throw new FileNotFoundException("Corresponding font family not found");

        using var fontFamily = collection.GetFontFamily(fontFamilyIndex);
        using var font = fontFamily.GetFirstMatchingFont(variant.Weight, variant.Stretch, variant.Style);
        return new(FontIdent.FromSystem(name, variant), atlas, factory, font, sizePx);
    }

    public static DirectWriteDynamicFont FromFile(
        DynamicFontAtlas atlas,
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
                    return new(FontIdent.FromFile(path, fontIndex), atlas, factory, font, sizePx);
            }
        }

        throw new IndexOutOfRangeException();
    }

    public static DirectWriteDynamicFont FromMemory(
        DynamicFontAtlas atlas,
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
                if (fontFace.Index == fontIndex)
                    return new(customIdent ?? FontIdent.FromNamedBytes(name, fontIndex), atlas, factory, font, sizePx);
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

                if (this.factory3 is not null) {
                    this.factory3.CreateGlyphRunAnalysis(
                        glyphRun,
                        null,
                        renderMode,
                        MeasuringMode,
                        GridFitMode,
                        TextAntialiasMode.Grayscale,
                        0f,
                        0f,
                        out analyses[i]);
                } else {
                    // TODO: this doesn't work
                    analyses[i] = new(
                        this.factory,
                        glyphRun,
                        1f,
                        renderMode,
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
            foreach (var (i, c) in Enumerable.Range(0, coll.Count).Zip(coll)) {
                if (analyses[i] is not { } analysis)
                    continue;

                var bound = analysis.GetAlphaTextureBounds(TextureType.Aliased1x1);
                var area = (bound.Right - bound.Left) * (bound.Bottom - bound.Top);
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

            tmpBuffer = ArrayPool<byte>.Shared.Rent(maxArea * 4);
            var multTable = this.Atlas.GammaMappingTable;
            foreach (var analysis in analyses) {
                if (analysis is null)
                    continue;

                ref var glyph = ref this.Glyphs[baseGlyphIndex++];
                if (!glyph.Visible)
                    continue;

                var width = (int)(glyph.X1 - glyph.X0);
                var height = (int)(glyph.Y1 - glyph.Y0);

                analysis.CreateAlphaTexture(
                    TextureType.Aliased1x1,
                    new((int)glyph.X0, (int)glyph.Y0, (int)glyph.X1, (int)glyph.Y1),
                    tmpBuffer,
                    tmpBuffer.Length);

                glyph.Y0 += this.Font.Ascent;
                glyph.Y1 += this.Font.Ascent;

                var wrap = (DynamicFontAtlasTextureWrap)this.Atlas.TextureWraps[glyph.TextureIndex];
                var u0 = (int)MathF.Round((glyph.U0 % 1) * wrap.Width);
                var v0 = (int)MathF.Round((glyph.V0 % 1) * wrap.Height);
                var channel = (int)Math.Floor(glyph.U0) - 1;
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

                changedTextures[glyph.TextureIndex] = true;
            }
        } catch (Exception e) {
            Debugger.Break();
            Debugger.Break();
        } finally {
            ArrayPool<byte>.Shared.Return(tmpBuffer);
            analyses.DisposeItems();

            foreach (var i in Enumerable.Range(0, changedTextures.Length)) {
                if (changedTextures[i])
                    ((DynamicFontAtlasTextureWrap)this.Atlas.TextureWraps[i]).MarkChanged();
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
    public override bool IsFontIdent(in FontIdent ident) => this.Ident == ident;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if (this.IsDisposed)
            return;

        if (disposing)
            this.disposeStack.Dispose();

        base.Dispose(disposing);
    }
}
