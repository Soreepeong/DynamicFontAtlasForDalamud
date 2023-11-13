using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Internal;
using DynamicFontAtlasLib.FontIdentificationStructs;
using DynamicFontAtlasLib.Internal.DynamicFonts;
using DynamicFontAtlasLib.Internal.TextureWraps;
using DynamicFontAtlasLib.Internal.Utilities;
using DynamicFontAtlasLib.Internal.Utilities.ImGuiUtilities;
using ImGuiNET;
using SharpDX.Direct3D11;

namespace DynamicFontAtlasLib.Internal;

/// <summary>
/// A wrapper for <see cref="ImFontAtlas"/> for managing fonts in an easy way.
/// </summary>
internal sealed class DynamicFontAtlas : IDynamicFontAtlas {
    private readonly Device device;
    private readonly DirectoryInfo dalamudAssetDirectory;
    private readonly Func<string, Task<byte[]>> gameFileFetcher;

    private readonly DisposeStack disposeStack = new();
    private readonly ReaderWriterLockSlim fontLock = new();
    private readonly Dictionary<(FontIdent Ident, int SizePx), Task<DynamicFont>> fontEntries = new();
    private readonly Dictionary<(FontChain Chain, float Scale), Task<DynamicFont>> fontChains = new();
    private readonly Dictionary<int, HeightPlaceholderDynamicFont> fontPlaceholders = new();
    private readonly Dictionary<nint, DynamicFont> fontPtrToFont = new();

    private unsafe ImFontAtlas* pAtlas;
    private float lastGamma = float.NaN;
    private int suppressTextureUpdateCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFontAtlas"/> class.
    /// </summary>
    /// <param name="device">An instance ID3D11Device. It can be disposed after a call to constructor.</param>
    /// <param name="dalamudAssetDirectory">Path to Dalamud assets. If invalid, loading any of <see cref="BundledFonts"/> will fail.</param>
    /// <param name="gameFileFetcher">Fetcher callback for game files. If invalid, loading any of <see cref="GameFontFamilyAndSize"/> will fail.</param>
    /// <param name="cache">Cache.</param>
    public unsafe DynamicFontAtlas(
        Device device,
        DirectoryInfo dalamudAssetDirectory,
        Func<string, Task<byte[]>> gameFileFetcher,
        IDynamicFontAtlasCache cache) {
        this.gameFileFetcher = gameFileFetcher;
        this.Cache = cache;
        try {
            this.device = this.disposeStack.Add(device.QueryInterface<Device>());
            this.dalamudAssetDirectory = dalamudAssetDirectory;

            this.pAtlas = ImGuiNative.ImFontAtlas_ImFontAtlas();
            this.disposeStack.Add(() =>
            {
                ImGuiNative.ImFontAtlas_destroy(this.pAtlas);
                this.pAtlas = null;
            });

            this.ImTextures = new(&this.pAtlas->Textures, null);
            this.TextureWraps = new();
            this.Fonts = new(&this.pAtlas->Fonts, x => x->Destroy());
            this.CustomRects = new(&this.pAtlas->CustomRects, null);
            this.FontConfigs = new(&this.pAtlas->ConfigData, null);

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

    public unsafe bool IsDisposed => this.pAtlas is null;

    /// <inheritdoc/>
    public unsafe ImFontAtlasPtr AtlasPtr => new(this.pAtlas);

    /// <inheritdoc/>
    public PushFontMode DefaultPushFontErrorMode { get; set; } = PushFontMode.Ignore;

    /// <inheritdoc/>
    public PushFontMode DefaultPushFontLoadingMode { get; set; } = PushFontMode.Ignore;

    /// <inheritdoc/>
    public FontChain FallbackFontChain { get; set; }

    /// <inheritdoc/>
    public ConcurrentDictionary<string, Task<byte[]>> FontDataBytes { get; } = new();

    /// <inheritdoc/>
    public Func<float> ScaleGetter { get; set; } = () => 1f;

    /// <inheritdoc/>
    public Func<float> GammaGetter { get; set; } = () => 1.4f;

    /// <inheritdoc/>
    public bool GammaChangeShouldClear { get; set; }

    /// <summary>
    /// Gets the cache associated.
    /// </summary>
    internal IDynamicFontAtlasCache Cache { get; }

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
    internal unsafe Span<Vector4> TexUvLines => new(&this.pAtlas->TexUvLines_0, 64);

    /// <summary>
    /// Gets the gamma mapping table.
    /// </summary>
    internal byte[] GammaMappingTable { get; } = new byte[256];

    /// <inheritdoc/>
    public void Clear() => this.Clear(false);

    /// <inheritdoc/>
    public void ClearLoadErrorHistory() {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        this.fontLock.EnterWriteLock();
        try {
            foreach (var chain in this.fontChains
                         .Where(x => x.Value.IsFaulted || x.Value.IsCanceled)
                         .Select(x => x.Key)
                         .ToArray()) {
                this.fontChains.Remove(chain);
            }

            foreach (var chain in this.fontEntries
                         .Where(x => x.Value.IsFaulted || x.Value.IsCanceled)
                         .Select(x => x.Key)
                         .ToArray()) {
                this.fontEntries.Remove(chain);
            }
        } finally {
            this.fontLock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (this.IsDisposed)
            return;

        this.Clear(true);
        this.disposeStack.Dispose();
        this.ReleaseUnmanagedResources();

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public Exception? GetLoadException(in FontChain chain) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        this.fontLock.EnterReadLock();
        try {
            return this.fontChains.TryGetValue((chain, this.ScaleGetter()), out var task)
                ? task.Exception
                : null;
        } finally {
            this.fontLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void LoadGlyphs(ImFontPtr font, IEnumerable<char> chars) => this.GetFontFromPtr(font)?.LoadGlyphs(chars);

    /// <inheritdoc/>
    public void LoadGlyphs(ImFontPtr font, IEnumerable<UnicodeRange> ranges) =>
        this.GetFontFromPtr(font)?.LoadGlyphs(ranges);

    /// <inheritdoc/>
    public void SuppressTextureUpdatesEnter() {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        this.suppressTextureUpdateCounter++;
    }

    /// <inheritdoc/>
    public void SuppressTextureUpdatesExit() {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        if (--this.suppressTextureUpdateCounter == 0)
            this.UpdateTextures(true);
    }

    /// <inheritdoc/>
    public PushFontResult PushFontScoped(
        in FontChain chain,
        PushFontMode errorMode = PushFontMode.Ignore,
        PushFontMode loadingMode = PushFontMode.Ignore) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        if (errorMode == PushFontMode.Default) {
            errorMode = this.DefaultPushFontErrorMode;
            if (errorMode == PushFontMode.Default)
                errorMode = PushFontMode.Ignore;
        }

        if (loadingMode == PushFontMode.Default) {
            loadingMode = this.DefaultPushFontLoadingMode;
            if (loadingMode == PushFontMode.Default)
                loadingMode = PushFontMode.Ignore;
        }

        var sizePx = chain.PrimaryFont.SizePx * chain.LineHeight;

        try {
            var fontTask = this.GetFontTask(chain, this.ScaleGetter(), false);
            if (loadingMode == PushFontMode.Wait)
                fontTask.Wait();

            if (!fontTask.IsCompletedSuccessfully) {
                var mode = fontTask.IsFaulted ? errorMode : loadingMode;
                switch (mode) {
                    case PushFontMode.Ignore:
                        return new(this, default, fontTask.Exception, PushFontResultState.Empty);
                    case PushFontMode.HeightPlaceholder:
                        return new(this,
                            this.GetPlaceholderFont(sizePx).FontPtr,
                            fontTask.Exception,
                            PushFontResultState.Placeholder);
                    case PushFontMode.OptionalFallback:
                    case PushFontMode.OptionalHeightPlaceholderFallback:
                    case PushFontMode.RequiredFallback: {
                        var fallback = this.GetFallbackFontTask(sizePx);
                        if (mode == PushFontMode.RequiredFallback)
                            fallback.Wait();

                        if (fallback is { IsCompletedSuccessfully: true, Result: not null })
                            return new(this, fallback.Result.FontPtr, fontTask.Exception, PushFontResultState.Fallback);

                        if (mode == PushFontMode.OptionalHeightPlaceholderFallback) {
                            return new(this,
                                this.GetPlaceholderFont(sizePx).FontPtr,
                                fontTask.Exception,
                                PushFontResultState.Placeholder);
                        }

                        return new(this, default, fontTask.Exception, PushFontResultState.Empty);
                    }
                    case PushFontMode.Wait when fontTask.IsFaulted:
                        throw new ArgumentOutOfRangeException(nameof(errorMode),
                            errorMode,
                            $"{nameof(errorMode)} cannot be {PushFontMode.Wait}");
                    case PushFontMode.Wait:
                        throw new InvalidOperationException(
                            "Wait returned, IsCompletedSuccessfully is false, but IsFaulted is false?");
                    default:
                        throw new ArgumentOutOfRangeException(
                            fontTask.IsFaulted ? nameof(errorMode) : nameof(loadingMode),
                            fontTask.IsFaulted ? errorMode : loadingMode,
                            null);
                }
            }

            return new(this, fontTask.Result.FontPtr, null, PushFontResultState.Loaded);
        } catch (Exception e) {
            return new(this, null, e, PushFontResultState.Empty);
        }
    }

    /// <inheritdoc/>
    public void UpdateTextures(bool forceUpdate) {
        if (this.suppressTextureUpdateCounter > 0 && !forceUpdate)
            return;

        this.fontLock.EnterWriteLock();
        try {
            foreach (var x in this.TextureWraps.OfType<RectpackingTextureWrap>())
                x.ApplyChanges();
        } finally {
            this.fontLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Get the font wrapper. Fallback fonts are not applicable.
    /// </summary>
    /// <param name="ident">Font identifier.</param>
    /// <param name="sizePx">Size in pixels. Note that it will be rounded to nearest integers.</param>
    /// <returns>Found font wrapper.</returns>
    internal Task<DynamicFont> GetFontTask(in FontIdent ident, int sizePx) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        this.ProcessGammaChanges();

        this.fontLock.EnterUpgradeableReadLock();
        var writeLockEntered = false;
        try {
            if (this.fontEntries.TryGetValue((ident, sizePx), out var fontTask))
                return fontTask;

            var identCopy = ident;

            this.fontLock.EnterWriteLock();
            writeLockEntered = true;
            return this.fontEntries[(identCopy, sizePx)] = Task.Run<DynamicFont>(async () =>
            {
                switch (identCopy) {
                    case { Game: not GameFontFamily.Undefined }: {
                        var gfs = new GameFontStyle(
                            Constants.GetRecommendedFamilyAndSize(
                                identCopy.Game,
                                sizePx * 3f / 4));

                        var baseSizePx = gfs.FamilyAndSize == GameFontFamilyAndSize.TrumpGothic68
                            ? 68 * 4f / 3f
                            : gfs.SizePx;

                        if ((int)MathF.Round(baseSizePx) != sizePx) {
                            return new ScaledDynamicFont(
                                this,
                                null,
                                await this.GetFontTask(identCopy, (int)MathF.Round(baseSizePx)),
                                sizePx / baseSizePx);
                        }

                        var fdtPath = gfs.FamilyAndSize.GetFdtPath();
                        var fdt = await this.Cache.GetAsync(
                            fdtPath,
                            async () => new FdtReader(await this.gameFileFetcher(fdtPath)));

                        var textureIndices = await this.LoadGameFontTextures(
                            "common/font/font{0}.tex",
                            fdt.Glyphs.Max(x => x.TextureFileIndex) + 1);

                        return new AxisDynamicFont(this, null, gfs, fdt, textureIndices);
                    }

                    case { BundledFont: not BundledFonts.None and var bf }: {
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

                        if (!this.FontDataBytes.TryGetValue(path, out var dataTask))
                            this.FontDataBytes[path] = dataTask = File.ReadAllBytesAsync(path);

                        return DirectWriteDynamicFont.FromMemory(
                            this,
                            null,
                            string.Empty,
                            await dataTask.AsStarted(),
                            0,
                            sizePx,
                            identCopy);
                    }

                    case { System: { Name: { } name, Variant: var variant } }:
                        return DirectWriteDynamicFont.FromSystem(this, null, name, variant, sizePx);

                    case { File: { Path: { } path, Index: var index } }:
                        return DirectWriteDynamicFont.FromFile(this, null, path, index, sizePx);

                    case { Memory: { Name: { } name, Index: var index } }:
                        return DirectWriteDynamicFont.FromMemory(this,
                            null,
                            name,
                            await this.FontDataBytes[name].AsStarted(),
                            index,
                            sizePx,
                            null);

                    default:
                        throw new ArgumentException("Invalid identifier specification", nameof(identCopy));
                }
            }).ContinueWith(result =>
            {
                this.fontLock.EnterWriteLock();
                try {
                    var font = result.Result;
                    this.fontPtrToFont[font.FontIntPtr] = font;
                    this.Fonts.Add(font.FontPtr);
                    return font;
                } finally {
                    this.fontLock.ExitWriteLock();
                }
            });
        } finally {
            if (writeLockEntered)
                this.fontLock.ExitWriteLock();

            this.fontLock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Get the font wrapper.
    /// </summary>
    /// <param name="chain">Font chain.</param>
    /// <param name="scale">Scale to apply to all font idents.</param>
    /// <param name="noFallback">Whether to disable fallbacks.</param>
    /// <returns>Found font wrapper.</returns>
    internal Task<DynamicFont> GetFontTask(in FontChain chain, float scale, bool noFallback) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        if (!(chain.LineHeight > 0))
            throw new ArgumentException("LineHeight must be a positive number", nameof(chain));

        if (!(chain.GlyphRatio >= 0))
            throw new ArgumentException("Ratio must be a positive number or a zero", nameof(chain));

        if (chain.SecondaryFonts.Any(x => x.Ident == default) || chain.PrimaryFont == default)
            throw new ArgumentException("Font chain cannot contain an empty identifier", nameof(chain));

        this.ProcessGammaChanges();

        this.fontLock.EnterUpgradeableReadLock();
        var writeLockEntered = false;
        try {
            if (this.fontChains.TryGetValue((chain, scale), out var fontTask))
                return fontTask;

            var chainCopy = chain;

            this.fontLock.EnterWriteLock();
            writeLockEntered = true;
            return this.fontChains[(chain, scale)] = Task.Run<DynamicFont>(async () =>
            {
                var fallbackFont = noFallback
                    ? null
                    : await this.GetFallbackFontTask(chainCopy.PrimaryFont.SizePx);

                var subfonts = await Task.WhenAll(
                    chainCopy.SecondaryFonts
                        .Prepend(chainCopy.PrimaryFont)
                        .Select(entry => this.GetFontTask(entry.Ident, (int)MathF.Round(entry.SizePx * scale))));

                return new ChainedDynamicFont(this, fallbackFont, chainCopy, subfonts, scale);
            }).ContinueWith(result =>
            {
                this.fontLock.EnterWriteLock();
                try {
                    var font = result.Result;
                    this.fontPtrToFont[font.FontIntPtr] = font;
                    this.Fonts.Add(font.FontPtr);

                    return font;
                } finally {
                    this.fontLock.ExitWriteLock();
                }
            });
        } finally {
            if (writeLockEntered)
                this.fontLock.ExitWriteLock();

            this.fontLock.ExitUpgradeableReadLock();
        }
    }

    internal unsafe DynamicFont? GetFontFromPtr(ImFontPtr font) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        this.fontLock.EnterReadLock();
        try {
            return this.fontPtrToFont.GetValueOrDefault((nint)font.NativePtr);
        } finally {
            this.fontLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Allocate a space for the given glyph.
    /// </summary>
    /// <param name="glyph">The glyph.</param>
    internal void AllocateGlyphSpace(ref ImFontGlyphReal glyph) {
        if (!glyph.Visible)
            return;

        this.fontLock.EnterWriteLock();
        try {
            foreach (var i in Enumerable.Range(0, this.TextureWraps.Count + 1)) {
                RectpackingTextureWrap wrap;
                if (i < this.TextureWraps.Count) {
                    if (this.TextureWraps[i] is not RectpackingTextureWrap w
                        || w.UseColor != glyph.Colored)
                        continue;

                    wrap = w;
                } else {
                    if (i == 256)
                        throw new OutOfMemoryException();

                    wrap = new(this.device, this.AtlasPtr.TexWidth, this.AtlasPtr.TexHeight, glyph.Colored);

                    this.AddTextureWhileLocked(wrap);
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
        } finally {
            this.fontLock.ExitWriteLock();
        }
    }

    private async Task<DynamicFont?> GetFallbackFontTask(float sizePx) => this.FallbackFontChain.IsEmpty
        ? null
        : await this.GetFontTask(
            this.FallbackFontChain.ToScaled(
                sizePx / (this.FallbackFontChain.PrimaryFont.SizePx * this.FallbackFontChain.LineHeight)),
            this.ScaleGetter(),
            true);

    private void AddTextureWhileLocked(IDalamudTextureWrap wrap) {
        this.ImTextures.Add(new() { TexID = wrap.ImGuiHandle });
        this.TextureWraps.Add(wrap);
    }

    private unsafe void Clear(bool disposing) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        Task.WaitAll(this.fontEntries.Values.Concat(this.fontChains.Values).Select(e => e.ContinueWith(r =>
        {
            if (r.IsCompletedSuccessfully)
                r.Result.Dispose();
        })).ToArray());

        this.fontLock.EnterWriteLock();
        try {
            this.fontEntries.Clear();
            this.fontChains.Clear();
            this.fontPtrToFont.Clear();
            this.Fonts.Clear(true);

            this.TextureWraps.DisposeItems();
            this.TextureWraps.Clear();
            this.ImTextures.Clear();
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
            var wrap = new RectpackingTextureWrap(this.device, width, height, false);
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
            this.AddTextureWhileLocked(wrap);
        } finally {
            this.fontLock.ExitWriteLock();
        }
    }

    private HeightPlaceholderDynamicFont GetPlaceholderFont(float sizePx) {
        var size = (int)MathF.Round(sizePx);

        this.fontLock.EnterUpgradeableReadLock();
        try {
            if (!fontPlaceholders.TryGetValue(size, out var v)) {
                this.fontLock.EnterWriteLock();
                try {
                    if (!fontPlaceholders.TryGetValue(size, out v)) {
                        this.fontPlaceholders[size] = v = new(this, size);
                    }
                } finally {
                    this.fontLock.ExitWriteLock();
                }
            }

            return v;
        } finally {
            this.fontLock.ExitUpgradeableReadLock();
        }
    }

    private async Task<int[]> LoadGameFontTextures(string format, int count) {
        var textures = await Task.WhenAll(Enumerable.Range(1, count)
            .Select(async i =>
            {
                var texPath = string.Format(format, i);
                return new CachedDalamudTextureWrap(await this.Cache.GetScopedAsync(
                    texPath,
                    async () => ImmutableTextureWrap.FromTexBytes(
                        this.device,
                        await this.gameFileFetcher(texPath))));
            }));

        this.fontLock.EnterWriteLock();
        try {
            return textures.Select(texture =>
            {
                for (var i = 0; i < this.TextureWraps.Count; i++) {
                    if (this.TextureWraps[i].ImGuiHandle == texture.ImGuiHandle) {
                        texture.Dispose();
                        return i;
                    }
                }

                this.AddTextureWhileLocked(texture);
                return this.TextureWraps.Count - 1;
            }).ToArray();
        } finally {
            this.fontLock.ExitWriteLock();
        }
    }

    private unsafe void ReleaseUnmanagedResources() {
        if (!this.IsDisposed) {
            ImGuiNative.ImFontAtlas_destroy(this.pAtlas);
            this.pAtlas = null;
        }
    }

    private void ProcessGammaChanges() {
        var gamma = this.GammaGetter();
        if (!(Math.Abs(this.lastGamma - gamma) < 0.0001)) {
            var table = this.GammaMappingTable;
            for (var i = 0; i < 256; i++)
                table[i] = (byte)(MathF.Pow(Math.Clamp(i / 255.0f, 0.0f, 1.0f), 1.0f / gamma) * 255.0f);

            this.lastGamma = gamma;
            if (this.GammaChangeShouldClear)
                this.Clear(false);
        }
    }

    private class CachedDalamudTextureWrap : IDalamudTextureWrap {
        private readonly IDynamicFontAtlasCache.ICacheItemReference<IDalamudTextureWrap> reference;

        public CachedDalamudTextureWrap(IDynamicFontAtlasCache.ICacheItemReference<IDalamudTextureWrap> reference) {
            this.reference = reference;
        }

        public IntPtr ImGuiHandle => this.reference.Item.ImGuiHandle;
        public int Width => this.reference.Item.Width;
        public int Height => this.reference.Item.Height;

        public void Dispose() => this.reference.Dispose();
    }
}
