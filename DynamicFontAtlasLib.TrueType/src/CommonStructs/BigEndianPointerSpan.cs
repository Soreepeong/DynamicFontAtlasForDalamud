using System.Collections;

namespace DynamicFontAtlasLib.TrueType.CommonStructs;

public readonly unsafe struct BigEndianPointerSpan<T>
    : IList<T>, IReadOnlyList<T>, ICollection
    where T : unmanaged {
    private readonly Func<T, T> reverseEndianness;

    public readonly T* Pointer;

    public BigEndianPointerSpan(PointerSpan<T> pointerSpan, Func<T, T> reverseEndianness) {
        this.reverseEndianness = reverseEndianness;
        this.Pointer = pointerSpan.Pointer;
        this.Count = pointerSpan.Count;
    }

    public int Count { get; }

    public int Length => this.Count;

    public int NumBytes => sizeof(T) * this.Count;

    public bool IsSynchronized => true;

    public object SyncRoot => this;

    public bool IsReadOnly => true;

    public T this[int index] {
        get => BitConverter.IsLittleEndian
            ? this.reverseEndianness(this.Pointer[this.EnsureIndex(index)])
            : this.Pointer[this.EnsureIndex(index)];
        set => this.Pointer[this.EnsureIndex(index)] = BitConverter.IsLittleEndian
            ? this.reverseEndianness(value)
            : value;
    }

    public IEnumerator<T> GetEnumerator() {
        for (var i = 0; i < this.Count; i++)
            yield return this[i];
    }

    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    void ICollection<T>.Clear() => throw new NotSupportedException();

    bool ICollection<T>.Contains(T item) => throw new NotSupportedException();

    void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
        if (array.Length < this.Count)
            throw new ArgumentException(null, nameof(array));

        if (array.Length < arrayIndex + this.Count)
            throw new ArgumentException(null, nameof(arrayIndex));

        for (var i = 0; i < this.Count; i++)
            array[arrayIndex + i] = this[i];
    }

    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    int IList<T>.IndexOf(T item) {
        for (var i = 0; i < this.Count; i++)
            if (Equals(this[i], item))
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
            array.SetValue(this[i], arrayIndex + i);
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private int EnsureIndex(int index) =>
        0 <= index && index < this.Count ? index : throw new IndexOutOfRangeException();
}
