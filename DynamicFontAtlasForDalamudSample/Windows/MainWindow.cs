using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Unicode;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using DynamicFontAtlasLib;
using DynamicFontAtlasLib.FontIdentificationStructs;
using ImGuiNET;

namespace OnDemandFontsSample.Windows;

public class MainWindow : Window, IDisposable {
    private static readonly string FontAwesomeChars = string.Join(
        "",
        Enum.GetValues<FontAwesomeIcon>().Where(x => x != 0).Select(x => (char)x));

    private readonly List<FontIdent> bundledIdents = Enum.GetValues<BundledFonts>()
        .Where(x => x != BundledFonts.None)
        .Select(FontIdent.From)
        .ToList();

    private readonly List<FontChainEntry> gameEntries = Enum.GetValues<GameFontFamilyAndSize>()
        .Where(x => x != GameFontFamilyAndSize.Undefined)
        .Select(x => new GameFontStyle(x))
        .Select(x => new FontChainEntry(FontIdent.From(x.Family), x.SizePx))
        .ToList();

    private readonly FontChain exampleChain = new(
        new FontChainEntry[] {
            new(FontIdent.FromSystem("Comic Sans MS"), 18f * 4 / 3) {
                Ranges = new UnicodeRange[] {
                    new(0, 'A' - 1),
                    new('Z' + 1, 0xFFFF - ('Z' + 1)),
                },
            },
            new(FontIdent.FromSystem("Papyrus"), 18f * 4 / 3, -2),
            new(FontIdent.FromSystem("Gulim"), 18f * 4 / 3) {
                Ranges = new[] {
                    UnicodeRanges.HangulJamo,
                    UnicodeRanges.HangulSyllables,
                    UnicodeRanges.HangulCompatibilityJamo,
                    UnicodeRanges.HangulJamoExtendedA,
                    UnicodeRanges.HangulJamoExtendedB,
                },
            },
            new(FontIdent.FromSystem("Gungsuh"), 18f * 4 / 3) {
                Ranges = new[] {
                    UnicodeRanges.CjkCompatibility,
                    UnicodeRanges.CjkStrokes,
                    UnicodeRanges.CjkCompatibilityForms,
                    UnicodeRanges.CjkCompatibilityIdeographs,
                    UnicodeRanges.CjkUnifiedIdeographs,
                    UnicodeRanges.CjkUnifiedIdeographsExtensionA,
                },
            },
        });

    private readonly List<FontIdent> systemEntries = new();

    private unsafe ImGuiListClipperPtr entryClipper = new(ImGuiNative.ImGuiListClipper_ImGuiListClipper());

    private string buffer = "ABCDE abcde 12345 가나다 漢字氣気 あかさたな アカサタナ";

    private DynamicFontAtlas? atlas;
    private float fontSize = 14f * 4 / 3;

    public MainWindow(Plugin plugin) : base("DynamicFontAtlas Sample") {
        this.SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new(640, 480),
            MaximumSize = new(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
    }

    private Plugin Plugin { get; }

    public void Dispose() {
        this.atlas?.Dispose();
        this.atlas = null;
    }

    public override void Draw() {
        if (ImGui.Button("New")) {
            this.atlas?.Dispose();

            CustomPixelShaderMonkeyPatcher.Patch();

            this.atlas = new(
                new(((dynamic)this.Plugin.PluginInterface.UiBuilder).Device.NativePointer),
                this.Plugin.PluginInterface.DalamudAssetDirectory,
                this.Plugin.DataManager,
                this.Plugin.TextureProvider) {
                FallbackFontIdent = FontIdent.FromSystem("Gulim"),
            };

            this.systemEntries.Clear();
            this.systemEntries.AddRange(
                FontIdent.GetSystemFonts()
                    .SelectMany(x => x.Variants)
                    .OrderBy(x => x.System!.Value.Name));
        }

        if (this.atlas is null)
            return;

        ImGui.SameLine();
        if (ImGui.Button("Dispose")) {
            this.atlas?.Dispose();
            this.atlas = null;
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear")) {
            this.atlas.Clear();
            return;
        }

        ImGui.DragFloat("Font Size(px)", ref this.fontSize, 1, 6, 64);

        using var dispose1 = this.atlas.SuppressTextureUpdatesScoped();

        using (this.atlas.PushFontScoped(FontIdent.From(GameFontFamily.Axis), 12f * 4 / 3)) {
            this.atlas.LoadGlyphs(this.buffer);
            ImGui.InputTextMultiline(
                "Test Here",
                ref this.buffer,
                65536,
                new(ImGui.GetContentRegionAvail().X, 80));
        }

        using (this.atlas.PushFontScoped(this.exampleChain)) {
            this.atlas.LoadGlyphs(this.buffer);
            ImGui.TextUnformatted(this.buffer);
        }

        if (ImGui.CollapsingHeader("Game fonts")) {
            foreach (var entry in this.gameEntries) {
                using (this.atlas.PushFontScoped(entry.Ident, entry.SizePx)) {
                    var s = $"{entry}: {this.buffer}";
                    this.atlas.LoadGlyphs(s);
                    ImGui.TextUnformatted(s);
                }
            }
        }

        if (ImGui.CollapsingHeader("Square FontAwesome icons")) {
            var squareFontAwesomeChain = new FontChain(
                new FontChainEntry(FontIdent.From(BundledFonts.FontAwesomeFreeSolid), this.fontSize)) {
                GlyphRatio = 1,
                VerticalAlignment = FontChainVerticalAlignment.Middle,
            };

            using (this.atlas.PushFontScoped(squareFontAwesomeChain)) {
                this.atlas.LoadGlyphs(FontAwesomeChars);
                for (var i = 0; i < FontAwesomeChars.Length; i += 64)
                    ImGui.TextUnformatted(FontAwesomeChars[i..Math.Min(i + 64, FontAwesomeChars.Length)]);
            }
        }

        if (ImGui.CollapsingHeader("Dalamud bundled fonts")) {
            foreach (var entry in this.bundledIdents) {
                using (this.atlas.PushFontScoped(entry, this.fontSize)) {
                    var s = $"{entry}: {this.buffer}";
                    this.atlas.LoadGlyphs(s);
                    ImGui.TextUnformatted(s);
                }
            }
        }

        if (ImGui.CollapsingHeader("System fonts")) {
            this.entryClipper.Begin(this.systemEntries.Count, this.fontSize + (ImGui.GetStyle().FramePadding.Y * 2));
            while (this.entryClipper.Step()) {
                for (var i = this.entryClipper.DisplayStart; i < this.entryClipper.DisplayEnd; i++) {
                    if (i < 0)
                        continue;

                    var entry = this.systemEntries[i];
                    using (this.atlas.PushFontScoped(entry, this.fontSize)) {
                        var s = $"{entry}: {this.buffer}";
                        this.atlas.LoadGlyphs(s);

                        ImGui.TextUnformatted(s);
                    }
                }
            }

            this.entryClipper.End();
        }

        if (ImGui.CollapsingHeader("Atlas textures")) {
            var ts = this.atlas.AtlasPtr.Textures;
            foreach (var t in Enumerable.Range(0, ts.Size)) {
                ImGui.Image(
                    ts[t].TexID,
                    new(
                        this.atlas.AtlasPtr.TexWidth,
                        this.atlas.AtlasPtr.TexHeight));
            }
        }

        if (ImGui.CollapsingHeader("Atlas textures (Channel separated)")) {
            var ts = this.atlas.AtlasPtr.Textures;
            foreach (var t in Enumerable.Range(0, ts.Size)) {
                ImGui.Image(
                    ts[t].TexID,
                    new(
                        this.atlas.AtlasPtr.TexWidth,
                        this.atlas.AtlasPtr.TexHeight),
                    new(1, 0),
                    new(2, 1));

                ImGui.Image(
                    ts[t].TexID,
                    new(
                        this.atlas.AtlasPtr.TexWidth,
                        this.atlas.AtlasPtr.TexHeight),
                    new(2, 0),
                    new(3, 1));

                ImGui.Image(
                    ts[t].TexID,
                    new(
                        this.atlas.AtlasPtr.TexWidth,
                        this.atlas.AtlasPtr.TexHeight),
                    new(3, 0),
                    new(4, 1));

                ImGui.Image(
                    ts[t].TexID,
                    new(
                        this.atlas.AtlasPtr.TexWidth,
                        this.atlas.AtlasPtr.TexHeight),
                    new(4, 0),
                    new(5, 1));
            }
        }
    }
}
