using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        this.lookupLock.EnterUpgradeableReadLock();
        var shouldExitUpgradableReadLock = true;
        try {
            if (!this.items.TryGetValue(typeof(T), out var cached))
                this.items[typeof(T)] = cached = new();

            if (cached.TryGetValue(key, out var holder)) {
                lock (holder.ValueLock) {
                    if (!holder.IsDisposed)
                        return (T)holder.Value!;
                }
            }

            this.lookupLock.EnterWriteLock();
            cached[key] = holder = new();

            lock (holder.ValueLock) {
                this.lookupLock.ExitWriteLock();
                this.lookupLock.ExitUpgradeableReadLock();
                shouldExitUpgradableReadLock = false;

                try {
                    var v = initializer();
                    holder.Value = v;
                    return v;
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

    public IDynamicFontAtlasCache.ICacheItemReference<T> GetScoped<T>(object key, Func<T> initializer) {
        this.lookupLock.EnterUpgradeableReadLock();
        var shouldExitUpgradableReadLock = true;
        try {
            if (!this.items.TryGetValue(typeof(T), out var cached))
                this.items[typeof(T)] = cached = new();

            if (cached.TryGetValue(key, out var holder)) {
                lock (holder.ValueLock) {
                    if (!holder.IsDisposed)
                        return holder.AddRefWhileLocked<T>();
                }
            }

            this.lookupLock.EnterWriteLock();
            cached[key] = holder = new();

            lock (holder.ValueLock) {
                this.lookupLock.ExitWriteLock();
                this.lookupLock.ExitUpgradeableReadLock();
                shouldExitUpgradableReadLock = false;

                try {
                    holder.Value = initializer();
                    return holder.AddRefWhileLocked<T>();
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

        public object? Value { get; set; }

        public int RefCount { get; set; }

        public void Dispose() {
            IDisposable? value;
            lock (this.ValueLock) {
                if (this.RefCount > 0)
                    throw new InvalidOperationException("Cannot dispose when RefCount != 0");

                value = this.Value as IDisposable;
                this.Value = null!;
                this.RefCount = -1;
            }

            value?.Dispose();
        }

        public IDynamicFontAtlasCache.ICacheItemReference<T> AddRefWhileLocked<T>() {
            if (this.Value is not T) {
                if (this.Value is not null)
                    throw new InvalidCastException($"Requested {typeof(T)}, contained {this.Value?.GetType()}");

                if (this.IsDisposed)
                    throw new ObjectDisposedException(nameof(ItemHolder));
            }

            this.RefCount++;
            return new Reference<T>(this);
        }

        private void DecRef() {
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
                .Value!;

            public void Dispose() {
                this.holder?.DecRef();
                this.holder = null;
                GC.SuppressFinalize(this);
            }
        }
    }
}
