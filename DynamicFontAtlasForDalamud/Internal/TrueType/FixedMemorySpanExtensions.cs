using System;
using System.Buffers.Binary;

namespace DynamicFontAtlasLib.Internal.TrueType;

#pragma warning disable CS0649
internal static class FixedMemorySpanExtensions {
    public delegate T ElementFetcher<T>(Span<byte> span, int index);
    
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

    public static int BinarySearchBE(this Span<byte> span, ushort value) =>
        span.BinarySearchBE(value, (s, i) => s.UInt16At(i));

    public static int BinarySearchBE<T>(this Span<byte> span, T value, ElementFetcher<T> reader)
        where T : IComparable<T> {
        var l = 0;
        var r = (span.Length / 2) - 1;
        while (l <= r) {
            var i = (int)(((uint)r + (uint)l) >> 1);
            var c = value.CompareTo(reader(span, i));
            switch (c) {
                case 0:
                    return i;
                case > 0:
                    l = i + 1;
                    break;
                default:
                    r = i - 1;
                    break;
            }
        }

        return ~l;
    }

    public static ushort UInt16At(this Span<byte> span, int index) =>
        BinaryPrimitives.ReadUInt16BigEndian(span[(index * 2)..]);
}
