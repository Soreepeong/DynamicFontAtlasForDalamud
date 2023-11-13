using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using SharpDX;

namespace DynamicFontAtlasLib.Internal.Utilities;

internal static class MiscellaneousExtensions {
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

    public static Task<T> AsStarted<T>(this Task<T> task) {
        if (task.Status == TaskStatus.Created) {
            try {
                task.Start();
            } catch (InvalidOperationException) {
                // don't care
            }
        }

        return task;
    }

    public static PointerSpan<byte> ToPointerSpan(this DataPointer sdxdp) => new(sdxdp.Pointer, sdxdp.Size);
}
