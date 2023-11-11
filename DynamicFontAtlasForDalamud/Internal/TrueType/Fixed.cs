using System;
using System.Buffers.Binary;

namespace DynamicFontAtlasLib.Internal.TrueType;

public struct Fixed {
    public ushort Major;
    public ushort Minor;

    public Fixed(Span<byte> span) {
        this.Major = BinaryPrimitives.ReadUInt16BigEndian(span);
        this.Minor = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
    }
}
