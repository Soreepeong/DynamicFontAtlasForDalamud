using System;
using ImGuiNET;

namespace DynamicFontAtlasLib;

/// <summary>
/// Result for pushing fonts into current ImGui font stack.
/// </summary>
public struct PushFontResult : IDisposable {
    private bool shouldDispose;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushFontResult"/> struct.
    /// </summary>
    /// <param name="atlas">The atlas.</param>
    /// <param name="fontPtr">The font pointer.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="state">The state.</param>
    public PushFontResult(IDynamicFontAtlas atlas, ImFontPtr fontPtr, Exception? exception, PushFontResultState state) {
        this.Atlas = atlas;
        this.FontPtr = fontPtr;
        this.Exception = exception;
        this.State = state;
        if (!this.IsEmpty) {
            ImGui.PushFont(fontPtr);
            atlas.SuppressTextureUpdatesEnter();
            this.shouldDispose = true;
        }
    }

    /// <summary>
    /// Gets the associated atlas.
    /// </summary>
    public IDynamicFontAtlas Atlas { get; }

    /// <summary>
    /// Gets the font pointer. It may be empty, a placeholder font, or the real font.
    /// </summary>
    public ImFontPtr FontPtr { get; }

    /// <summary>
    /// Gets the exception thrown while loading the font, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the value indicating whether this struct contains nothing.
    /// </summary>
    public unsafe bool IsEmpty => this.FontPtr.NativePtr == null;

    /// <summary>
    /// Gets the state.
    /// </summary>
    public PushFontResultState State { get; }

    /// <summary>
    /// Gets the value indicating whether the font has failed to load.
    /// </summary>
    public bool IsFailed => this.Exception is not null;

    /// <summary>
    /// Gets the value indicating whether the font is being loaded.
    /// </summary>
    public bool IsLoading => this.Exception is null && this.State != PushFontResultState.Loaded;

    /// <inheritdoc/>
    public void Dispose() {
        if (this.shouldDispose) {
            ImGui.PopFont();
            this.Atlas.SuppressTextureUpdatesExit();
            this.shouldDispose = false;
        }
    }
}
