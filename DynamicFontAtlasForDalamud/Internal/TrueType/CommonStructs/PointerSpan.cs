using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

public static class PointerSpan {
    public static short ReadI16BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadInt16BigEndian(ps.Span[offset..]);
    public static int ReadI32BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadInt32BigEndian(ps.Span[offset..]);
    public static long ReadI64BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadInt64BigEndian(ps.Span[offset..]);
    public static ushort ReadU16BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadUInt16BigEndian(ps.Span[offset..]);
    public static uint ReadU32BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadUInt32BigEndian(ps.Span[offset..]);
    public static ulong ReadU64BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadUInt64BigEndian(ps.Span[offset..]);
    public static Half ReadF16BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadHalfBigEndian(ps.Span[offset..]);
    public static float ReadF32BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadSingleBigEndian(ps.Span[offset..]);
    public static double ReadF64BE(this PointerSpan<byte> ps, int offset) => BinaryPrimitives.ReadDoubleBigEndian(ps.Span[offset..]);
    
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out short value) => value = BinaryPrimitives.ReadInt16BigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out int value) => value = BinaryPrimitives.ReadInt32BigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out long value) => value = BinaryPrimitives.ReadInt64BigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out ushort value) => value = BinaryPrimitives.ReadUInt16BigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out uint value) => value = BinaryPrimitives.ReadUInt32BigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out ulong value) => value = BinaryPrimitives.ReadUInt64BigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out Half value) => value = BinaryPrimitives.ReadHalfBigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out float value) => value = BinaryPrimitives.ReadSingleBigEndian(ps.Span[offset..]);
    public static void ReadBE(this PointerSpan<byte> ps, int offset, out double value) => value = BinaryPrimitives.ReadDoubleBigEndian(ps.Span[offset..]);
    
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out short value) { ps.ReadBE(offset, out value); offset += 2; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out int value) { ps.ReadBE(offset, out value); offset += 4; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out long value) { ps.ReadBE(offset, out value); offset += 8; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out ushort value) { ps.ReadBE(offset, out value); offset += 2; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out uint value) { ps.ReadBE(offset, out value); offset += 4; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out ulong value) { ps.ReadBE(offset, out value); offset += 8; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out Half value) { ps.ReadBE(offset, out value); offset += 2; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out float value) { ps.ReadBE(offset, out value); offset += 4; }
    public static void ReadBE(this PointerSpan<byte> ps, ref int offset, out double value) { ps.ReadBE(offset, out value); offset += 8; }

    public static T ReadEnumBE<T>(this PointerSpan<byte> ps, int offset) where T : unmanaged, Enum =>
        Marshal.SizeOf<T>() switch {
            1 => (T)(object)ps[offset],
            2 => (T)(object)BinaryPrimitives.ReadInt16BigEndian(ps.Span[offset..]),
            4 => (T)(object)BinaryPrimitives.ReadInt32BigEndian(ps.Span[offset..]),
            8 => (T)(object)BinaryPrimitives.ReadInt64BigEndian(ps.Span[offset..]),
            _ => throw new NotSupportedException(),
        };
    
    public static void ReadBE<T>(this PointerSpan<byte> ps, int offset, out T value) where T : unmanaged, Enum =>
        value = ps.ReadEnumBE<T>(offset);
    
    public static void ReadBE<T>(this PointerSpan<byte> ps, ref int offset, out T value) where T : unmanaged, Enum {
        value = ps.ReadEnumBE<T>(offset);
        offset += Unsafe.SizeOf<T>();
    }

    public static int BinarySearch<T>(this IReadOnlyList<T> span, in T value)
        where T : unmanaged, IComparable<T> {
        var l = 0;
        var r = span.Count - 1;
        while (l <= r) {
            var i = (int)(((uint)r + (uint)l) >> 1);
            var c = value.CompareTo(span[i]);
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
}
