using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Unicode;
using System.Threading.Tasks;
using DynamicFontAtlasLib.FontIdentificationStructs;
using ImGuiNET;

namespace DynamicFontAtlasLib;

/// <summary>
/// A wrapper for <see cref="ImFontAtlas"/> which supports adding glyphs without having to rebuild from scratch on every change.
/// </summary>
/// <remarks>Any function marked <b>NOT thread safe</b> cannot be run concurrently with itself or other functions.</remarks>
public interface IDynamicFontAtlas : IDisposable {
    /// <summary>
    /// Gets the wrapped <see cref="ImFontAtlasPtr"/>.
    /// </summary>
    ImFontAtlasPtr AtlasPtr { get; }

    /// <summary>
    /// Gets or sets the fallback font. Once set, until a call to <see cref="Clear"/>, the changes may not apply.
    /// </summary>
    FontChain FallbackFontChain { get; set; }

    /// <summary>
    /// Gets the dictionary containing <see cref="Task{T}"/> returning an array of <see cref="byte"/>s.
    /// The task can be created suspended.
    /// </summary>
    ConcurrentDictionary<string, Task<byte[]>> FontDataBytes { get; }
    
    /// <summary>
    /// Gets or sets the getter function that fetches the overall scale of all fonts contained in this atlas.
    /// </summary>
    Func<float> ScaleGetter { get; set; }

    /// <summary>
    /// Gets or sets the getter function that fetches the font gamma value.
    /// Once set, until a call to <see cref="Clear"/>, the changes may not apply.
    /// </summary>
    Func<float> GammaGetter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether changes detected from <see cref="GammaGetter"/> should clear the atlas.
    /// If false, until a call to <see cref="Clear"/>, the changes may not apply.
    /// </summary>
    bool GammaChangeShouldClear { get; set; }

    /// <summary>
    /// Clears all the loaded fonts from the atlas.
    /// </summary>
    /// <remarks>This function is <b>NOT thread safe</b>.</remarks>
    void Clear();

    /// <summary>
    /// Reset recorded font load errors, so that on next access, font will be attempted for load again.
    /// </summary>
    /// <remarks>This function is thread safe.</remarks>
    void ClearLoadErrorHistory();

    /// <summary>
    /// Gets the reason why <paramref name="ident"/> of size <paramref name="sizePx"/> failed to load.
    /// </summary>
    /// <param name="ident">The font identifier.</param>
    /// <param name="sizePx">The font size in pixels.</param>
    /// <returns>Exception, if any.</returns>
    /// <remarks>This function is thread safe.</remarks>
    Exception? GetLoadException(in FontIdent ident, float sizePx) =>
        this.GetLoadException(new(new FontChainEntry(ident, sizePx)));

    /// <summary>
    /// Gets the reason why <paramref name="chain"/> failed to load.
    /// </summary>
    /// <param name="chain">The chain to query.</param>
    /// <returns>Exception, if any.</returns>
    /// <remarks>This function is thread safe.</remarks>
    Exception? GetLoadException(in FontChain chain);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="chars">Chars.</param>
    void LoadGlyphs(params char[] chars) => this.LoadGlyphs(ImGui.GetFont(), chars);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="chars">Chars.</param>
    void LoadGlyphs(IEnumerable<char> chars) => this.LoadGlyphs(ImGui.GetFont(), chars);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into <paramref name="font"/>, if it is managed by this.
    /// </summary>
    /// <param name="font">Relevant font.</param>
    /// <param name="chars">Chars.</param>
    void LoadGlyphs(ImFontPtr font, IEnumerable<char> chars);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="ranges">Ranges.</param>
    void LoadGlyphs(params UnicodeRange[] ranges) => this.LoadGlyphs(ImGui.GetFont(), ranges);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="ranges">Ranges.</param>
    void LoadGlyphs(IEnumerable<UnicodeRange> ranges) => this.LoadGlyphs(ImGui.GetFont(), ranges);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into <paramref name="font"/>, if it is managed by this.
    /// </summary>
    /// <param name="font">Relevant font.</param>
    /// <param name="ranges">Ranges.</param>
    void LoadGlyphs(ImFontPtr font, IEnumerable<UnicodeRange> ranges);

    /// <summary>
    /// Suppress uploading updated texture onto GPU for the scope.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that will make it update the texture on dispose.</returns>
    /// <remarks>This function is <b>NOT thread safe</b>.</remarks>
    IDisposable? SuppressTextureUpdatesScoped();

    /// <summary>
    /// Fetch a font, and if it succeeds, push it onto the stack.
    /// </summary>
    /// <param name="ident">Font identifier.</param>
    /// <param name="sizePx">Font size in pixels.</param>
    /// <param name="waitForLoad">Wait for the font to be loaded.</param>
    /// <returns>An <see cref="IDisposable"/> that will make it pop the font on dispose if font is loaded; otherwise, null.</returns>
    /// <remarks>This function is <b>NOT thread safe</b>.</remarks>
    IDisposable? PushFontScoped(in FontIdent ident, float sizePx, bool waitForLoad = false) =>
        this.PushFontScoped(new(new FontChainEntry(ident, sizePx)), waitForLoad);

    /// <summary>
    /// Fetch a font, and if it succeeds, push it onto the stack.
    /// </summary>
    /// <param name="chain">Font chain.</param>
    /// <param name="waitForLoad">Wait for the font to be loaded.</param>
    /// <returns>An <see cref="IDisposable"/> that will make it pop the font on dispose if font is loaded; otherwise, null.</returns>
    /// <remarks>This function is <b>NOT thread safe</b>.</remarks>
    IDisposable? PushFontScoped(in FontChain chain, bool waitForLoad = false);

    /// <summary>
    /// Upload updated textures onto GPU, if not suppressed.
    /// </summary>
    /// <remarks>This function is thread safe.</remarks>
    void UpdateTextures(bool forceUpdate);
}
