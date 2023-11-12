using System.Reactive.Disposables;
using System.Runtime.InteropServices;

namespace DynamicFontAtlasLib.TrueType.CommonStructs;

public static partial class PointerSpan {
    public static IDisposable CreatePointerSpan<T>(this T[] data, out PointerSpan<T> pointerSpan) 
        where T : unmanaged {
        var gchandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        pointerSpan = new(gchandle.AddrOfPinnedObject(), data.Length);
        return Disposable.Create(() => gchandle.Free());
    }

    public static int BinarySearch<T>(this IReadOnlyList<T> span, in T value)
        where T : unmanaged, IComparable<T> {
        var l = 0;
        var r = span.Count - 1;
        while (l <= r) {
            var i = (int)(((uint)r + (uint)l) >> 1);
            var c = value.CompareTo(span[i]);
            switch (c) {
                case 0:
                    return i;
                case > 0:
                    l = i + 1;
                    break;
                default:
                    r = i - 1;
                    break;
            }
        }

        return ~l;
    }
}
