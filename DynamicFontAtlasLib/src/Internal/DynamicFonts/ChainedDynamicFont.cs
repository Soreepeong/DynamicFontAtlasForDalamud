using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text.Unicode;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal unsafe class ChainedDynamicFont : DynamicFont {
    private readonly float globalScale;

    public ChainedDynamicFont(
        DynamicFontAtlas atlas,
        in FontChain chain,
        IEnumerable<DynamicFont> subfonts,
        float globalScale)
        : base(atlas, null) {
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

        this.LoadGlyphs(' ', (char)this.Font.FallbackChar, (char)this.Font.EllipsisChar, (char)this.Font.DotChar);
    }

    public FontChain Chain { get; set; }

    public IReadOnlyList<DynamicFont> Subfonts { get; set; }

    /// <inheritdoc/>
    public override bool IsCharAvailable(char c) =>
        this.Chain.SecondaryFonts
            .Prepend(this.Chain.PrimaryFont)
            .Zip(this.Subfonts)
            .Any(x => x.First.RangeContainsCharacter(c) && x.Second.IsCharAvailable(c));

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<char> chars) {
        if (chars is not ICollection<char> coll)
            coll = chars.ToArray();

        if (!coll.Any())
            return;

        this.EnsureIndex(coll.Max());

        var changed = false;
        foreach (var (entry, font) in this.Chain.SecondaryFonts.Prepend(this.Chain.PrimaryFont).Zip(this.Subfonts)) {
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
        foreach (var (entry, font) in this.Chain.SecondaryFonts.Prepend(this.Chain.PrimaryFont).Zip(this.Subfonts)) {
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

    /// <inheritdoc/>
    public override bool IsFontIdent(in FontIdent ident) => false;

    private bool EnsureCharacter(char c, in FontChainEntry entry, DynamicFont font) {
        if (this.LoadAttemptedGlyphs[c])
            return false;

        if (!entry.RangeContainsCharacter(c))
            return false;

        var sourceGlyph = font.FindLoadedGlyphNoFallback(c);
        if (sourceGlyph is null)
            return false;

        if (this.Chain.GlyphRatio > 0) {
            var expectedWidth = this.Chain.PrimaryFont.SizePx * this.globalScale * this.Chain.GlyphRatio;
            if (expectedWidth < sourceGlyph->AdvanceX) {
                var adjustedFontSizePx = (int)MathF.Floor(entry.SizePx * expectedWidth / sourceGlyph->AdvanceX);
                var font2 = this.Atlas.GetDynamicFont(entry.Ident, adjustedFontSizePx);
                font2.LoadGlyphs(c);
                var sourceGlyph2 = font2.FindLoadedGlyphNoFallback(c);
                if (sourceGlyph2 != null) {
                    sourceGlyph = sourceGlyph2;
                    font = font2;
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
                    _ => throw new ArgumentOutOfRangeException()
                }
                + (this.Chain.PrimaryFont.SizePx * this.globalScale * (this.Chain.LineHeight - 1f) / 2)));

        var glyph = new ImFontGlyphReal {
            AdvanceX = sourceGlyph->AdvanceX + entry.LetterSpacing,
            Codepoint = c,
            Colored = sourceGlyph->Colored,
            TextureIndex = sourceGlyph->TextureIndex,
            Visible = sourceGlyph->Visible,
            UV = sourceGlyph->UV,
            XY0 = sourceGlyph->XY0 + offsetVector2,
            XY1 = sourceGlyph->XY1 + offsetVector2,
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
