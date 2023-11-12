using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reflection;
using System.Text.Unicode;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal;
using DynamicFontAtlasLib.Internal.DynamicFonts;
using DynamicFontAtlasLib.Internal.Utilities;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;
using ImGuiNET;
using Lumina.Data.Files;
using SharpDX.Direct3D11;

namespace DynamicFontAtlasLib;

/// <summary>
/// A wrapper for <see cref="ImFontAtlas"/> for managing fonts in an easy way.
/// </summary>
public sealed unsafe class DynamicFontAtlas : IDisposable {
    private readonly Device device;
    private readonly DirectoryInfo dalamudAssetDirectory;
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;

    private readonly DisposeStack disposeStack = new();
    private readonly ImFontAtlas* pAtlas;
    private readonly Dictionary<(FontIdent Ident, int SizePx), DynamicFont> fontEntries = new();
    private readonly Dictionary<(FontChain Chain, float Scale), DynamicFont> fontChains = new();
    private readonly Dictionary<nint, DynamicFont> fontPtrToFont = new();
    private readonly Dictionary<string, FdtReader> gameFdtFiles = new();
    private readonly Dictionary<string, int[]> gameFontTextures = new();
    private readonly Dictionary<FontChain, Exception> failedChains = new();
    private readonly Dictionary<(FontIdent Ident, int SizePx), Exception> failedIdents = new();

    private float lastGamma = float.NaN;
    private int suppressTextureUpdateCounter;
    private FontIdent? fallbackFontIdent;

    private readonly object interfaceManager;
    private readonly PropertyInfo fontGammaProperty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFontAtlas"/> class.
    /// </summary>
    /// <param name="device">An instance ID3D11Device.</param>
    /// <param name="dalamudAssetDirectory"></param>
    /// <param name="dataManager">An instance of IDataManager.</param>
    /// <param name="textureProvider">An instance of ITextureProvider.</param>
    public DynamicFontAtlas(
        Device device,
        DirectoryInfo dalamudAssetDirectory,
        IDataManager dataManager,
        ITextureProvider textureProvider) {
        try {
            this.device = this.disposeStack.Add(device.QueryInterface<Device>());
            this.dalamudAssetDirectory = dalamudAssetDirectory;
            this.dataManager = dataManager;
            this.textureProvider = textureProvider;

            this.pAtlas = ImGuiNative.ImFontAtlas_ImFontAtlas();
            this.disposeStack.Add(() => ImGuiNative.ImFontAtlas_destroy(this.pAtlas));

            this.ImTextures = new(&this.pAtlas->Textures, null);
            this.TextureWraps = new();
            this.Fonts = new(&this.pAtlas->Fonts, x => x->Destroy());
            this.CustomRects = new(&this.pAtlas->CustomRects, null);
            this.FontConfigs = new(&this.pAtlas->ConfigData, null);

            var serviceGenericType = Assembly.GetAssembly(typeof(IDalamudTextureWrap))!.DefinedTypes
                .Single(x => x.FullName == "Dalamud.Service`1");

            var interfaceManagerType = Assembly.GetAssembly(typeof(IDalamudTextureWrap))!.DefinedTypes
                .Single(x => x.FullName == "Dalamud.Interface.Internal.InterfaceManager");

            var serviceInterfaceManagerType = serviceGenericType.MakeGenericType(interfaceManagerType);
            this.interfaceManager = serviceInterfaceManagerType.GetMethod("Get")!.Invoke(null, null)!;
            this.fontGammaProperty = interfaceManagerType
                .GetProperty("FontGamma", BindingFlags.Public | BindingFlags.Instance)!;

            this.Clear(false);
        } catch {
            this.disposeStack.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="DynamicFontAtlas"/> class.
    /// </summary>
    ~DynamicFontAtlas() => this.ReleaseUnmanagedResources();

    /// <summary>
    /// Gets a value indicating whether it is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the wrapped <see cref="ImFontAtlasPtr"/>.
    /// </summary>
    public ImFontAtlasPtr AtlasPtr => new(this.pAtlas);

    /// <summary>
    /// Gets or sets the fallback font. Once set, until a call to <see cref="Clear"/>, the changes may not apply.
    /// </summary>
    public FontIdent? FallbackFontIdent {
        get => this.fallbackFontIdent;
        set {
            if (this.fallbackFontIdent == value)
                return;

            this.fallbackFontIdent = value;
            if (value is not null) {
                foreach (var f in this.fontPtrToFont.Values)
                    f.FallbackFontChanged();
            }
        }
    }

    /// <summary>
    /// Gets the dictionary containing callbacks for opening streams.
    /// </summary>
    public Dictionary<string, byte[]> FontDataBytes { get; } = new();

    /// <summary>
    /// Gets the reasons why <see cref="FontChain"/>s have failed to load.
    /// </summary>
    public IReadOnlyDictionary<FontChain, Exception> FailedChains => this.failedChains;

    /// <summary>
    /// Gets the reasons why <see cref="FontIdent"/>s have failed to load.
    /// </summary>
    public IReadOnlyDictionary<(FontIdent Ident, int SizePx), Exception> FailedIdents => this.failedIdents;

    /// <summary>
    /// Gets the list of associated <see cref="IDalamudTextureWrap"/>.
    /// </summary>
    internal List<IDalamudTextureWrap> TextureWraps { get; }

    /// <summary>
    /// Gets the wrapped vector of <see cref="ImFontAtlasTexture"/>.
    /// </summary>
    internal ImVectorWrapper<ImFontAtlasTexture> ImTextures { get; }

    /// <summary>
    /// Gets the wrapped vector of <see cref="ImFontPtr"/>.
    /// </summary>
    internal ImVectorWrapper<ImFontPtr> Fonts { get; }

    /// <summary>
    /// Gets the wrapped vector of <see cref="ImFontAtlasCustomRectReal"/>.
    /// </summary>
    internal ImVectorWrapper<ImFontAtlasCustomRectReal> CustomRects { get; }

    /// <summary>
    /// Gets the wrapped vector of <see cref="ImFontConfig"/>.
    /// </summary>
    internal ImVectorWrapper<ImFontConfig> FontConfigs { get; }

    /// <summary>
    /// Gets the TexUvLines as a <see cref="Span{T}"/>.
    /// </summary>
    internal Span<Vector4> TexUvLines => new(&this.pAtlas->TexUvLines_0, 64);

    /// <summary>
    /// Gets the gamma mapping table.
    /// </summary>
    internal byte[] GammaMappingTable { get; } = new byte[256];

    /// <summary>
    /// Gets the gamma level.
    /// </summary>
    private float GammaLevel => (float)this.fontGammaProperty.GetValue(this.interfaceManager)!;

    /// <summary>
    /// Gets the font corresponding to the given specifications.
    /// </summary>
    /// <param name="ident">Font identifier.</param>
    /// <param name="sizePx">Size in pixels.</param>
    public ImFontPtr this[in FontIdent ident, float sizePx] => this[new(new FontChainEntry(ident, sizePx))];

    /// <summary>
    /// Gets the font corresponding to the given specifications.
    /// </summary>
    /// <param name="chain">Font chain.</param>
    public ImFontPtr this[in FontChain chain] => this.GetDynamicFont(chain).FontPtr;

    /// <summary>
    /// Clears all the loaded fonts from the atlas.
    /// </summary>
    public void Clear() {
        this.Clear(false);
        this.ClearLoadErrorHistory();
    }

    /// <summary>
    /// Reset recorded font load errors, so that on next access, font will be attempted for load again.
    /// </summary>
    public void ClearLoadErrorHistory() {
        this.failedChains.Clear();
        this.failedIdents.Clear();
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (this.IsDisposed)
            return;

        this.Clear(true);
        this.disposeStack.Dispose();
        this.ReleaseUnmanagedResources();

        this.IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="chars">Chars.</param>
    public void LoadGlyphs(params char[] chars) => this.LoadGlyphs(ImGui.GetFont(), chars);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="chars">Chars.</param>
    public void LoadGlyphs(IEnumerable<char> chars) => this.LoadGlyphs(ImGui.GetFont(), chars);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into <paramref name="font"/>, if it is managed by this.
    /// </summary>
    /// <param name="font">Relevant font.</param>
    /// <param name="chars">Chars.</param>
    public void LoadGlyphs(ImFontPtr font, IEnumerable<char> chars) =>
        this.fontPtrToFont.GetValueOrDefault((nint)font.NativePtr)?.LoadGlyphs(chars);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="ranges">Ranges.</param>
    public void LoadGlyphs(params UnicodeRange[] ranges) => this.LoadGlyphs(ImGui.GetFont(), ranges);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into currently active ImGui font, if it is managed by this.
    /// </summary>
    /// <param name="ranges">Ranges.</param>
    public void LoadGlyphs(IEnumerable<UnicodeRange> ranges) => this.LoadGlyphs(ImGui.GetFont(), ranges);

    /// <summary>
    /// Load the glyphs corresponding to the given chars into <paramref name="font"/>, if it is managed by this.
    /// </summary>
    /// <param name="font">Relevant font.</param>
    /// <param name="ranges">Ranges.</param>
    public void LoadGlyphs(ImFontPtr font, IEnumerable<UnicodeRange> ranges) =>
        this.fontPtrToFont.GetValueOrDefault((nint)font.NativePtr)?.LoadGlyphs(ranges);

    /// <summary>
    /// Suppress uploading updated texture onto GPU for the scope.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that will make it update the texture on dispose.</returns>
    public IDisposable? SuppressTextureUpdatesScoped() {
        if (this.IsDisposed)
            return null;

        this.suppressTextureUpdateCounter++;
        return Disposable.Create(
            () =>
            {
                if (--this.suppressTextureUpdateCounter == 0)
                    this.UpdateTextures();
            });
    }

    /// <summary>
    /// Fetch a font, and if it succeeds, push it onto the stack.
    /// </summary>
    /// <param name="ident">Font identifier.</param>
    /// <param name="sizePx">Font size in pixels.</param>
    /// <returns>An <see cref="IDisposable"/> that will make it pop the font on dispose.</returns>
    /// <remarks>It will return null on failure, and exception will be stored in <see cref="FailedIdents"/>.</remarks>
    public IDisposable? PushFontScoped(in FontIdent ident, float sizePx) =>
        PushFontScoped(new(new FontChainEntry(ident, sizePx)));

    /// <summary>
    /// Fetch a font, and if it succeeds, push it onto the stack.
    /// </summary>
    /// <param name="chain">Font chain.</param>
    /// <returns>An <see cref="IDisposable"/> that will make it pop the font on dispose.</returns>
    /// <remarks>It will return null on failure, and exception will be stored in <see cref="FailedChains"/>.</remarks>
    public IDisposable? PushFontScoped(in FontChain chain) {
        if (this.IsDisposed)
            return null;

        if (this.failedChains.TryGetValue(chain, out _))
            return null;

        try {
            ImGui.PushFont(this[chain]);
        } catch {
            return null;
        }

        this.suppressTextureUpdateCounter++;
        return Disposable.Create(
            () =>
            {
                ImGui.PopFont();
                if (--this.suppressTextureUpdateCounter == 0)
                    this.UpdateTextures();
            });
    }

    /// <summary>
    /// Upload updated textures onto GPU, if not suppressed.
    /// </summary>
    public void UpdateTextures() {
        foreach (var tw in this.TextureWraps) {
            if (this.suppressTextureUpdateCounter <= 0 && tw is DynamicFontAtlasTextureWrap utw)
                utw.ApplyChanges();
        }
    }

    /// <summary>
    /// Get the font wrapper.
    /// </summary>
    /// <param name="ident">Font identifier.</param>
    /// <param name="sizePx">Size in pixels. Note that it will be rounded to nearest integers.</param>
    /// <returns>Found font wrapper.</returns>
    internal DynamicFont GetDynamicFont(in FontIdent ident, int sizePx) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        this.ProcessGammaChanges();

        if (this.fontEntries.TryGetValue((ident, sizePx), out var wrapper))
            return wrapper;

        if (this.failedIdents.TryGetValue((ident, sizePx), out var previousException))
            throw new AggregateException(previousException);

        try {
            switch (ident) {
                case { Game: not GameFontFamily.Undefined }:
                {
                    var gfs = new GameFontStyle(Constants.GetRecommendedFamilyAndSize(ident.Game, sizePx * 3f / 4));
                    var baseSizePx = gfs.FamilyAndSize == GameFontFamilyAndSize.TrumpGothic68 ? 68 * 4f / 3f : gfs.SizePx;
                    if ((int)MathF.Round(baseSizePx) == sizePx) {
                        const string filename = "font{}.tex";

                        var fdtPath = gfs.FamilyAndSize.GetFdtPath();
                        if (!this.gameFdtFiles.TryGetValue(fdtPath, out var fdt))
                            this.gameFdtFiles.Add(fdtPath, fdt = new(this.dataManager.GetFile(fdtPath)!.Data));

                        var numExpectedTex = fdt.Glyphs.Max(x => x.TextureFileIndex) + 1;
                        if (!this.gameFontTextures.TryGetValue(filename, out var textureIndices)
                            || textureIndices.Length < numExpectedTex) {
                            this.UpdateTextures();

                            var newTextureWraps = new IDalamudTextureWrap?[numExpectedTex];
                            var newTextureIndices = new int[numExpectedTex];
                            using (var errorDispose = new DisposeStack()) {
                                var addCounter = 0;
                                for (var i = 0; i < numExpectedTex; i++) {
                                    // Note: texture index for these cannot be 0, since it is occupied by ImGui.
                                    if (textureIndices is not null && i < textureIndices.Length) {
                                        newTextureIndices[i] = textureIndices[i];
                                        Debug.Assert(
                                            this.TextureWraps[i] is not null,
                                            "textureIndices[i] != 0 but this.TextureWraps[i] is null");

                                        continue;
                                    }

                                    newTextureWraps[i] = errorDispose.Add(this.textureProvider.GetTexture(
                                        this.dataManager.GetFile<TexFile>($"common/font/font{1 + i}.tex")
                                        ?? throw new FileNotFoundException($"Texture #{1 + i} not found")));

                                    newTextureIndices[i] = this.TextureWraps.Count + addCounter++;
                                }

                                this.ImTextures.EnsureCapacity(this.ImTextures.Length + addCounter);
                                this.TextureWraps.EnsureCapacity(this.TextureWraps.Count + addCounter);
                                errorDispose.Cancel();
                            }

                            this.gameFontTextures[filename] = textureIndices = newTextureIndices;

                            foreach (var i in Enumerable.Range(0, numExpectedTex)) {
                                if (newTextureWraps[i] is not { } wrap)
                                    continue;

                                Debug.Assert(
                                    textureIndices[i] == this.ImTextures.Length
                                    && textureIndices[i] == this.TextureWraps.Count,
                                    "Counts must be same");

                                this.AddTexture(wrap);
                            }
                        }

                        wrapper = new AxisDynamicFont(this, gfs, fdt, textureIndices);
                    } else {
                        var baseFontIdent = this.GetDynamicFont(ident, (int)MathF.Round(baseSizePx)).FontPtr.NativePtr;
                        wrapper = new ScaledDynamicFont(
                            this,
                            this.fontPtrToFont[(nint)baseFontIdent],
                            sizePx / baseSizePx);
                    }

                    break;
                }

                case { BundledFont: not BundledFonts.None and var bf }:
                {
                    var path = Path.Join(
                        this.dalamudAssetDirectory.FullName,
                        "UIRes",
                        bf switch {
                            BundledFonts.None => throw new InvalidOperationException("should not happen"),
                            BundledFonts.NotoSansJpMedium => "NotoSansCJKjp-Medium.otf",
                            BundledFonts.NotoSansKrRegular => "NotoSansKR-Regular.otf",
                            BundledFonts.InconsolataRegular => "Inconsolata-Regular.ttf",
                            BundledFonts.FontAwesomeFreeSolid => "FontAwesomeFreeSolid.otf",
                            _ => throw new NotSupportedException(),
                        });

                    if (!this.FontDataBytes.TryGetValue(path, out var data)) {
                        data = File.ReadAllBytes(path);
                        this.FontDataBytes.Add(path, data);
                    }

                    wrapper = DirectWriteDynamicFont.FromMemory(
                        this,
                        string.Empty,
                        data,
                        0,
                        sizePx,
                        ident);

                    break;
                }

                case { System: { Name: { } name, Variant: var variant } }:
                    wrapper = DirectWriteDynamicFont.FromSystem(this, name, variant, sizePx);
                    break;

                case { File: { Path: { } path, Index: var index } }:
                    wrapper = DirectWriteDynamicFont.FromFile(this, path, index, sizePx);
                    break;

                case { Memory: { Name: { } name, Index: var index } }:
                    wrapper = DirectWriteDynamicFont.FromMemory(this,
                        name,
                        this.FontDataBytes[name],
                        index,
                        sizePx,
                        null);

                    break;

                default:
                    throw new ArgumentException("Invalid identifier specification", nameof(ident));
            }

            this.fontEntries[(ident, sizePx)] = wrapper;
            this.fontPtrToFont[(nint)wrapper.FontPtr.NativePtr] = wrapper;
            this.Fonts.Add(wrapper.FontPtr);
            wrapper.Font.ContainerAtlas = this.AtlasPtr;

            return wrapper;
        } catch (Exception e) {
            this.failedIdents[(ident, sizePx)] = e;
            throw;
        }
    }

    /// <summary>
    /// Get the font wrapper.
    /// </summary>
    /// <param name="chain">Font chain.</param>
    /// <returns>Found font wrapper.</returns>
    internal DynamicFont GetDynamicFont(in FontChain chain) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        if (this.failedChains.TryGetValue(chain, out var previousException))
            throw new AggregateException(previousException);

        if (!(chain.LineHeight > 0))
            throw new ArgumentException("LineHeight must be a positive number", nameof(chain));

        if (!(chain.GlyphRatio >= 0))
            throw new ArgumentException("Ratio must be a positive number or a zero", nameof(chain));
    
        this.ProcessGammaChanges();

        try {
            var scale = ImGuiHelpers.GlobalScale;
            if (chain.SecondaryFonts.Any(x => x.Ident == default) || chain.PrimaryFont == default)
                throw new ArgumentException("Font chain cannot contain an empty identifier", nameof(chain));

            if (this.fontChains.TryGetValue((chain, scale), out var wrapper))
                return wrapper;

            wrapper = new ChainedDynamicFont(
                this,
                chain,
                chain.SecondaryFonts
                    .Prepend(chain.PrimaryFont)
                    .Select(entry => this.GetDynamicFont(entry.Ident, (int)MathF.Round(entry.SizePx * scale))),
                scale);

            this.fontChains[(chain, scale)] = wrapper;
            this.fontPtrToFont[(nint)wrapper.FontPtr.NativePtr] = wrapper;
            this.Fonts.Add(wrapper.FontPtr);
            wrapper.Font.ContainerAtlas = this.AtlasPtr;
            return wrapper;
        } catch (Exception e) {
            this.failedChains[chain] = e;
            throw;
        }
    }

    /// <summary>
    /// Allocate a space for the given glyph.
    /// </summary>
    /// <param name="glyph">The glyph.</param>
    internal void AllocateGlyphSpace(ref ImFontGlyphReal glyph) {
        if (!glyph.Visible)
            return;

        foreach (var i in Enumerable.Range(0, this.TextureWraps.Count + 1)) {
            DynamicFontAtlasTextureWrap wrap;
            if (i < this.TextureWraps.Count) {
                if (this.TextureWraps[i] is not DynamicFontAtlasTextureWrap w
                    || w.UseColor != glyph.Colored)
                    continue;

                wrap = w;
            } else {
                if (i == 256)
                    throw new OutOfMemoryException();

                wrap = new(this.device, this.AtlasPtr.TexWidth, this.AtlasPtr.TexHeight, glyph.Colored);

                this.AddTexture(wrap);
            }

            for (var j = 0; j < wrap.Packers.Length; j++) {
                var packer = wrap.Packers[j];
                var width = (int)(glyph.X1 - glyph.X0);
                var height = (int)(glyph.Y1 - glyph.Y0);

                var rc = packer.PackRect(width + 1, height + 1, null!);
                if (rc is null)
                    continue;

                glyph.TextureIndex = i;
                var du = glyph.Colored ? 0 : 1 + j;
                glyph.U0 = du + ((float)(rc.X + 1) / wrap.Width);
                glyph.U1 = du + ((float)(rc.X + 1 + width) / wrap.Width);
                glyph.V0 = (float)(rc.Y + 1) / wrap.Height;
                glyph.V1 = (float)(rc.Y + 1 + height) / wrap.Height;
                return;
            }
        }
    }

    private void AddTexture(IDalamudTextureWrap wrap) {
        this.ImTextures.Add(new() { TexID = wrap.ImGuiHandle });
        this.TextureWraps.Add(wrap);
    }

    private void Clear(bool disposing) {
        this.fontEntries.Values.Concat(this.fontChains.Values).DisposeItems();
        this.fontEntries.Clear();
        this.fontChains.Clear();
        this.fontPtrToFont.Clear();
        this.Fonts.Clear(true);

        var retainedGameFontTextures = disposing
            ? new()
            : this.gameFontTextures
                .Select(x => (x.Key, Value: x.Value.Select(y => this.TextureWraps[y]).ToArray()))
                .ToDictionary(x => x.Key, x => x.Value);

        var retainedWraps = retainedGameFontTextures.SelectMany(x => x.Value).Distinct().ToArray();

        this.TextureWraps.Where(x => !retainedWraps.Contains(x)).DisposeItems();
        this.TextureWraps.Clear();
        this.ImTextures.Clear();
        this.gameFontTextures.Clear();
        this.AtlasPtr.Clear();

        if (disposing)
            return;

        this.pAtlas->TexWidth = this.pAtlas->TexDesiredWidth = 1024;
        this.pAtlas->TexHeight = this.pAtlas->TexDesiredHeight = 1024;
        this.pAtlas->TexGlyphPadding = 1;

        // need a space for shapes, so need to call Build
        // calling Build does AddFontDefault anyway if no font is configured
        var conf = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
        try {
            fixed (ushort* dummyRange = stackalloc ushort[] { ' ', ' ', 0 }) {
                conf.GlyphRanges = (nint)dummyRange;
                this.AtlasPtr.AddFontDefault(conf);
                this.AtlasPtr.Build();
                this.Fonts.Clear();
            }
        } finally {
            conf.Destroy();
        }

        Debug.Assert(this.ImTextures.Length == 1);

        this.AtlasPtr.GetTexDataAsAlpha8(0, out nint alphaValues, out var width, out var height);
        var wrap = new DynamicFontAtlasTextureWrap(this.device, width, height, false);
        fixed (byte* pixels = wrap.Data) {
            for (var y = 0; y < height; y++) {
                var sourceRow = new Span<byte>((byte*)alphaValues + (y * width), width);
                var targetRow = new Span<uint>(pixels + (y * width * 4), width);
                for (var x = 0; x < width; x++)
                    targetRow[x] = (uint)sourceRow[x] << 16;
            }
        }

        wrap.MarkChanged();
        wrap.ApplyChanges();

        try {
            // We don't need to have ImGui keep the buffer.
            this.AtlasPtr.ClearTexData();

            // We rely on the implementation detail that default custom rects stick to top left,
            // and the rectpack we're using will stick the first item to the top left.
            wrap.Packers[0].PackRect(
                this.CustomRects.Aggregate(0, (a, x) => Math.Max(a, x.X + x.Width)) + 1,
                this.CustomRects.Aggregate(0, (a, x) => Math.Max(a, x.Y + x.Height)) + 1,
                null!);

            // Mark them to use the first channel.
            foreach (ref var v4 in this.TexUvLines)
                v4 += new Vector4(1, 0, 1, 0);

            this.pAtlas->TexUvWhitePixel += new Vector2(1, 0);
        } catch {
            wrap.Dispose();
            throw;
        }

        this.ImTextures.Clear();
        this.AddTexture(wrap);

        foreach (var t in retainedWraps)
            this.AddTexture(t);

        foreach (var (key, values) in retainedGameFontTextures)
            this.gameFontTextures[key] = values.Select(x => this.TextureWraps.IndexOf(x)).ToArray();
    }

    private void ReleaseUnmanagedResources() {
        if (this.IsDisposed) {
            ImGuiNative.ImFontAtlas_destroy(this.pAtlas);
            this.IsDisposed = true;
        }
    }

    private void ProcessGammaChanges() {
        var gamma = this.GammaLevel;
        if (!(Math.Abs(this.lastGamma - gamma) < 0.0001)) {
            var table = this.GammaMappingTable;
            for (var i = 0; i < 256; i++)
                table[i] = (byte)(MathF.Pow(Math.Clamp(i / 255.0f, 0.0f, 1.0f), 1.0f / gamma) * 255.0f);

            this.lastGamma = gamma;
            this.Clear(false);
        }
    }
}
