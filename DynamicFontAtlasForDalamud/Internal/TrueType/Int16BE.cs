using System;
using System.Buffers.Binary;

namespace DynamicFontAtlasLib.Internal.TrueType;

#pragma warning disable CS0649

public struct Int16BE : IComparable<Int16BE> {
    public short RawValue;

    public short Value {
        get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(this.RawValue) : this.RawValue;
        set => this.RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public static implicit operator Int16BE(short value) =>
        new() { RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value };

    public static explicit operator short(Int16BE value) =>
        BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value.RawValue) : value.RawValue;

    public int CompareTo(Int16BE other) => ((short)this).CompareTo((short)other);
}

public struct UInt16BE : IComparable<UInt16BE> {
    public ushort RawValue;

    public ushort Value {
        get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(this.RawValue) : this.RawValue;
        set => this.RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public static implicit operator UInt16BE(ushort value) =>
        new() { RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value };

    public static explicit operator ushort(UInt16BE value) =>
        BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value.RawValue) : value.RawValue;

    public int CompareTo(UInt16BE other) => ((ushort)this).CompareTo((ushort)other);
}

public struct UInt32BE : IComparable<UInt32BE> {
    public uint RawValue;

    public uint Value {
        get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(this.RawValue) : this.RawValue;
        set => this.RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public static implicit operator UInt32BE(uint value) =>
        new() { RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value };

    public static explicit operator uint(UInt32BE value) =>
        BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value.RawValue) : value.RawValue;

    public int CompareTo(UInt32BE other) => ((uint)this).CompareTo((uint)other);
}

public struct UInt64BE : IComparable<UInt64BE> {
    public ulong RawValue;

    public ulong Value {
        get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(this.RawValue) : this.RawValue;
        set => this.RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public static implicit operator UInt64BE(ulong value) =>
        new() { RawValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value };

    public static explicit operator ulong(UInt64BE value) =>
        BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value.RawValue) : value.RawValue;

    public int CompareTo(UInt64BE other) => ((ulong)this).CompareTo((ulong)other);
}
