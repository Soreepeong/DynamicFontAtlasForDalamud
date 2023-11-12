namespace DynamicFontAtlasLib.TrueType;

#pragma warning disable CS0649
public struct LookupFlags {
    private byte Value;

    public bool RightToLeft {
        get => ((this.Value >> 0) & 1) != 0;
        set => this.Value = (byte)((this.Value & ~(1u << 0)) | (value ? 1u << 0 : 0u));
    }
    
    public bool IgnoreBaseGlyphs {
        get => ((this.Value >> 1) & 1) != 0;
        set => this.Value = (byte)((this.Value & ~(1u << 1)) | (value ? 1u << 1 : 0u));
    }
    
    public bool IgnoreLigatures {
        get => ((this.Value >> 2) & 1) != 0;
        set => this.Value = (byte)((this.Value & ~(1u << 2)) | (value ? 1u << 2 : 0u));
    }

    public bool IgnoreMarks {
        get => ((this.Value >> 3) & 1) != 0;
        set => this.Value = (byte)((this.Value & ~(1u << 3)) | (value ? 1u << 3 : 0u));
    }
    
    public bool UseMarkFilteringSet  {
        get => ((this.Value >> 4) & 1) != 0;
        set => this.Value = (byte)((this.Value & ~(1u << 4)) | (value ? 1u << 4 : 0u));
    }
}
