namespace DynamicFontAtlasLib.TrueType;

#pragma warning disable CS0649
internal static class FixedMemorySpanExtensions {
    public static unsafe ref T AsRef<T>(this Span<byte> data) where T : unmanaged {
        if (data.Length < sizeof(T))
            throw new InvalidOperationException($"{typeof(T).Name} takes {sizeof(T)} bytes < provided {data.Length}");
        fixed (void* p = data)
            return ref *(T*)p;
    }

    public static unsafe Span<T> AsSpan<T>(this Span<byte> data, int count)
        where T : unmanaged {
        fixed (void* p = data)
            return new(p, Math.Min(count, data.Length / sizeof(T)));
    }
}
