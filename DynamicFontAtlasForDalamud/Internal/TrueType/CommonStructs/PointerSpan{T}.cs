using System;
using System.Collections;
using System.Collections.Generic;
using SharpDX;

namespace DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

public readonly unsafe struct PointerSpan<T> : IList<T>, IReadOnlyList<T>, ICollection
    where T : unmanaged {
    public readonly T* Pointer;

    public PointerSpan(DataPointer dataPointer)
        : this((T*)dataPointer.Pointer, dataPointer.Size / sizeof(T)) { }

    public PointerSpan(T* pointer, int count) {
        this.Pointer = pointer;
        this.Count = count;
    }

    public static PointerSpan<T> Empty => new();

    public Span<T> Span => new(this.Pointer, this.Count);

    public bool IsEmpty => Count == 0;

    public int Count { get; }

    public int Length => this.Count;

    public int NumBytes => sizeof(T) * this.Count;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    bool ICollection<T>.IsReadOnly => false;

    public ref T this[int index] => ref this.Pointer[this.EnsureIndex(index)];

    public PointerSpan<T> this[Range range] => this.Slice(range.GetOffsetAndLength(this.Count));

    T IList<T>.this[int index] {
        get => this.Pointer[this.EnsureIndex(index)];
        set => this.Pointer[this.EnsureIndex(index)] = value;
    }

    T IReadOnlyList<T>.this[int index] => this.Pointer[this.EnsureIndex(index)];

    public PointerSpan<T> Slice(int offset, int count) => new(this.Pointer + offset, count);

    public PointerSpan<T> Slice((int Offset, int Count) offsetAndCount)
        => this.Slice(offsetAndCount.Offset, offsetAndCount.Count);

    public PointerSpan<T2> As<T2>(int maxCount = int.MaxValue)
        where T2 : unmanaged =>
        new((T2*)this.Pointer, Math.Min(maxCount, this.Count / sizeof(T2)));

    public IEnumerator<T> GetEnumerator() {
        for (var i = 0; i < this.Count; i++)
            yield return this[i];
    }

    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    void ICollection<T>.Clear() => throw new NotSupportedException();

    bool ICollection<T>.Contains(T item) {
        for (var i = 0; i < this.Count; i++)
            if (Equals(this.Pointer[i], item))
                return true;

        return false;
    }

    void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
        if (array.Length < this.Count)
            throw new ArgumentException(null, nameof(array));

        if (array.Length < arrayIndex + this.Count)
            throw new ArgumentException(null, nameof(arrayIndex));

        for (var i = 0; i < this.Count; i++)
            array[arrayIndex + i] = this.Pointer[i];
    }

    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    int IList<T>.IndexOf(T item) {
        for (var i = 0; i < this.Count; i++)
            if (Equals(this.Pointer[i], item))
                return i;

        return -1;
    }

    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int arrayIndex) {
        if (array.Length < this.Count)
            throw new ArgumentException(null, nameof(array));

        if (array.Length < arrayIndex + this.Count)
            throw new ArgumentException(null, nameof(arrayIndex));

        for (var i = 0; i < this.Count; i++)
            array.SetValue(this.Pointer[i], arrayIndex + i);
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private int EnsureIndex(int index) =>
        0 <= index && index < this.Count ? index : throw new IndexOutOfRangeException();
}
