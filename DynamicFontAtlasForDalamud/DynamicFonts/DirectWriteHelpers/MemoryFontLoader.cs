using System;
using SharpDX;
using SharpDX.DirectWrite;

namespace DynamicFontAtlasLib.OnDemandFonts.DirectWriteHelpers;

internal class MemoryFontLoader : CallbackBase, FontCollectionLoader, FontFileLoader {
    internal static readonly ulong Signature = 0x855ba86686681e20UL;
    private static ulong Counter;

    private readonly Factory registeredFactory;

    public MemoryFontLoader(Factory factory, params Memory<byte>[] memories) {
        this.InstanceCounter = ++Counter;
        this.Memories = memories;

        this.Key = new(16, true, true);
        this.Key.Write(Signature);
        this.Key.Write(this.InstanceCounter);
        this.Key.Position = 0;

        this.registeredFactory = factory.QueryInterface<Factory>();
        this.registeredFactory.RegisterFontCollectionLoader(this);
        this.registeredFactory.RegisterFontFileLoader(this);
    }

    public DataStream Key { get; }

    internal ulong InstanceCounter { get; }

    internal Memory<byte>[] Memories { get; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        this.registeredFactory.UnregisterFontCollectionLoader(this);
        this.registeredFactory.UnregisterFontFileLoader(this);
        this.registeredFactory.Dispose();
    }

    /// <inheritdoc/>
    public unsafe FontFileEnumerator CreateEnumeratorFromKey(Factory factory, DataPointer collectionKey) {
        if (!new Span<byte>((void*)collectionKey.Pointer, collectionKey.Size)
                .SequenceEqual(new Span<byte>((void*)this.Key.DataPointer, (int)this.Key.Length)))
            throw new ArgumentException(null, nameof(collectionKey));

        return new MemoryFontFileEnumerator(factory, this);
    }

    /// <inheritdoc/>
    public unsafe FontFileStream CreateStreamFromKey(DataPointer fontFileReferenceKey) {
        if (fontFileReferenceKey.Size != 24)
            throw new ArgumentException(null, nameof(fontFileReferenceKey));

        var keys = (ulong*)fontFileReferenceKey.Pointer;
        if (keys[0] != Signature || keys[1] != this.InstanceCounter)
            throw new ArgumentException(null, nameof(fontFileReferenceKey));

        var index = checked((int)keys[2]);

        return new MemoryFontFileStream(this.Memories[index]);
    }
}
