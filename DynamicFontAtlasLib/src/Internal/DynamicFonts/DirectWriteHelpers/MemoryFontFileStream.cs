using System;
using System.Buffers;
using SharpDX;
using SharpDX.DirectWrite;

namespace DynamicFontAtlasLib.Internal.DynamicFonts.DirectWriteHelpers;

internal class MemoryFontFileStream : CallbackBase, FontFileStream {
    private readonly Memory<byte> memory;
    private MemoryHandle pin;

    public MemoryFontFileStream(Memory<byte> memory) {
        this.memory = memory;
        this.pin = memory.Pin();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        this.pin.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public unsafe void ReadFileFragment(
        out nint fragmentStart,
        long fileOffset,
        long fragmentSize,
        out nint fragmentContext) {
        if (fileOffset < 0)
            throw new ArgumentException(null, nameof(fileOffset));

        if (fileOffset + fragmentSize > this.memory.Length)
            throw new ArgumentException(null, nameof(fragmentSize));

        fragmentStart = (nint)this.pin.Pointer + (nint)fileOffset;
        fragmentContext = 0;
    }

    /// <inheritdoc/>
    public void ReleaseFileFragment(nint fragmentContext) { }

    /// <inheritdoc/>
    public long GetFileSize() => this.memory.Length;

    /// <inheritdoc/>
    public long GetLastWriteTime() => 0;
}
