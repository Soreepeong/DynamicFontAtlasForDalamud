namespace DynamicFontAtlasLib.Utilities.ImGuiUtilities;

/// <summary>
/// ImFontGlyphHotData the correct version.
/// </summary>
internal struct ImFontGlyphHotDataReal {
    public float AdvanceX;
    public float OccupiedWidth;
    public uint KerningPairInfo;

    private const uint UseBisectMask /***/ = 0b_00000000_00000000_00000000_00000001u;
    private const uint OffsetMask /******/ = 0b_00000000_00001111_11111111_11111110u;
    private const uint CountMask /*******/ = 0b_11111111_11110000_00000111_11111100u;

    private const int UseBisectShift = 0;
    private const int OffsetShift = 1;
    private const int CountShift = 20;

    public bool UseBisect
    {
        get => (int)((this.KerningPairInfo & UseBisectMask) >> UseBisectShift) != 0;
        set => this.KerningPairInfo = (this.KerningPairInfo & ~UseBisectMask) | (value ? 1u << UseBisectShift : 0u);
    }

    public int Offset
    {
        get => (int)((this.KerningPairInfo & OffsetMask) >> OffsetShift);
        set => this.KerningPairInfo = (this.KerningPairInfo & ~OffsetMask) | ((uint)value << OffsetShift);
    }

    public int Count
    {
        get => (int)(this.KerningPairInfo & CountMask) >> CountShift;
        set => this.KerningPairInfo = (this.KerningPairInfo & ~CountMask) | ((uint)value << CountShift);
    }
}
