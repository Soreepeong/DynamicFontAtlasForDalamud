using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using DynamicFontAtlasLib.TrueType.Enums;

namespace DynamicFontAtlasLib.TrueType.CommonStructs;

[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct PlatformAndEncoding {
    [FieldOffset(0)]
    public PlatformId Platform;

    [FieldOffset(2)]
    public UnicodeEncodingId UnicodeEncoding;

    [FieldOffset(2)]
    public MacintoshEncodingId MacintoshEncoding;

    [FieldOffset(2)]
    public IsoEncodingId IsoEncoding;

    [FieldOffset(2)]
    public WindowsEncodingId WindowsEncoding;

    public PlatformAndEncoding(PointerSpan<byte> source) {
        var offset = 0;
        source.ReadBig(ref offset, out this.Platform);
        source.ReadBig(ref offset, out this.UnicodeEncoding);
    }

    public static PlatformAndEncoding ReverseEndianness(PlatformAndEncoding value) => new() {
        Platform = (PlatformId)BinaryPrimitives.ReverseEndianness((ushort)value.Platform),
        UnicodeEncoding = (UnicodeEncodingId)BinaryPrimitives.ReverseEndianness((ushort)value.UnicodeEncoding),
    };

    public readonly string Decode(Span<byte> data) {
        switch (this.Platform) {
            case PlatformId.Unicode:
                switch (this.UnicodeEncoding) {
                    case UnicodeEncodingId.Unicode_2_0_Bmp:
                    case UnicodeEncodingId.Unicode_2_0_Full:
                        return Encoding.BigEndianUnicode.GetString(data);
                }

                break;

            case PlatformId.Macintosh:
                switch (this.MacintoshEncoding) {
                    case MacintoshEncodingId.Roman:
                        return Encoding.ASCII.GetString(data);
                }

                break;

            case PlatformId.Windows:
                switch (this.WindowsEncoding) {
                    case WindowsEncodingId.Symbol:
                    case WindowsEncodingId.UnicodeBmp:
                    case WindowsEncodingId.UnicodeFullRepertoire:
                        return Encoding.BigEndianUnicode.GetString(data);
                }

                break;
        }

        throw new NotSupportedException();
    }
}
