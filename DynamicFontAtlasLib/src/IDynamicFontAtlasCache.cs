using System;

namespace DynamicFontAtlasLib;

public interface IDynamicFontAtlasCache : IDisposable {
    public T Get<T>(object key, Func<T> initializer);

    public ICacheItemReference<T> GetScoped<T>(object key, Func<T> initializer);

    public interface ICacheItemReference<out T> : IDisposable {
        public T Item { get; }
    }
}
