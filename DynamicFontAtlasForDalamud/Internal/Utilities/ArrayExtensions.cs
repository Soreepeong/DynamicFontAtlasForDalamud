using System;
using System.Collections.Generic;

namespace DynamicFontAtlasLib.Internal.Utilities;

internal static class ArrayExtensions {
    /// <summary>
    /// Dispose all items in the given enumerable.
    /// </summary>
    /// <param name="array">The enumerable.</param>
    /// <typeparam name="T">Disposable type.</typeparam>
    public static void DisposeItems<T>(this IEnumerable<T?> array)
        where T : IDisposable {
        List<Exception>? excs = null;
        foreach (var x in array) {
            try {
                x?.Dispose();
            } catch (Exception ex) {
                (excs ??= new()).Add(ex);
            }
        }

        if (excs is not null)
            throw excs.Count == 1 ? excs[0] : new AggregateException(excs);
    }
}
