using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

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

    public PlatformAndEncoding(PointerSpan<byte> source) {
        var offset = 0;
        source.ReadBE(ref offset, out this.Platform);
        source.ReadBE(ref offset, out this.UnicodeEncoding);
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
