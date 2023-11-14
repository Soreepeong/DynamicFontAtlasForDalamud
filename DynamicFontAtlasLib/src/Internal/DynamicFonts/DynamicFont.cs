using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;
using ImGuiNET;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal abstract unsafe class DynamicFont : IDisposable {
    protected const int FrequentKerningPairsMaxCodepoint = 128;

    private float lastFallbackHotDataAdvanceX = 0f;
    private float lastFallbackHotDataOccupiedWidth = 0f;
    private int lastFallbackHotDataLength = 0;

    protected DynamicFont(DynamicFontAtlas atlas, DynamicFont? fallbackFont, BitArray? loadAttemptedGlyphs) {
        this.Atlas = atlas;
        this.FallbackFont = fallbackFont;
        this.FontNative = ImGuiNative.ImFont_ImFont();
        this.FontNative->ContainerAtlas = atlas.AtlasPtr.NativePtr;
        this.IndexedHotData = new(&this.FontNative->IndexedHotData, null);
        this.FrequentKerningPairs = new(&this.FontNative->FrequentKerningPairs, null);
        this.IndexLookup = new(&this.FontNative->IndexLookup, null);
        this.Glyphs = new(&this.FontNative->Glyphs, null);
        this.KerningPairs = new(&this.FontNative->KerningPairs, null);
        this.LoadAttemptedGlyphs = loadAttemptedGlyphs ?? new(0x10000, false);

        this.FrequentKerningPairs.Resize(FrequentKerningPairsMaxCodepoint * FrequentKerningPairsMaxCodepoint);
    }

    ~DynamicFont() => this.Dispose(false);

    public bool IsDisposed { get; private set; }

    public DynamicFontAtlas Atlas { get; }

    public DynamicFont? FallbackFont { get; }

    public ref ImFont Font => ref *this.FontNative;

    public ImFontPtr FontPtr => new(this.FontNative);

    public nint FontIntPtr => (nint)this.FontNative;

    public ImVectorWrapper<float> FrequentKerningPairs { get; }

    public ImVectorWrapper<ImFontGlyphReal> Glyphs { get; }

    public ImVectorWrapper<ImFontGlyphHotDataReal> IndexedHotData { get; }

    public ImVectorWrapper<ushort> IndexLookup { get; }

    public ImVectorWrapper<ImFontKerningPair> KerningPairs { get; }

    public BitArray LoadAttemptedGlyphs { get; }

    protected ImFont* FontNative { get; }

    public abstract bool IsCharAvailable(char c);

    public abstract void LoadGlyphs(IEnumerable<char> chars);

    public abstract void LoadGlyphs(IEnumerable<UnicodeRange> ranges);

    public void LoadGlyphs(params char[] chars) => this.LoadGlyphs((IEnumerable<char>)chars);

    public void LoadGlyphs(params UnicodeRange[] ranges) => this.LoadGlyphs((IEnumerable<UnicodeRange>)ranges);

    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public char FirstAvailableChar(IEnumerable<char> chars) {
        var lastc = '\0';
        foreach (var c in chars) {
            lastc = c;
            if (this.IsCharAvailable(c))
                return c;
        }

        return lastc;
    }

    public char FirstAvailableChar(params char[] chars) => this.FirstAvailableChar(chars.AsEnumerable());

    public ImFontGlyphReal* FindLoadedGlyph(int c)
        => c is >= 0 and <= ushort.MaxValue
            ? (ImFontGlyphReal*)ImGuiNative.ImFont_FindGlyph(this.FontNative, (ushort)c)
            : null;

    public ImFontGlyphReal* FindLoadedGlyphNoFallback(int c)
        => c is >= 0 and <= ushort.MaxValue
            ? (ImFontGlyphReal*)ImGuiNative.ImFont_FindGlyphNoFallback(this.FontNative, (ushort)c)
            : null;

    public void EnsureIndex(int maxCodepoint) {
        maxCodepoint = Math.Max(ushort.MaxValue, maxCodepoint);
        var oldLength = this.IndexLookup.Length;
        if (oldLength >= maxCodepoint + 1)
            return;

        this.IndexedHotData.Resize(maxCodepoint + 1);
        this.IndexLookup.Resize(maxCodepoint + 1, ushort.MaxValue);
    }

    protected bool ApplyFallbackGlyph(char codepoint) {
        if (this.LoadAttemptedGlyphs[codepoint])
            return false;

        this.LoadAttemptedGlyphs[codepoint] = true;

        //var fallbackFont = this.Atlas.GetFontTask(fi, (int)MathF.Round(this.Font.FontSize));
        if (this.FallbackFont is not { } fallbackFont)
            return false;

        fallbackFont.LoadGlyphs(codepoint);
        var glyphPointer = fallbackFont.FindLoadedGlyphNoFallback(codepoint);
        if (glyphPointer == null)
            return false;

        var glyph = *glyphPointer;
        glyph.Y0 += this.Font.Ascent - fallbackFont.Font.Ascent;
        glyph.Y1 += this.Font.Ascent - fallbackFont.Font.Ascent;
        this.IndexLookup[codepoint] = unchecked((ushort)this.Glyphs.Length);
        this.Glyphs.Add(glyph);
        this.Mark4KPageUsed(glyph);

        ref var indexedHotData = ref this.IndexedHotData[glyph.Codepoint];
        indexedHotData.AdvanceX = glyph.AdvanceX;
        indexedHotData.OccupiedWidth = Math.Max(glyph.AdvanceX, glyph.X1);

        return true;
    }

    internal void SanityCheck() {
        _ = Marshal.ReadIntPtr((nint)this.FontNative->ContainerAtlas);
        _ = Marshal.ReadIntPtr((nint)this.FontNative->FallbackGlyph);
        var texIndex = ((ImFontGlyphReal*)this.FontNative->FallbackGlyph)->TextureIndex;
        var textures = new ImVectorWrapper<ImFontAtlasTexture>(&this.FontNative->ContainerAtlas->Textures, null);
        var texId = textures[texIndex].TexID;
        if (texId != 0)
            _ = Marshal.ReadIntPtr(texId);
    }

    protected void AllocateGlyphSpaces(int startIndex, int count) {
        foreach (ref var glyph in this.Glyphs.AsSpan.Slice(startIndex, count))
            this.Atlas.AllocateGlyphSpace(ref glyph);
    }

    protected virtual void Dispose(bool disposing) {
        if (this.IsDisposed)
            return;

        this.IsDisposed = true;
        ImGuiNative.ImFont_destroy(this.FontNative);
    }

    /// <summary>
    /// Updates references stored in ImFont.
    /// </summary>
    /// <remarks>
    /// Need to fix our custom ImGui, so that imgui_widgets.cpp:3656 stops thinking
    /// Codepoint &lt; FallbackHotData.size always means it's not fallback char.
    /// </remarks>
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    protected void UpdateReferencesToVectorItems() {
        this.Font.FallbackGlyph = (ImFontGlyph*)this.FindLoadedGlyphNoFallback(this.Font.FallbackChar);
        this.Font.FallbackHotData =
            this.Font.FallbackChar == ushort.MaxValue
                ? null
                : (ImFontGlyphHotData*)(this.IndexedHotData.Data + this.Font.FallbackChar);

        var fallbackHotData = this.IndexedHotData[this.Font.FallbackChar];
        if (fallbackHotData.AdvanceX != this.lastFallbackHotDataAdvanceX ||
            fallbackHotData.OccupiedWidth != this.lastFallbackHotDataOccupiedWidth) {
            this.lastFallbackHotDataLength = 0;
        }

        for (var codepoint = this.lastFallbackHotDataLength; codepoint < this.IndexedHotData.Length; codepoint++) {
            if (this.IndexLookup[codepoint] == ushort.MaxValue) {
                this.IndexedHotData[codepoint].AdvanceX = fallbackHotData.AdvanceX;
                this.IndexedHotData[codepoint].OccupiedWidth = fallbackHotData.OccupiedWidth;
            }
        }

        this.lastFallbackHotDataAdvanceX = fallbackHotData.AdvanceX;
        this.lastFallbackHotDataOccupiedWidth = fallbackHotData.OccupiedWidth;
        this.lastFallbackHotDataLength = this.IndexedHotData.Length;
    }

    protected void Mark4KPageUsed(in ImFontGlyphReal glyph) {
        // Mark 4K page as used
        var pageIndex = unchecked((ushort)(glyph.Codepoint / 4096));
        this.Font.Used4kPagesMap[pageIndex >> 3] |= unchecked((byte)(1 << (pageIndex & 7)));
    }

    protected void ReplaceKerningPairs(IEnumerable<ImFontKerningPair> sortedPairs) {
        this.KerningPairs.Clear();
        foreach (ref var ihd in this.IndexedHotData.AsSpan)
            ihd.KerningPairInfo = 0u;

        this.FrequentKerningPairs.AsSpan.Clear();
        foreach (var pair in sortedPairs) {
            if (pair is { Left: < FrequentKerningPairsMaxCodepoint, Right: < FrequentKerningPairsMaxCodepoint }) {
                this.FrequentKerningPairs[(pair.Left * FrequentKerningPairsMaxCodepoint) + pair.Right] =
                    pair.AdvanceXAdjustment;
            }

            if (this.KerningPairs.Any()
                && this.KerningPairs[^1].Left == pair.Left
                && this.KerningPairs[^1].Right == pair.Right) {
                this.KerningPairs[^1].AdvanceXAdjustment = pair.AdvanceXAdjustment;
                continue;
            }

            this.KerningPairs.Add(pair);

            this.EnsureIndex(pair.Right);
            ref var rhd = ref this.IndexedHotData[pair.Right];
            var count = rhd.Count;
            if (count == 0)
                rhd.Offset = this.KerningPairs.Length - 1;

            Debug.Assert(count < 1 << 12, "Too many kerning entry");
            rhd.Count = ++count;

            // If linear search takes at least 32 iterations,
            // swap to bisect which should do the job in 5 iterations.
            if (count == 32)
                rhd.UseBisect = true;
        }

        this.UpdateReferencesToVectorItems();
    }
}
