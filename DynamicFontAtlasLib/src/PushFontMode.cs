namespace DynamicFontAtlasLib;

/// <summary>
/// Specifies what to do when a font is not available for whatever reason.
/// </summary>
public enum PushFontMode {
    /// <summary>
    /// Take default action.
    /// </summary>
    Default,

    /// <summary>
    /// Do not push any font.
    /// </summary>
    Ignore,

    /// <summary>
    /// Push an empty font that occupies the intended height.
    /// </summary>
    HeightPlaceholder,

    /// <summary>
    /// Push the fallback font if specified and loaded.
    /// </summary>
    OptionalFallback,

    /// <summary>
    /// Push the fallback font if specified and loaded; otherwise, push an empty font that occupies the intended height.
    /// i.e. try <see cref="OptionalFallback"/> first, and if it does nothing, do <see cref="HeightPlaceholder"/>.
    /// </summary>
    OptionalHeightPlaceholderFallback,

    /// <summary>
    /// Push the fallback font if specified, waiting as necessary.
    /// </summary>
    RequiredFallback,

    /// <summary>
    /// Wait for the intended font to be loaded.
    /// </summary>
    Wait,
}
