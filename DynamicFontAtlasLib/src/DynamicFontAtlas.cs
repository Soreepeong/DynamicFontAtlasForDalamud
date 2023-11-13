using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reflection;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
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
public sealed class DynamicFontAtlas : IDisposable {
    private readonly Device device;
    private readonly DirectoryInfo dalamudAssetDirectory;
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;

    private readonly DisposeStack disposeStack = new();
    private readonly unsafe ImFontAtlas* pAtlas;
    private readonly ReaderWriterLockSlim fontLock = new();
    private readonly Dictionary<(FontIdent Ident, int SizePx), Task<DynamicFont>> fontEntries = new();
    private readonly Dictionary<(FontChain Chain, float Scale), Task<DynamicFont>> fontChains = new();
    private readonly Dictionary<nint, DynamicFont> fontPtrToFont = new();
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
    /// <param name="cache">Cache.</param>
    public unsafe DynamicFontAtlas(
        Device device,
        DirectoryInfo dalamudAssetDirectory,
        IDataManager dataManager,
        ITextureProvider textureProvider,
        IDynamicFontAtlasCache cache) {
        this.Cache = cache;
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
    public unsafe ImFontAtlasPtr AtlasPtr => new(this.pAtlas);

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
                this.fontLock.EnterReadLock();
                try {
                    foreach (var f in this.fontPtrToFont.Values)
                        f.FallbackFontChanged();
                } finally {
                    this.fontLock.ExitReadLock();
                }
            }
        }
    }

    /// <summary>
    /// Gets the dictionary containing callbacks for opening streams.
    /// </summary>
    public ConcurrentDictionary<string, byte[]> FontDataBytes { get; } = new();

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

    /// <summary>
    /// Gets the gamma level.
    /// </summary>
    private float GammaLevel => (float)this.fontGammaProperty.GetValue(this.interfaceManager)!;

    /// <summary>
    /// Gets the reasons why <see cref="FontChain"/>s have failed to load.
    /// </summary>
    private IReadOnlyDictionary<FontChain, Exception> FailedChains => this.failedChains;

    /// <summary>
    /// Gets the reasons why <see cref="FontIdent"/>s have failed to load.
    /// </summary>
    private IReadOnlyDictionary<(FontIdent Ident, int SizePx), Exception> FailedIdents => this.failedIdents;


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
    /// Gets the reason why <paramref name="ident"/> of size <paramref name="sizePx"/> failed to load.
    /// </summary>
    /// <param name="ident">The font identifier.</param>
    /// <param name="sizePx">The font size in pixels.</param>
    /// <returns>Exception, if any.</returns>
    public Exception? GetLoadException(in FontIdent ident, float sizePx) =>
        this.GetLoadException(new(new FontChainEntry(ident, sizePx)));

    /// <summary>
    /// Gets the reason why <paramref name="chain"/> failed to load.
    /// </summary>
    /// <param name="chain">The chain to query.</param>
    /// <returns>Exception, if any.</returns>
    public Exception? GetLoadException(in FontChain chain) {
        this.fontLock.EnterReadLock();
        try {
            return this.failedChains.GetValueOrDefault(chain);
        } finally {
            this.fontLock.ExitReadLock();
        }
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
    public void LoadGlyphs(ImFontPtr font, IEnumerable<char> chars) => this.GetFontFromPtr(font)?.LoadGlyphs(chars);

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
        this.GetFontFromPtr(font)?.LoadGlyphs(ranges);

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
    /// <param name="waitForLoad">Wait for the font to be loaded.</param>
    /// <returns>An <see cref="IDisposable"/> that will make it pop the font on dispose.</returns>
    /// <remarks>It will return null on failure, and exception will be stored in <see cref="FailedIdents"/>.</remarks>
    public IDisposable? PushFontScoped(in FontIdent ident, float sizePx, bool waitForLoad = false) =>
        this.PushFontScoped(new(new FontChainEntry(ident, sizePx)), waitForLoad);

    /// <summary>
    /// Fetch a font, and if it succeeds, push it onto the stack.
    /// </summary>
    /// <param name="chain">Font chain.</param>
    /// <param name="waitForLoad">Wait for the font to be loaded.</param>
    /// <returns>An <see cref="IDisposable"/> that will make it pop the font on dispose.</returns>
    /// <remarks>It will return null on failure, and exception will be stored in <see cref="FailedChains"/>.</remarks>
    public IDisposable? PushFontScoped(in FontChain chain, bool waitForLoad = false) {
        if (this.IsDisposed)
            return null;

        this.fontLock.EnterReadLock();
        try {
            if (this.failedChains.TryGetValue(chain, out _))
                return null;
        } finally {
            this.fontLock.ExitReadLock();
        }

        try {
            var fontTask = this.GetFontTask(chain);
            if (waitForLoad)
                fontTask.Wait();

            if (!fontTask.IsCompletedSuccessfully)
                return null;

            ImGui.PushFont(fontTask.Result.FontPtr);
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
        if (this.suppressTextureUpdateCounter > 0)
            return;

        this.fontLock.EnterReadLock();
        try {
            foreach (var x in this.TextureWraps.OfType<DynamicFontAtlasTextureWrap>())
                x.ApplyChanges();
        } finally {
            this.fontLock.ExitReadLock();
        }
    }

    private async Task<DynamicFont?> GetFallbackFontTask(int sizePx) =>
        this.fallbackFontIdent is not { } f ? null : await this.GetFontTask(f, sizePx);

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

            if (this.failedIdents.TryGetValue((ident, sizePx), out var previousException))
                throw new AggregateException(previousException);

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

                        const string filename = "font{}.tex";

                        var fdtPath = gfs.FamilyAndSize.GetFdtPath();
                        var fdt = this.Cache.Get(fdtPath,
                            () => new FdtReader(this.dataManager.GetFile(fdtPath)!.Data));

                        var numExpectedTex = fdt.Glyphs.Max(x => x.TextureFileIndex) + 1;
                        if (!this.gameFontTextures.TryGetValue(filename, out var textureIndices)
                            || textureIndices.Length < numExpectedTex) {
                            this.UpdateTextures();

                            var newTextureWraps = new IDalamudTextureWrap?[numExpectedTex];
                            var newTextureIndices = new int[numExpectedTex];
                            await using (var errorDispose = new DisposeStack()) {
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

                                    var texPath = $"common/font/font{1 + i}.tex";
                                    var wrap = new CachedDalamudTextureWrap(this.Cache.GetScoped(
                                        texPath,
                                        () => this.textureProvider.GetTexture(this.Cache.Get(
                                            texPath,
                                            () => this.dataManager.GetFile<TexFile>(texPath)
                                                ?? throw new FileNotFoundException()))));

                                    newTextureWraps[i] = errorDispose.Add(wrap);
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

                                this.AddTextureWhileLocked(wrap);
                            }
                        }

                        return new AxisDynamicFont(
                            this,
                            null,
                            gfs,
                            fdt,
                            textureIndices);
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

                        if (!this.FontDataBytes.TryGetValue(path, out var data))
                            this.FontDataBytes[path] = data = File.ReadAllBytes(path);

                        return DirectWriteDynamicFont.FromMemory(
                            this,
                            null,
                            string.Empty,
                            data,
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
                            this.FontDataBytes[name],
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
                    if (result.IsCompletedSuccessfully) {
                        var wrapper = result.Result;
                        this.fontPtrToFont[wrapper.FontIntPtr] = wrapper;
                        this.Fonts.Add(wrapper.FontPtr);
                        unsafe {
                            wrapper.Font.ContainerAtlas = this.AtlasPtr;
                        }

                        return result;
                    } else {
                        return Task.FromException<DynamicFont>(this.failedIdents[(identCopy, sizePx)] =
                            result.Exception!);
                    }
                } finally {
                    this.fontLock.ExitWriteLock();
                }
            }).Unwrap();
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
    /// <returns>Found font wrapper.</returns>
    internal Task<DynamicFont> GetFontTask(in FontChain chain) {
        if (this.IsDisposed)
            throw new ObjectDisposedException(nameof(DynamicFontAtlas));

        if (!(chain.LineHeight > 0))
            throw new ArgumentException("LineHeight must be a positive number", nameof(chain));

        if (!(chain.GlyphRatio >= 0))
            throw new ArgumentException("Ratio must be a positive number or a zero", nameof(chain));

        if (chain.SecondaryFonts.Any(x => x.Ident == default) || chain.PrimaryFont == default)
            throw new ArgumentException("Font chain cannot contain an empty identifier", nameof(chain));

        this.fontLock.EnterUpgradeableReadLock();
        var writeLockEntered = false;
        try {
            if (this.failedChains.TryGetValue(chain, out var previousException))
                throw new AggregateException(previousException);

            this.ProcessGammaChanges();

            var scale = ImGuiHelpers.GlobalScale;
            if (this.fontChains.TryGetValue((chain, scale), out var fontTask))
                return fontTask;

            var chainCopy = chain;
            this.fontLock.EnterWriteLock();
            writeLockEntered = true;
            return this.fontChains[(chain, scale)] = Task.Run(async () =>
            {
                DynamicFont font = new ChainedDynamicFont(
                    this,
                    await this.GetFallbackFontTask((int)MathF.Round(chainCopy.PrimaryFont.SizePx * scale)),
                    chainCopy,
                    await Task.WhenAll(chainCopy.SecondaryFonts
                        .Prepend(chainCopy.PrimaryFont)
                        .Select(entry => this.GetFontTask(entry.Ident, (int)MathF.Round(entry.SizePx * scale)))),
                    scale);

                return font;
            }).ContinueWith(result =>
            {
                this.fontLock.EnterWriteLock();
                try {
                    if (result.IsCompletedSuccessfully) {
                        var font = result.Result;
                        this.fontPtrToFont[font.FontIntPtr] = font;
                        this.Fonts.Add(font.FontPtr);
                        unsafe {
                            font.Font.ContainerAtlas = this.AtlasPtr;
                        }

                        return Task.FromResult(font);
                    } else {
                        return Task.FromException<DynamicFont>(this.failedChains[chainCopy] = result.Exception!);
                    }
                } finally {
                    this.fontLock.ExitWriteLock();
                }
            }).Unwrap();
        } finally {
            if (writeLockEntered)
                this.fontLock.ExitWriteLock();

            this.fontLock.ExitUpgradeableReadLock();
        }
    }

    internal unsafe DynamicFont? GetFontFromPtr(ImFontPtr font) {
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

    private void AddTextureWhileLocked(IDalamudTextureWrap wrap) {
        this.ImTextures.Add(new() { TexID = wrap.ImGuiHandle });
        this.TextureWraps.Add(wrap);
    }

    private unsafe void Clear(bool disposing) {
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
            this.AddTextureWhileLocked(wrap);

            foreach (var t in retainedWraps)
                this.AddTextureWhileLocked(t);

            foreach (var (key, values) in retainedGameFontTextures)
                this.gameFontTextures[key] = values.Select(x => this.TextureWraps.IndexOf(x)).ToArray();
        } finally {
            this.fontLock.ExitWriteLock();
        }
    }

    private unsafe void ReleaseUnmanagedResources() {
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
