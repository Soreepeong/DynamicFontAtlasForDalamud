using System;
using System.Collections;
using System.Collections.Generic;

namespace DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

public struct Bytes256 : IList<byte>, IReadOnlyList<byte>, ICollection {
    public const int FixedCount = 256;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private unsafe fixed byte data[FixedCount];
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

    public unsafe Bytes256(Span<byte> b) {
        fixed (byte* p = data)
            b.CopyTo(new(p, FixedCount));
    }

    public bool IsReadOnly => false;

    public int Count => FixedCount;

    bool ICollection.IsSynchronized => true;

    object ICollection.SyncRoot => this;

    public unsafe byte this[int index] {
        get => this.data[EnsureIndex(index)];
        set => this.data[EnsureIndex(index)] = value;
    }

    public IEnumerator<byte> GetEnumerator() {
        for (var i = 0; i < FixedCount; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public void Add(byte item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public unsafe bool Contains(byte item) {
        for (var i = 0; i < FixedCount; i++)
            if (this.data[i] == item)
                return true;

        return false;
    }

    public unsafe void CopyTo(byte[] array, int arrayIndex) {
        if (array.Length - arrayIndex < FixedCount)
            throw new ArgumentException(null, nameof(arrayIndex));

        for (var i = 0; i < FixedCount; i++)
            array[arrayIndex + i] = this.data[i];
    }

    public unsafe void CopyTo(Array array, int arrayIndex) {
        if (array.Length - arrayIndex < FixedCount)
            throw new ArgumentException(null, nameof(arrayIndex));

        for (var i = 0; i < FixedCount; i++)
            array.SetValue(arrayIndex + i, this.data[i]);
    }

    public bool Remove(byte item) => throw new NotSupportedException();

    public unsafe int IndexOf(byte item) {
        for (var i = 0; i < FixedCount; i++)
            if (this.data[i] == item)
                return i;

        return -1;
    }

    public void Insert(int index, byte item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    private static int EnsureIndex(int index) =>
        index is >= 0 and < FixedCount ? index : throw new IndexOutOfRangeException();
}
