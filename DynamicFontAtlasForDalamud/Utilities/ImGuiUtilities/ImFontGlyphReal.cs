using System.Numerics;
using System.Runtime.InteropServices;

namespace DynamicFontAtlasLib.Utilities.ImGuiUtilities;

/// <summary>
/// ImFontGlyph the correct version.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 40)]
internal struct ImFontGlyphReal {
    [FieldOffset(0)]
    public uint ColoredVisibleTextureIndexCodepoint;

    [FieldOffset(4)]
    public float AdvanceX;

    [FieldOffset(8)]
    public float X0;

    [FieldOffset(12)]
    public float Y0;

    [FieldOffset(16)]
    public float X1;

    [FieldOffset(20)]
    public float Y1;

    [FieldOffset(24)]
    public float U0;

    [FieldOffset(28)]
    public float V0;

    [FieldOffset(32)]
    public float U1;

    [FieldOffset(36)]
    public float V1;

    [FieldOffset(8)]
    public Vector2 XY0;

    [FieldOffset(16)]
    public Vector2 XY1;

    [FieldOffset(24)]
    public Vector2 UV0;

    [FieldOffset(32)]
    public Vector2 UV1;

    [FieldOffset(8)]
    public Vector4 XY;

    [FieldOffset(24)]
    public Vector4 UV;

    private const uint ColoredMask /*****/ = 0b_00000000_00000000_00000000_00000001u;
    private const uint VisibleMask /*****/ = 0b_00000000_00000000_00000000_00000010u;
    private const uint TextureMask /*****/ = 0b_00000000_00000000_00000111_11111100u;
    private const uint CodepointMask /***/ = 0b_11111111_11111111_11111000_00000000u;

    private const int ColoredShift = 0;
    private const int VisibleShift = 1;
    private const int TextureShift = 2;
    private const int CodepointShift = 11;

    public bool Colored
    {
        get => (int)((this.ColoredVisibleTextureIndexCodepoint & ColoredMask) >> ColoredShift) != 0;
        set => this.ColoredVisibleTextureIndexCodepoint =
            (this.ColoredVisibleTextureIndexCodepoint & ~ColoredMask) | (value ? 1u << ColoredShift : 0u);
    }

    public bool Visible
    {
        get => (int)((this.ColoredVisibleTextureIndexCodepoint & VisibleMask) >> VisibleShift) != 0;
        set => this.ColoredVisibleTextureIndexCodepoint =
            (this.ColoredVisibleTextureIndexCodepoint & ~VisibleMask) | (value ? 1u << VisibleShift : 0u);
    }

    public int TextureIndex
    {
        get => (int)((this.ColoredVisibleTextureIndexCodepoint & TextureMask) >> TextureShift);
        set => this.ColoredVisibleTextureIndexCodepoint =
            (this.ColoredVisibleTextureIndexCodepoint & ~TextureMask) | ((uint)value << TextureShift);
    }

    public int Codepoint
    {
        get => (int)((this.ColoredVisibleTextureIndexCodepoint & CodepointMask) >> CodepointShift);
        set => this.ColoredVisibleTextureIndexCodepoint =
            (this.ColoredVisibleTextureIndexCodepoint & ~CodepointMask) | ((uint)value << CodepointShift);
    }
}
