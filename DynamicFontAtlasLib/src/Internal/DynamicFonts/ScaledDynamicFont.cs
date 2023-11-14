using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Unicode;
using DynamicFontAtlasLib.FontIdentificationStructs;
using ImGuiNET;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal unsafe class ScaledDynamicFont : DynamicFont {
    public ScaledDynamicFont(
        DynamicFontAtlas atlas,
        DynamicFont? fallbackFont,
        DynamicFont src,
        float scale)
        : base(atlas, fallbackFont, (BitArray)src.LoadAttemptedGlyphs.Clone()) {
        this.BaseFont = src;
        this.IndexedHotData.Clear();
        this.FrequentKerningPairs.Clear();
        this.IndexLookup.Clear();
        this.Glyphs.Clear();
        this.KerningPairs.Clear();

        this.IndexedHotData.AddRange(src.IndexedHotData);
        this.FrequentKerningPairs.AddRange(src.FrequentKerningPairs);
        this.IndexLookup.AddRange(src.IndexLookup);
        this.Glyphs.AddRange(src.Glyphs);
        this.KerningPairs.AddRange(src.KerningPairs);
        this.Font.FontSize = MathF.Round(src.Font.FontSize * scale);
        this.Font.FallbackChar = src.Font.FallbackChar;
        this.Font.EllipsisChar = src.Font.EllipsisChar;
        this.Font.DotChar = src.Font.DotChar;
        this.Font.DirtyLookupTables = src.Font.DirtyLookupTables;
        this.Font.Scale = src.Font.Scale;
        this.Font.Ascent = src.Font.Ascent * scale;
        this.Font.Descent = src.Font.Descent * scale;
        this.Font.FallbackGlyph = (ImFontGlyph*)this.FindLoadedGlyphNoFallback(this.Font.FallbackChar);
        this.Font.FallbackHotData =
            this.Font.FallbackChar == ushort.MaxValue
                ? null
                : (ImFontGlyphHotData*)(this.IndexedHotData.Data + this.Font.FallbackChar);

        foreach (ref var glyph in this.Glyphs.AsSpan) {
            glyph.XY *= scale;
            glyph.AdvanceX = MathF.Round(glyph.AdvanceX * scale);
        }

        foreach (ref var hd in this.IndexedHotData.AsSpan) {
            hd.AdvanceX = MathF.Round(hd.AdvanceX * scale);
            hd.OccupiedWidth = MathF.Ceiling(hd.OccupiedWidth * scale);
        }

        foreach (ref var k in this.KerningPairs.AsSpan) {
            if (k is not { Left: < FrequentKerningPairsMaxCodepoint, Right: < FrequentKerningPairsMaxCodepoint })
                continue;

            ref var d = ref this.FrequentKerningPairs[(k.Left * FrequentKerningPairsMaxCodepoint) + k.Right];
            d = MathF.Round(d * scale);
        }

        this.UpdateReferencesToVectorItems();
    }

    /// <summary>
    /// Gets the base font.
    /// </summary>
    public DynamicFont BaseFont { get; }

    /// <inheritdoc/>
    public override bool IsCharAvailable(char c) => this.FindLoadedGlyphNoFallback(c) != null;

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<char> chars) {
        var changed = false;
        foreach (var c in chars)
            changed |= this.ApplyFallbackGlyph(c);

        if (changed)
            this.UpdateReferencesToVectorItems();
    }

    /// <inheritdoc/>
    public override void LoadGlyphs(IEnumerable<UnicodeRange> ranges) {
        var changed = false;
        foreach (var r in ranges) {
            foreach (var c in Enumerable.Range(r.FirstCodePoint, r.Length)) {
                if (c > ushort.MaxValue)
                    break;

                changed |= this.ApplyFallbackGlyph((char)c);
            }
        }

        if (changed)
            this.UpdateReferencesToVectorItems();
    }
}
