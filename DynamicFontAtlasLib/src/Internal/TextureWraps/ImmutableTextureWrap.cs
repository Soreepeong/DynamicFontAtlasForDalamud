using System;
using Dalamud.Interface.Internal;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Tex.Buffers;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace DynamicFontAtlasLib.Internal.TextureWraps;

internal sealed unsafe class ImmutableTextureWrap : IDalamudTextureWrap {
    private readonly ShaderResourceView shaderResourceView;

    public ImmutableTextureWrap(Device device, nint dataPointer, int width, int pitch, int height, Format format) {
        var texDesc = new Texture2DDescription {
            Width = this.Width = width,
            Height = this.Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDescription = new(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None,
        };

        using var texture = new Texture2D(device, texDesc, new DataRectangle(dataPointer, pitch));
        this.shaderResourceView = new(device,
            texture,
            new() {
                Format = texDesc.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = { MipLevels = texDesc.MipLevels },
            });
    }

    public static ImmutableTextureWrap FromTexBytes(Device device, byte[] bytes) {
        var reader = new LuminaBinaryReader(bytes);
        var header = reader.ReadStructure<TexFile.TexHeader>();
        if ((header.Type & TexFile.Attribute.TextureTypeMask) != TexFile.Attribute.TextureType2D)
            throw new NotSupportedException();

        var bpp = 1 << (((int)header.Format & (int)TexFile.TextureFormat.BppMask) >>
            (int)TexFile.TextureFormat.BppShift);

        var dataSpan = bytes.AsSpan((int)header.OffsetToSurface[0]);
        var (dxgiFormat, conversion) = TexFile.GetDxgiFormatFromTextureFormat(header.Format, false);
        if (conversion != TexFile.DxgiFormatConversion.NoConversion
            || !device.CheckFormatSupport((Format)dxgiFormat).HasFlag(FormatSupport.Texture2D)) {
            
            var buffer = TextureBuffer.FromStream(header, reader);
            buffer = buffer.Filter(0, 0, header.Format = TexFile.TextureFormat.B8G8R8A8);
            dxgiFormat = (int)Format.B8G8R8A8_UNorm;
            bpp = 32;
            dataSpan = buffer.RawData;
        }

        var pitch = (header.Format & (TexFile.TextureFormat.TypeBc123 | TexFile.TextureFormat.TypeBc57)) != 0
            ? Math.Max(1, (header.Width + 3) / 4) * 2 * bpp
            : ((header.Width * bpp) + 7) / 8;
        
        fixed (void* pData = dataSpan)
            return new(device, (nint)pData, header.Width, pitch, header.Height, (Format)dxgiFormat);
    }

    public void Dispose() => this.shaderResourceView.Dispose();

    public IntPtr ImGuiHandle => this.shaderResourceView.NativePointer;
    public int Width { get; }
    public int Height { get; }
}
