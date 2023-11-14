using System.Collections.Immutable;
using System.Diagnostics;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Files;
using DynamicFontAtlasLib.TrueType.Tables;

namespace DynamicFontAtlasLib.Testing;

public static class Class1 {
    public static unsafe void Main() {
        // foreach (var path in Directory.GetFiles(@"C:\Windows\Fonts")) {
        //     if (!path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
        //         !path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
        //         continue;
        //
        //     Console.WriteLine($"Testing: {path}");
        //
        //     using var handle = File.ReadAllBytes(path).CreatePointerSpan(out var pointerSpan);
        //     var sfnt = new SfntFile(pointerSpan);

        TestTtf(@"C:\Users\sp\Downloads\YuGothicUI-Regular-02.ttf");

        Debugger.Break();
    }

    private static void TestTtf(string path) {
        using var handle = File.ReadAllBytes(path).CreatePointerSpan(out var pointerSpan);
        TestSfnt(new(pointerSpan));
    }

    private static void TestTtc(string path) {
        using var handle = File.ReadAllBytes(path).CreatePointerSpan(out var pointerSpan);
        foreach (var sfnt in new TtcFile(pointerSpan))
            TestSfnt(sfnt);
    }

    private static void TestSfnt(SfntFile sfnt) {
        var cmap = new Cmap(sfnt);
        if (cmap.UnicodeTable is not { } unicodeTable)
            return;

        var glyphToCodepoints = unicodeTable.GroupBy(x => x.Value, x => x.Key)
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.ToImmutableSortedSet());

        var distGpos = Array.Empty<KerningPair>();
        var distKern = Array.Empty<KerningPair>();
        (char Left, char Right, short Value)[] distGposCodepoints = Array.Empty<(char, char, short)>();
        (char Left, char Right, short Value)[] distKernCodepoints = Array.Empty<(char, char, short)>();

        if (sfnt.ContainsKey(Gpos.DirectoryTableTag)) {
            var gpos = new Gpos(sfnt);
            distGpos = gpos.ExtractAdvanceX().ToArray();
            distGposCodepoints = distGpos
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Left, ImmutableSortedSet<int>.Empty)
                    .Select(lc => (Left: (char)lc, x.Right, x.Value)))
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Right, ImmutableSortedSet<int>.Empty)
                    .Select(rc => (x.Left, Right: (char)rc, x.Value)))
                .ToArray();
        }

        if (sfnt.ContainsKey(Kern.DirectoryTableTag)) {
            var kern = new Kern(sfnt);
            distKern = kern.EnumerateHorizontalPairs().ToArray();
            distKernCodepoints = distKern
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Left, ImmutableSortedSet<int>.Empty)
                    .Select(lc => (Left: (char)lc, x.Right, x.Value)))
                .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Right, ImmutableSortedSet<int>.Empty)
                    .Select(rc => (x.Left, Right: (char)rc, x.Value)))
                .ToArray();
        }

        if (distGpos.Any() && distKern.Any()) {
            Debugger.Break();
        }
    }
}
