using System;
using System.Runtime.CompilerServices;

namespace DynamicFontAtlasLib.Internal.Utilities;

public sealed class DisposableTuple<T> : IDisposable, ITuple
    where T : ITuple {
    public readonly T Tuple;

    public DisposableTuple(T tuple) => this.Tuple = tuple;

    public void Dispose() {
        for (var i = 0; i < this.Tuple.Length; i++)
            (this.Tuple[i] as IDisposable)?.Dispose();
    }

    public object? this[int index] => this.Tuple[index];

    public int Length => this.Tuple.Length;
}

public static class DisposableTuple {
    public static DisposableTuple<T> AsDisposable<T>(this T tuple)
        where T : ITuple => new(tuple);
}
