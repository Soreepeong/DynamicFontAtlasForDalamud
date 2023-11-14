using System;
using System.Collections.Generic;
using System.Text.Unicode;

namespace DynamicFontAtlasLib.Internal.DynamicFonts;

internal class HeightPlaceholderDynamicFont : DynamicFont {
    public HeightPlaceholderDynamicFont(DynamicFontAtlas atlas, int sizePx)
        : base(atlas, null, new(0x10000, true)) {
        this.Font.FontSize = sizePx;
        this.Font.FallbackChar = ' ';
        this.Font.EllipsisChar = ' ';
        this.Font.DotChar = '.';
        this.Font.Scale = 1f;
        this.Font.Ascent = MathF.Round(sizePx * 3f / 4);
        this.Font.Descent = sizePx - this.Font.Ascent;
        this.Glyphs.Add(new() { Codepoint = ' ' });
        this.Mark4KPageUsed(this.Glyphs[0]);
        this.EnsureIndex(' ');
        this.IndexLookup[' '] = 0;
        this.UpdateReferencesToVectorItems();
    }

    public override bool IsCharAvailable(char c) => c == ' ';

    public override void LoadGlyphs(IEnumerable<char> chars) { }

    public override void LoadGlyphs(IEnumerable<UnicodeRange> ranges) { }
}
