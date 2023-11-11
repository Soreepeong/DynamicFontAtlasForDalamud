using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;
using ImGuiNET;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal abstract unsafe class DynamicFont : IDisposable {
    protected const int FrequentKerningPairsMaxCodepoint = 128;

    protected DynamicFont(DynamicFontAtlas atlas, BitArray? loadAttemptedGlyphs) {
        this.Atlas = atlas;
        this.FontNative = ImGuiNative.ImFont_ImFont();
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

    public ref ImFont Font => ref *this.FontNative;

    public ImFontPtr FontPtr => new(this.FontNative);

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

    public abstract bool IsFontIdent(in FontIdent ident);

    protected bool ApplyFallbackGlyph(char codepoint) {
        if (this.Atlas.FallbackFontIdent is not { } fi)
            return false;

        if (this.LoadAttemptedGlyphs[codepoint])
            return false;

        if (this.IsFontIdent(fi)) {
            this.LoadAttemptedGlyphs[codepoint] = true;
            return false;
        }

        var fallbackFont = this.Atlas.GetDynamicFont(fi, (int)MathF.Round(this.Font.FontSize));

        this.LoadAttemptedGlyphs[codepoint] = true;

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

    internal void FallbackFontChanged() {
        this.LoadAttemptedGlyphs.SetAll(false);
        foreach (ref var g in this.Glyphs.AsSpan)
            this.LoadAttemptedGlyphs[g.Codepoint] = true;
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
    protected void UpdateReferencesToVectorItems() {
        this.Font.FallbackGlyph = (ImFontGlyph*)this.FindLoadedGlyphNoFallback(this.Font.FallbackChar);
        this.Font.FallbackHotData =
            this.Font.FallbackChar == ushort.MaxValue
                ? null
                : (ImFontGlyphHotData*)(this.IndexedHotData.Data + this.Font.FallbackChar);

        var fallbackHotData = this.IndexedHotData[this.Font.FallbackChar];
        foreach (var codepoint in Enumerable.Range(0, this.IndexedHotData.Length)) {
            if (this.IndexLookup[codepoint] == ushort.MaxValue)
                this.IndexedHotData[codepoint] = fallbackHotData;
        }
    }

    protected void Mark4KPageUsed(in ImFontGlyphReal glyph) {
        // Mark 4K page as used
        var pageIndex = unchecked((ushort)(glyph.Codepoint / 4096));
        this.Font.Used4kPagesMap[pageIndex >> 3] |= unchecked((byte)(1 << (pageIndex & 7)));
    }
}
