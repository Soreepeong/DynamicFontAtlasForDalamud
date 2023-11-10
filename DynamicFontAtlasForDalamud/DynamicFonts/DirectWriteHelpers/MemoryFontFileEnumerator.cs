using SharpDX;
using SharpDX.DirectWrite;

namespace DynamicFontAtlasLib.DynamicFonts.DirectWriteHelpers;

internal class MemoryFontFileEnumerator : CallbackBase, FontFileEnumerator {
    private readonly Factory factory;
    private readonly MemoryFontLoader loader;
    private int index = -1;

    public MemoryFontFileEnumerator(Factory factory, MemoryFontLoader loader) {
        this.factory = factory;
        this.loader = loader;
    }

    /// <inheritdoc/>
    public bool MoveNext() {
        if (this.index == this.loader.Memories.Length - 1)
            return false;

        this.index++;
        return true;
    }

    /// <inheritdoc/>
    public unsafe FontFile CurrentFontFile {
        get {
            var tmp = stackalloc ulong[3];
            tmp[0] = MemoryFontLoader.Signature;
            tmp[1] = this.loader.InstanceCounter;
            tmp[2] = (ulong)this.index;
            return new(this.factory, (nint)tmp, 24, this.loader);
        }
    }
}
