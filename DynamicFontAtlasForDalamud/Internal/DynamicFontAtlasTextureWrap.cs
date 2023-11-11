using System;
using System.Linq;
using Dalamud.Interface.Internal;
using DynamicFontAtlasLib.Internal.Utilities;
using DynamicFontAtlasLib.Internal.Utilities.StbRectPackSharp;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace DynamicFontAtlasLib.Internal;

/// <summary>
/// A TextureWrap for use from <see cref="DynamicFontAtlas"/>.
/// </summary>
internal sealed unsafe class DynamicFontAtlasTextureWrap : IDalamudTextureWrap {
    private readonly DisposeStack waste = new();
    private bool changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="device"/> class.
    /// </summary>
    /// <param name="device">DX device.</param>
    /// <param name="width">Texture width.</param>
    /// <param name="height">Texture height.</param>
    /// <param name="color">Whether to store colored glyphs.</param>
    public DynamicFontAtlasTextureWrap(
        Device device,
        int width,
        int height,
        bool color) {
        this.Width = width;
        this.Height = height;
        try {
            this.Data = new byte[width * height * 4];

            this.Device = this.waste.Add(device.QueryInterface<Device>());

            var desc = new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new(1, 0),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
            };

            this.Texture = this.waste.Add(new Texture2D(device, desc));

            this.View = this.waste.Add(new ShaderResourceView(
                device,
                this.Texture,
                new() {
                    Format = desc.Format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = { MipLevels = desc.MipLevels },
                }));

            this.Packers = Enumerable
                .Range(0, color ? 1 : 4)
                .Select(_ => new Packer(width - 1, height - 1))
                .ToArray();
        } catch (Exception) {
            this.waste.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the buffer for texture data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the GPU shader resource view.
    /// </summary>
    public ShaderResourceView View { get; }

    /// <summary>
    /// Gets the GPU Texture.
    /// </summary>
    public Texture2D Texture { get; }

    /// <summary>
    /// Gets the rectpackers.
    /// </summary>
    public Packer[] Packers { get; }

    /// <summary>
    /// Gets a value indicating whether this texture wrap will only contain fully colored glyphs.
    /// </summary>
    public bool UseColor => this.Packers.Length == 1;

    /// <inheritdoc/>
    public nint ImGuiHandle => this.View.NativePointer;

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    private Device Device { get; }

    /// <summary>
    /// Mark the underlying data as changed.
    /// </summary>
    public void MarkChanged() => this.changed = true;

    /// <summary>
    /// Apply the changed data onto GPU texture.
    /// </summary>
    public void ApplyChanges() {
        if (!this.changed)
            return;

        var box = this.Device.ImmediateContext.MapSubresource(
            this.Texture,
            0,
            MapMode.WriteDiscard,
            MapFlags.None);

        this.Data.AsSpan().CopyTo(new((void*)box.DataPointer, this.Data.Length));
        this.Device.ImmediateContext.UnmapSubresource(this.Texture, 0);
        this.changed = false;
    }

    /// <inheritdoc/>
    public void Dispose() => this.waste.Dispose();
}
