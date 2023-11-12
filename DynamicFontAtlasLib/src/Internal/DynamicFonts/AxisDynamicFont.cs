using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Unicode;
using Dalamud.Interface.GameFonts;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal unsafe class AxisDynamicFont : DynamicFont {
    public AxisDynamicFont(
        DynamicFontAtlas atlas,
        GameFontStyle fontStyle,
        FdtReader fdt,
        IReadOnlyList<int> textureIndices)
        : base(atlas, null) {
        this.FontStyle = fontStyle;
        var fdtTexSize = new Vector4(
            fdt.FontHeader.TextureWidth,
            fdt.FontHeader.TextureHeight,
            fdt.FontHeader.TextureWidth,
            fdt.FontHeader.TextureHeight);

        this.EnsureIndex((char)Math.Min(char.MaxValue, fdt.Glyphs[^1].CharInt));
        this.Glyphs.EnsureCapacity(fdt.Glyphs.Count);
        foreach (var fdtg in fdt.Glyphs) {
            var codepoint = fdtg.CharInt;
            if (codepoint > char.MaxValue)
                break;

            if (codepoint < this.IndexLookup.Length && this.IndexLookup[codepoint] != ushort.MaxValue)
                continue;

            var glyph = new ImFontGlyphReal {
                AdvanceX = fdtg.AdvanceWidth,
                Codepoint = codepoint,
                Colored = false,
                TextureIndex = textureIndices[fdtg.TextureFileIndex],
                Visible = true,
                U0 = fdtg.TextureOffsetX,
                V0 = fdtg.TextureOffsetY,
                U1 = fdtg.BoundingWidth,
                V1 = fdtg.BoundingHeight,
            };

            glyph.XY1 = glyph.UV1;
            glyph.Y0 += fdtg.CurrentOffsetY;
            glyph.Y1 += fdtg.CurrentOffsetY;

            glyph.UV1 += glyph.UV0;
            glyph.UV /= fdtTexSize;
            glyph.U0 += 1 + fdtg.TextureChannelIndex;
            glyph.U1 += 1 + fdtg.TextureChannelIndex;

            this.LoadAttemptedGlyphs[codepoint] = true;
            this.IndexLookup[codepoint] = unchecked((ushort)this.Glyphs.Length);
            this.Glyphs.Add(glyph);
            this.Mark4KPageUsed(glyph);

            ref var indexedHotData = ref this.IndexedHotData[glyph.Codepoint];
            indexedHotData.AdvanceX = glyph.AdvanceX;
            indexedHotData.OccupiedWidth = Math.Max(glyph.AdvanceX, glyph.X1);
        }

        this.KerningPairs.EnsureCapacity(fdt.Distances.Count);
        foreach (var fdtk in fdt.Distances) {
            var l = fdtk.LeftInt;
            if (l > char.MaxValue)
                break;

            var r = fdtk.RightInt;
            if (r > char.MaxValue)
                continue;

            var d = fdtk.RightOffset;
            if (d == 0)
                continue;

            if (l < FrequentKerningPairsMaxCodepoint && r < FrequentKerningPairsMaxCodepoint)
                this.FrequentKerningPairs[(l * FrequentKerningPairsMaxCodepoint) + r] = d;

            this.KerningPairs.Add(
                new() {
                    AdvanceXAdjustment = d,
                    Left = unchecked((ushort)l),
                    Right = unchecked((ushort)r),
                });

            ref var rhd = ref this.IndexedHotData[r];
            var count = rhd.Count;
            if (count == 0)
                rhd.Offset = this.KerningPairs.Length - 1;

            Debug.Assert(count + 1 < 1 << 12, "Too many kerning entry");

            rhd.Count = ++count;

            // If linear search takes at least 32 iterations,
            // swap to bisect which should do the job in 5 iterations.
            if (count == 32)
                rhd.UseBisect = true;
        }

        this.Font.FontSize = MathF.Floor(fdt.FontHeader.Size * 4 / 3);
        this.Font.FallbackChar = this.FirstAvailableChar(
            (char)Constants.Fallback1Codepoint,
            (char)Constants.Fallback2Codepoint,
            ' ',
            '!',
            (char)this.Glyphs.First().Codepoint);

        this.Font.EllipsisChar = this.FirstAvailableChar('…', char.MaxValue);
        this.Font.DotChar = this.FirstAvailableChar('.', char.MaxValue);
        this.Font.DirtyLookupTables = 0;
        this.Font.Scale = 1f;
        this.Font.Ascent = fdt.FontHeader.Ascent;
        this.Font.Descent = fdt.FontHeader.Descent;
        this.UpdateReferencesToVectorItems();
    }

    /// <summary>
    /// Gets the game font style.
    /// </summary>
    public GameFontStyle FontStyle { get; }

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

    /// <inheritdoc/>
    public override bool IsFontIdent(in FontIdent ident) => ident.Game == this.FontStyle.Family;
}
