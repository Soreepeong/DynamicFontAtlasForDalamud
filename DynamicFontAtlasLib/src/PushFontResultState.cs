namespace DynamicFontAtlasLib;

/// <summary>
/// State enum for <see cref="PushFontResult"/>.
/// </summary>
public enum PushFontResultState {
    /// <summary>
    /// The <see cref="PushFontResult"/> contains nothing.
    /// </summary>
    Empty,
    
    /// <summary>
    /// The <see cref="PushFontResult"/> contains fallback font.
    /// </summary>
    Fallback,
    
    /// <summary>
    /// The <see cref="PushFontResult"/> contains placeholder font.
    /// </summary>
    Placeholder,

    /// <summary>
    /// The <see cref="PushFontResult"/> contains the desired font.
    /// </summary>
    Loaded,
}
