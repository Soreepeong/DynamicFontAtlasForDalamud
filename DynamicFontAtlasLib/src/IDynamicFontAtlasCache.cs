using System;
using System.Threading.Tasks;

namespace DynamicFontAtlasLib;

public interface IDynamicFontAtlasCache : IDisposable {
    public T Get<T>(object key, Func<T> initializer);

    public Task<T> GetAsync<T>(object key, Func<Task<T>> initializer);

    public ICacheItemReference<T> GetScoped<T>(object key, Func<T> initializer);

    public Task<ICacheItemReference<T>> GetScopedAsync<T>(object key, Func<Task<T>> initializer);

    public interface ICacheItemReference<out T> : IDisposable {
        public T Item { get; }
    }
}
