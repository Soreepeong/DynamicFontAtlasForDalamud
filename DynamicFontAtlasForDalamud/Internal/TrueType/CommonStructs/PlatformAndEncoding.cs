using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicFontAtlasLib.Internal.TrueType;

[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct PlatformAndEncoding {
    [FieldOffset(0)]
    public PlatformId Platform;

    [FieldOffset(2)]
    public UnicodePlatformEncodingId UnicodeEncoding;

    [FieldOffset(2)]
    public MacintoshPlatformEncodingId MacintoshEncoding;

    [FieldOffset(2)]
    public IsoPlatformEncodingId IsoEncoding;

    [FieldOffset(2)]
    public WindowsPlatformEncodingId WindowsEncoding;

    public PlatformAndEncoding(Span<byte> source) {
        this.Platform = (PlatformId)BinaryPrimitives.ReadUInt16BigEndian(source);
        this.UnicodeEncoding = (UnicodePlatformEncodingId)BinaryPrimitives.ReadUInt16BigEndian(source[2..]);
    }

    public readonly string Decode(Span<byte> data) {
        switch (this.Platform) {
            case PlatformId.Unicode:
                switch (this.UnicodeEncoding) {
                    case UnicodePlatformEncodingId.Unicode_2_0_Bmp:
                    case UnicodePlatformEncodingId.Unicode_2_0_Full:
                        return Encoding.BigEndianUnicode.GetString(data);
                }

                break;

            case PlatformId.Macintosh:
                switch (this.MacintoshEncoding) {
                    case MacintoshPlatformEncodingId.Roman:
                        return Encoding.ASCII.GetString(data);
                }

                break;

            case PlatformId.Windows:
                switch (this.WindowsEncoding) {
                    case WindowsPlatformEncodingId.Symbol:
                    case WindowsPlatformEncodingId.UnicodeBmp:
                    case WindowsPlatformEncodingId.UnicodeFullRepertoire:
                        return Encoding.BigEndianUnicode.GetString(data);
                }

                break;
        }

        throw new NotSupportedException();
    }
}
