using System;
using System.Threading.Tasks;

namespace DynamicFontAtlasLib;

/// <summary>
/// A cache storage for <see cref="IDynamicFontAtlas"/>.
/// </summary>
public interface IDynamicFontAtlasCache : IDisposable {
    /// <summary>
    /// Gets an item of type <typeparamref name="T"/> and with key <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="initializer">Initializer function</param>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>The object.</returns>
    /// <remarks>Both <typeparamref name="T"/> and <paramref name="key"/> make a unique entry.</remarks>
    public T Get<T>(object key, Func<T> initializer);

    /// <inheritdoc cref="Get{T}"/>
    public Task<T> GetAsync<T>(object key, Func<Task<T>> initializer);

    /// <summary>
    /// Gets an item of type <typeparamref name="T"/> and with key <paramref name="key"/>, in a reference counted manner.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="initializer">Initializer function</param>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>Reference counting wrapper for the object.</returns>
    /// <remarks>Both <typeparamref name="T"/> and <paramref name="key"/> make a unique entry.</remarks>
    public ICacheItemReference<T> GetScoped<T>(object key, Func<T> initializer);

    /// <inheritdoc cref="GetScoped{T}"/>
    public Task<ICacheItemReference<T>> GetScopedAsync<T>(object key, Func<Task<T>> initializer);

    /// <summary>
    /// Reference counter for cache items.
    /// Note that disposing will not dispose the item; it will only decrease the reference count.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    public interface ICacheItemReference<out T> : IDisposable {
        /// <summary>
        /// Gets the item.
        /// </summary>
        public T Item { get; }
    }
}
