using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DynamicFontAtlasLib.Internal;

namespace DynamicFontAtlasLib;

public static class DynamicFontAtlasFactory {
    /// <summary>
    /// Creates a new instance of the <see cref="IDynamicFontAtlas"/> interface with the default implementation.
    /// </summary>
    /// <param name="device">An instance ID3D11Device. It can be disposed after a call to constructor.</param>
    /// <param name="dalamudAssetDirectory">Path to Dalamud assets. If invalid, loading any of <see cref="DynamicFontAtlasLib.FontIdentificationStructs.BundledFonts"/> will fail.</param>
    /// <param name="gameFileFetcher">Fetcher callback for game files. If invalid, loading any of <see cref="Dalamud.Interface.GameFonts.GameFontFamilyAndSize"/> will fail.</param>
    /// <param name="cache">Cache.</param>
    /// <returns>A new instance of <see cref="IDynamicFontAtlas"/>.</returns>
    public static IDynamicFontAtlas CreateAtlas(
        SharpDX.Direct3D11.Device device,
        DirectoryInfo dalamudAssetDirectory,
        Func<string, Task<byte[]>> gameFileFetcher,
        IDynamicFontAtlasCache cache) =>
        new DynamicFontAtlas(device, dalamudAssetDirectory, gameFileFetcher, cache);

    /// <summary>
    /// Creates a new instance of the <see cref="IDynamicFontAtlas"/> interface with the default implementation.
    /// </summary>
    /// <param name="pluginInterface">An instance of <see cref="DalamudPluginInterface"/>.</param>
    /// <param name="dataManager">An instance of <see cref="IDataManager"/>.</param>
    /// <param name="cache">An instance of <see cref="IDynamicFontAtlasCache"/>.</param>
    /// <returns>A new instance of <see cref="IDynamicFontAtlas"/>.</returns>
    public static IDynamicFontAtlas CreateAtlas(
        DalamudPluginInterface pluginInterface,
        IDataManager dataManager,
        IDynamicFontAtlasCache cache) =>
        CreateAtlas(
                new(((dynamic)pluginInterface.UiBuilder).Device.NativePointer),
                pluginInterface.DalamudAssetDirectory,
                path => Task.Run(() => dataManager.GetFile(path)?.Data ?? throw new FileNotFoundException()),
                cache);

    /// <summary>
    /// Creates an instance of the default implementation of <see cref="IDynamicFontAtlasCache"/>.
    /// </summary>
    /// <returns>The newly created cache.</returns>
    public static IDynamicFontAtlasCache CreateCache() => new DynamicFontAtlasCache();

    /// <summary>
    /// Bind properties for <paramref name="atlas"/> from Dalamud configuration.
    /// </summary>
    /// <param name="atlas">The atlas.</param>
    /// <returns>The same atlas, but with properties bound.</returns>
    public static IDynamicFontAtlas WithBindToDalamudConfiguration(this IDynamicFontAtlas atlas) {
        var serviceGenericType = Assembly.GetAssembly(typeof(IDalamudTextureWrap))!.DefinedTypes
            .Single(x => x.FullName == "Dalamud.Service`1");

        var interfaceManagerType = Assembly.GetAssembly(typeof(IDalamudTextureWrap))!.DefinedTypes
            .Single(x => x.FullName == "Dalamud.Interface.Internal.InterfaceManager");

        var serviceInterfaceManagerType = serviceGenericType.MakeGenericType(interfaceManagerType);
        var interfaceManager = serviceInterfaceManagerType.GetMethod("Get")!.Invoke(null, null)!;
        var fontGammaProperty = interfaceManagerType
            .GetProperty("FontGamma", BindingFlags.Public | BindingFlags.Instance)!;

        atlas.GammaGetter = () => (float)fontGammaProperty.GetValue(interfaceManager)!;
        atlas.ScaleGetter = () => ImGuiHelpers.GlobalScale;
        atlas.GammaChangeShouldClear = true;
        return atlas;
    }
}
