using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicFontAtlasLib.Internal.Utilities;

namespace DynamicFontAtlasLib.Internal;

internal sealed class DynamicFontAtlasCache : IDynamicFontAtlasCache {
    private readonly ReaderWriterLockSlim lookupLock = new();
    private readonly Dictionary<Type, Dictionary<object, ItemHolder>> items = new();

    public void Dispose() {
        this.items.Values.SelectMany(x => x.Values).DisposeItems();
        this.items.Clear();
    }

    public T Get<T>(object key, Func<T> initializer) {
        var r = this.GetAndAddRef(key, initializer);
        try {
            return (T)r.Value.Result!;
        } finally {
            r.DecRef();
        }
    }

    public async Task<T> GetAsync<T>(object key, Func<Task<T>> initializer) {
        var r = await this.GetAndAddRefAsync(key, initializer);
        try {
            return (T)(await r.Value)!;
        } finally {
            r.DecRef();
        }
    }

    public IDynamicFontAtlasCache.ICacheItemReference<T> GetScoped<T>(object key, Func<T> initializer) {
        var r = this.GetAndAddRef(key, initializer);
        try {
            return r.NewRefWithoutAddRef<T>();
        } catch {
            r.DecRef();
            throw;
        }
    }

    public async Task<IDynamicFontAtlasCache.ICacheItemReference<T>> GetScopedAsync<T>(
        object key,
        Func<Task<T>> initializer) {
        var r = await this.GetAndAddRefAsync(key, initializer);
        try {
            return r.NewRefWithoutAddRef<T>();
        } catch {
            r.DecRef();
            throw;
        }
    }

    private ItemHolder GetAndAddRef<T>(object key, Func<T> initializer) {
        this.lookupLock.EnterUpgradeableReadLock();
        var shouldExitUpgradableReadLock = true;
        try {
            if (!this.items.TryGetValue(typeof(T), out var cached))
                this.items[typeof(T)] = cached = new();

            if (cached.TryGetValue(key, out var holder)) {
                lock (holder.ValueLock) {
                    if (!holder.IsDisposed) {
                        holder.AddRef();
                        return holder;
                    }
                }
            }

            this.lookupLock.EnterWriteLock();
            cached[key] = holder = new();

            lock (holder.ValueLock) {
                this.lookupLock.ExitWriteLock();
                this.lookupLock.ExitUpgradeableReadLock();
                shouldExitUpgradableReadLock = false;

                try {
                    holder.Value = Task.FromResult<object?>(initializer());
                    return holder;
                } catch {
                    this.lookupLock.EnterWriteLock();
                    cached.Remove(key);
                    this.lookupLock.ExitWriteLock();
                    throw;
                }
            }
        } finally {
            if (shouldExitUpgradableReadLock)
                this.lookupLock.ExitUpgradeableReadLock();
        }
    }

    private Task<ItemHolder> GetAndAddRefAsync<T>(object key, Func<Task<T>> initializer) {
        this.lookupLock.EnterUpgradeableReadLock();
        var shouldExitUpgradableReadLock = true;
        try {
            if (!this.items.TryGetValue(typeof(T), out var cached))
                this.items[typeof(T)] = cached = new();

            if (cached.TryGetValue(key, out var holder)) {
                lock (holder.ValueLock) {
                    if (!holder.IsDisposed) {
                        holder.AddRef();
                        return Task.FromResult(holder);
                    }
                }
            }

            this.lookupLock.EnterWriteLock();
            cached[key] = holder = new();

            lock (holder.ValueLock) {
                this.lookupLock.ExitWriteLock();
                this.lookupLock.ExitUpgradeableReadLock();
                shouldExitUpgradableReadLock = false;

                try {
                    holder.Value = initializer().ContinueWith(r => (object?)r.Result);
                    return Task.FromResult(holder);
                } catch {
                    this.lookupLock.EnterWriteLock();
                    cached.Remove(key);
                    this.lookupLock.ExitWriteLock();
                    throw;
                }
            }
        } finally {
            if (shouldExitUpgradableReadLock)
                this.lookupLock.ExitUpgradeableReadLock();
        }
    }

    private class ItemHolder : IDisposable {
        public object ValueLock { get; } = new();

        public bool IsDisposed => this.RefCount < 0;

        public Task<object?> Value { get; set; } = Task.FromResult<object?>(null);

        public int RefCount { get; private set; } = 1;

        public void Dispose() {
            lock (this.ValueLock) {
                if (this.RefCount > 0)
                    throw new InvalidOperationException("Cannot dispose when RefCount != 0");

                this.Value.ContinueWith(r => (r as IDisposable).Dispose());
                this.Value = null!;
                this.RefCount = -1;
            }
        }

        public IDynamicFontAtlasCache.ICacheItemReference<T> NewRefWithoutAddRef<T>() {
            if (this.Value.Result is not T) {
                if (this.Value is not null)
                    throw new InvalidCastException($"Requested {typeof(T)}, contained {this.Value.Result?.GetType()}");

                if (this.IsDisposed)
                    throw new ObjectDisposedException(nameof(ItemHolder));
            }

            return new Reference<T>(this);
        }

        public void AddRef() {
            lock (this.ValueLock) {
                if (this.IsDisposed)
                    throw new ObjectDisposedException(nameof(ItemHolder));

                ++this.RefCount;
            }
        }

        public void DecRef() {
            lock (this.ValueLock) {
                if (this.IsDisposed)
                    throw new ObjectDisposedException(nameof(ItemHolder));

                --this.RefCount;
            }
        }

        private sealed class Reference<T> : IDynamicFontAtlasCache.ICacheItemReference<T> {
            private ItemHolder? holder;

            public Reference(ItemHolder holder) => this.holder = holder;

            ~Reference() => this.holder?.DecRef();

            public T Item =>
                (T)(this.holder
                    ?? throw new ObjectDisposedException(nameof(IDynamicFontAtlasCache.ICacheItemReference<T>)))
                .Value.Result!;

            public void Dispose() {
                this.holder?.DecRef();
                this.holder = null;
                GC.SuppressFinalize(this);
            }
        }
    }
}
