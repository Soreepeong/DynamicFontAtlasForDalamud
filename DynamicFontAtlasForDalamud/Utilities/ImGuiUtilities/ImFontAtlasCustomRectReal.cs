using System.Numerics;
using ImGuiNET;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace DynamicFontAtlasLib.Utilities.ImGuiUtilities;

/// <summary>
/// ImFontAtlasCustomRect the correct version.
/// </summary>
internal unsafe struct ImFontAtlasCustomRectReal {
    public ushort Width;
    public ushort Height;
    public ushort X;
    public ushort Y;
    public uint TextureIndexAndGlyphId;
    public float GlyphAdvanceX;
    public Vector2 GlyphOffset;
    public ImFont* Font;

    private const uint TextureIndexMask /***/ = 0b_00000000_00000000_00000111_11111100u;
    private const uint GlyphIdMask /********/ = 0b_11111111_11111111_11111000_00000000u;

    private const int TextureIndexShift = 2;
    private const int GlyphIdShift = 11;

    public int TextureIndex {
        get => (int)(this.TextureIndexAndGlyphId & TextureIndexMask) >> TextureIndexShift;
        set => this.TextureIndexAndGlyphId =
            (this.TextureIndexAndGlyphId & ~TextureIndexMask) | ((uint)value << TextureIndexShift);
    }

    public int GlyphId {
        get => (int)(this.TextureIndexAndGlyphId & GlyphIdMask) >> GlyphIdShift;
        set => this.TextureIndexAndGlyphId =
            (this.TextureIndexAndGlyphId & ~GlyphIdMask) | ((uint)value << GlyphIdShift);
    }
}
