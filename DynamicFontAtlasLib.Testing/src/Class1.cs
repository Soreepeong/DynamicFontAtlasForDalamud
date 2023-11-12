using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using DynamicFontAtlasLib.TrueType;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Files;
using DynamicFontAtlasLib.TrueType.Tables;

namespace DynamicFontAtlasLib.Testing;

public static class Class1 {
    public static unsafe void Main() {
        foreach (var path in Directory.GetFiles(@"C:\Windows\Fonts")) {
            if (!path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                continue;

            Console.WriteLine($"Testing: {path}");

            using var handle = File.ReadAllBytes(path).CreatePointerSpan(out var pointerSpan);
            var sfnt = new SfntFile(pointerSpan);

            var cmap = new Cmap(sfnt);
            if (cmap.UnicodeTable is not { } unicodeTable)
                continue;

            var glyphToCodepoints = unicodeTable.GroupBy(x => x.Value, x => x.Key)
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.ToImmutableSortedSet());

            if (!sfnt.ContainsKey(Kern.DirectoryTableTag))
                continue;
            
            if (sfnt.ContainsKey(Kern.DirectoryTableTag) && false) {
                var kern = new Kern(sfnt);
                var glyphPairs = kern.EnumerateHorizontalPairs().ToArray();
                var nonexistentGlyphsInKern = glyphPairs
                    .Select(x => x.Left)
                    .Concat(glyphPairs.Select(x => x.Right))
                    .Except(glyphToCodepoints.Keys)
                    .ToArray();

                if (nonexistentGlyphsInKern.Any())
                    Console.WriteLine($"\tNonexistent glyphs in kern: {string.Join(", ", nonexistentGlyphsInKern)}");

                var codepointPairs = glyphPairs
                    .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Left, ImmutableSortedSet<int>.Empty)
                        .Select(lc => (Left: (char)lc, x.Right, x.Value)))
                    .SelectMany(x => glyphToCodepoints.GetValueOrDefault(x.Right, ImmutableSortedSet<int>.Empty)
                        .Select(rc => (x.Left, Right: (char)rc, x.Value)))
                    .ToArray();
            }
        }

        Debugger.Break();
    }
}
