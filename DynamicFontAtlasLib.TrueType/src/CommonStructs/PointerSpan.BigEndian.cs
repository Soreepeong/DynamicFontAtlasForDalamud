using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DynamicFontAtlasLib.TrueType.CommonStructs;

public static partial class PointerSpan {
    public static short ReadI16Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(ps.Span[offset..]);

    public static int ReadI32Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(ps.Span[offset..]);

    public static long ReadI64Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadInt64BigEndian(ps.Span[offset..]);

    public static ushort ReadU16Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(ps.Span[offset..]);

    public static uint ReadU32Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(ps.Span[offset..]);

    public static ulong ReadU64Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadUInt64BigEndian(ps.Span[offset..]);

    public static Half ReadF16Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadHalfBigEndian(ps.Span[offset..]);

    public static float ReadF32Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadSingleBigEndian(ps.Span[offset..]);

    public static double ReadF64Big(this PointerSpan<byte> ps, int offset) =>
        BinaryPrimitives.ReadDoubleBigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out short value) =>
        value = BinaryPrimitives.ReadInt16BigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out int value) =>
        value = BinaryPrimitives.ReadInt32BigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out long value) =>
        value = BinaryPrimitives.ReadInt64BigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out ushort value) =>
        value = BinaryPrimitives.ReadUInt16BigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out uint value) =>
        value = BinaryPrimitives.ReadUInt32BigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out ulong value) =>
        value = BinaryPrimitives.ReadUInt64BigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out Half value) =>
        value = BinaryPrimitives.ReadHalfBigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out float value) =>
        value = BinaryPrimitives.ReadSingleBigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, int offset, out double value) =>
        value = BinaryPrimitives.ReadDoubleBigEndian(ps.Span[offset..]);

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out short value) {
        ps.ReadBig(offset, out value);
        offset += 2;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out int value) {
        ps.ReadBig(offset, out value);
        offset += 4;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out long value) {
        ps.ReadBig(offset, out value);
        offset += 8;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out ushort value) {
        ps.ReadBig(offset, out value);
        offset += 2;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out uint value) {
        ps.ReadBig(offset, out value);
        offset += 4;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out ulong value) {
        ps.ReadBig(offset, out value);
        offset += 8;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out Half value) {
        ps.ReadBig(offset, out value);
        offset += 2;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out float value) {
        ps.ReadBig(offset, out value);
        offset += 4;
    }

    public static void ReadBig(this PointerSpan<byte> ps, ref int offset, out double value) {
        ps.ReadBig(offset, out value);
        offset += 8;
    }

    public static unsafe T ReadEnumBig<T>(this PointerSpan<byte> ps, int offset) where T : unmanaged, Enum {
        switch (Marshal.SizeOf(Enum.GetUnderlyingType(typeof(T)))) {
            case 1:
                var b1 = ps.Span[offset];
                return *(T*)&b1;
            case 2:
                var b2 = ps.ReadU16Big(offset);
                return *(T*)&b2;
            case 4:
                var b4 = ps.ReadU32Big(offset);
                return *(T*)&b4;
            case 8:
                var b8 = ps.ReadU64Big(offset);
                return *(T*)&b8;
            default:
                throw new ArgumentException("Enum is not of size 1, 2, 4, or 8.", nameof(T), null);
        }
    }

    public static void ReadBig<T>(this PointerSpan<byte> ps, int offset, out T value) where T : unmanaged, Enum =>
        value = ps.ReadEnumBig<T>(offset);

    public static void ReadBig<T>(this PointerSpan<byte> ps, ref int offset, out T value) where T : unmanaged, Enum {
        value = ps.ReadEnumBig<T>(offset);
        offset += Unsafe.SizeOf<T>();
    }
}
