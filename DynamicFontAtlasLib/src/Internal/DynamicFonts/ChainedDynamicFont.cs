using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text.Unicode;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal sealed unsafe class ChainedDynamicFont : DynamicFont {
    private readonly float globalScale;

    public ChainedDynamicFont(
        DynamicFontAtlas atlas,
        DynamicFont? fallbackFont,
        in FontChain chain,
        IEnumerable<DynamicFont> subfonts,
        float globalScale)
        : base(atlas, fallbackFont, null) {
        this.globalScale = globalScale;
        this.Chain = chain;
        this.Subfonts = subfonts.ToImmutableList();

        this.Font.FontSize = MathF.Round(chain.PrimaryFont.SizePx * this.globalScale * chain.LineHeight);
        this.Font.FallbackChar = this.FirstAvailableChar(
            (char)Constants.Fallback1Codepoint,
            (char)Constants.Fallback2Codepoint,
            '?',
            ' ');

        this.Font.EllipsisChar = this.FirstAvailableChar('…', char.MaxValue);
        this.Font.DotChar = this.FirstAvailableChar('.', char.MaxValue);
        this.Font.DirtyLookupTables = 0;
        this.Font.Scale = 1f / globalScale;
        this.Font.Ascent = this.Subfonts[0].Font.Ascent
            + MathF.Ceiling((chain.PrimaryFont.SizePx * this.globalScale * (chain.LineHeight - 1f)) / 2);

        this.Font.Descent = this.Subfonts[0].Font.Descent
            + MathF.Floor((chain.PrimaryFont.SizePx * this.globalScale * (chain.LineHeight - 1f)) / 2);

        this.LoadGlyphs(
            this.ZipChainFont()
                .SelectMany(ef =>
                    ef.Second.Glyphs
                        .Where(x => ef.First.RangeContainsCharacter(x.Codepoint))
                        .Select(x => (char)x.Codepoint))
                .Append(' ')
                .Append((char)this.Font.FallbackChar)
                .Append((char)this.Font.EllipsisChar)
                .Append((char)this.Font.DotChar));

        var rawDistances = atlas.Cache.Get(chain,
            () => this.ZipChainFont()
                .Reverse()
                .SelectMany(x => x.Second.KerningPairs.Where(y =>
                    x.First.RangeContainsCharacter(y.Left) && x.First.RangeContainsCharacter(y.Right)))
                .OrderBy(x => x.Right)
                .ThenBy(x => x.Left)
                .ToArray());

        this.KerningPairs.EnsureCapacity(rawDistances.Length);
        this.ReplaceKerningPairs(rawDistances);
    }

    public FontChain Chain { get; set; }

    public IReadOnlyList<DynamicFont> Subfonts { get; set; }

    private IEnumerable<(FontChainEntry First, DynamicFont Second)> ZipChainFont() =>
        this.Chain.SecondaryFonts
            .Prepend(this.Chain.PrimaryFont)
            .Zip(this.Subfonts);

    /// <inheritdoc/>
    public override bool IsCharAvailable(char c) =>
        this.ZipChainFont()
            .Any(x => x.First.RangeContainsCharacter(c) && x.Second.IsCharAvailable(c));

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<char> chars) {
        if (chars is not ICollection<char> coll)
            coll = chars.ToArray();

        if (!coll.Any())
            return;

        this.EnsureIndex(coll.Max());

        var changed = false;
        foreach (var (entry, font) in this.ZipChainFont()) {
            font.LoadGlyphs(coll);
            foreach (var c in coll)
                changed |= this.EnsureCharacter(c, entry, font);
        }

        foreach (var c in coll) {
            if (!this.LoadAttemptedGlyphs[c])
                changed |= this.ApplyFallbackGlyph(c);
        }

        if (changed)
            this.UpdateReferencesToVectorItems();
    }

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<UnicodeRange> ranges) {
        if (ranges is not ICollection<UnicodeRange> coll)
            coll = ranges.ToArray();

        if (!coll.Any())
            return;

        this.EnsureIndex(coll.Max(x => x.FirstCodePoint + (x.Length - 1)));

        var changed = false;
        foreach (var (entry, font) in this.ZipChainFont()) {
            font.LoadGlyphs(coll);
            foreach (var c in coll) {
                foreach (var cc in Enumerable.Range(c.FirstCodePoint, c.Length))
                    changed |= this.EnsureCharacter((char)cc, entry, font);
            }
        }

        foreach (var c in coll) {
            foreach (var cc in Enumerable.Range(c.FirstCodePoint, c.Length)) {
                if (!this.LoadAttemptedGlyphs[cc])
                    changed |= this.ApplyFallbackGlyph((char)cc);
            }
        }

        if (changed)
            this.UpdateReferencesToVectorItems();
    }

    private bool EnsureCharacter(char c, in FontChainEntry entry, DynamicFont font) {
        if (this.LoadAttemptedGlyphs[c])
            return false;

        if (!entry.RangeContainsCharacter(c))
            return false;

        var sourceGlyph = font.FindLoadedGlyphNoFallback(c);
        if (sourceGlyph is null)
            return false;

        var glyphScale = 1f;
        if (this.Chain.GlyphRatio > 0) {
            var expectedWidth = this.Chain.PrimaryFont.SizePx * this.globalScale * this.Chain.GlyphRatio;
            if (expectedWidth < sourceGlyph->AdvanceX) {
                var adjustedFontSizePx = (int)MathF.Floor(entry.SizePx * expectedWidth / sourceGlyph->AdvanceX);
                var font2Task = this.Atlas.GetFontTask(entry.Ident, adjustedFontSizePx);
                font2Task.Wait();
                if (font2Task.IsCompletedSuccessfully) {
                    var font2 = font2Task.Result;
                    font2.LoadGlyphs(c);
                    var sourceGlyph2 = font2.FindLoadedGlyphNoFallback(c);
                    if (sourceGlyph2 != null) {
                        sourceGlyph = sourceGlyph2;
                        font = font2;
                    } else {
                        glyphScale = expectedWidth / sourceGlyph->AdvanceX;
                    }
                } else {
                    glyphScale = expectedWidth / sourceGlyph->AdvanceX;
                }
            }
        }

        var offsetVector2 = new Vector2(
            MathF.Round(entry.OffsetX),
            MathF.Round(
                entry.OffsetY
                + this.Chain.VerticalAlignment switch {
                    FontChainVerticalAlignment.Top => 0,
                    FontChainVerticalAlignment.Middle => (this.Subfonts[0].Font.FontSize - font.Font.FontSize) / 2,
                    FontChainVerticalAlignment.Baseline => this.Subfonts[0].Font.Ascent - font.Font.Ascent,
                    FontChainVerticalAlignment.Bottom => this.Subfonts[0].Font.FontSize - font.Font.FontSize,
                    _ => throw new InvalidOperationException(),
                }
                + (this.Chain.PrimaryFont.SizePx * this.globalScale * (this.Chain.LineHeight - 1f) / 2)));

        var xy0 = sourceGlyph->XY0;
        var xy1 = sourceGlyph->XY1;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (glyphScale != 1f) {
            xy0.X *= glyphScale;
            xy1.X *= glyphScale;
            var baseY = this.Chain.VerticalAlignment switch {
                FontChainVerticalAlignment.Top => 0f,
                FontChainVerticalAlignment.Middle => this.Subfonts[0].Font.FontSize / 2f,
                FontChainVerticalAlignment.Baseline => this.Subfonts[0].Font.Ascent,
                FontChainVerticalAlignment.Bottom => this.Subfonts[0].Font.FontSize,
                _ => throw new InvalidOperationException(),
            };

            xy0.Y = ((xy0.Y - baseY) * glyphScale) + baseY;
            xy1.Y = ((xy1.Y - baseY) * glyphScale) + baseY;
        }

        var glyph = new ImFontGlyphReal {
            AdvanceX = MathF.Round(sourceGlyph->AdvanceX * glyphScale) + entry.LetterSpacing,
            Codepoint = c,
            Colored = sourceGlyph->Colored,
            TextureIndex = sourceGlyph->TextureIndex,
            Visible = sourceGlyph->Visible,
            UV = sourceGlyph->UV,
            XY0 = xy0 + offsetVector2,
            XY1 = xy1 + offsetVector2,
        };

        if (this.Chain.GlyphRatio > 0) {
            var expectedWidth = this.Chain.PrimaryFont.SizePx * this.globalScale * this.Chain.GlyphRatio;
            var adjust = (int)((expectedWidth - sourceGlyph->AdvanceX) / 2);
            if (adjust > 0) {
                glyph.X0 += adjust;
                glyph.X1 += adjust;
            }

            glyph.AdvanceX = expectedWidth + entry.LetterSpacing;
        }

        this.IndexLookup[c] = unchecked((ushort)this.Glyphs.Length);
        this.Glyphs.Add(glyph);
        this.Mark4KPageUsed(glyph);
        this.LoadAttemptedGlyphs[c] = true;

        ref var indexedHotData = ref this.IndexedHotData[glyph.Codepoint];
        indexedHotData.AdvanceX = glyph.AdvanceX;
        indexedHotData.OccupiedWidth = Math.Max(glyph.AdvanceX, glyph.X1);
        return true;
    }
}
