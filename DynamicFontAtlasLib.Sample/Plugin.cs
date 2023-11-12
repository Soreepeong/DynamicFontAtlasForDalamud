using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using OnDemandFontsSample.Windows;

namespace OnDemandFontsSample;

public sealed class Plugin : IDalamudPlugin {
    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] IDataManager dataManager,
        [RequiredVersion("1.0")] ITextureProvider textureProvider,
        [RequiredVersion("1.0")] IGameInteropProvider gameInteropProvider) {
        this.DataManager = dataManager;
        this.TextureProvider = textureProvider;
        this.GameInteropProvider = gameInteropProvider;
        this.PluginInterface = pluginInterface;

        this.MainWindow = new(this);
        this.WindowSystem.AddWindow(MainWindow);

        this.PluginInterface.UiBuilder.Draw += this.DrawUi;
    }

    public DalamudPluginInterface PluginInterface { get; init; }
    public IDataManager DataManager { get; init; }
    public ITextureProvider TextureProvider { get; init; }
    public IGameInteropProvider GameInteropProvider { get; }
    public WindowSystem WindowSystem = new("DynamicFontAtlasLib.Sample");

    private MainWindow MainWindow { get; init; }

    public void Dispose() {
        this.WindowSystem.RemoveAllWindows();
        this.MainWindow.Dispose();
    }

    private void DrawUi() {
        this.MainWindow.IsOpen = true;
        this.WindowSystem.Draw();
    }
}
