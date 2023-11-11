using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Kern : IEnumerable<(Kern.KerningPair Pair, bool Override)> {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/kern
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6kern.html

    public static readonly TagStruct DirectoryTableTag = new('k', 'e', 'r', 'n');

    public Memory<byte> Data;
    public ushort Version;

    public Kern(Memory<byte> data) {
        this.Data = data;
        Version = data.Span.UInt16At(0);
    }

    public IEnumerator<(KerningPair Pair, bool Override)> GetEnumerator() {
        switch (this.Version) {
            case 0: {
                foreach (var f in new Version0(this.Data))
                    yield return f;

                break;
            }
            case 1: {
                foreach (var f in new Version1(this.Data))
                    yield return (f, false);

                break;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public struct KerningPair {
        public ushort Left;
        public ushort Right;
        public short Value;

        public KerningPair(Span<byte> span) {
            this.Left = BinaryPrimitives.ReadUInt16BigEndian(span);
            this.Right = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            this.Value = BinaryPrimitives.ReadInt16BigEndian(span[4..]);
        }
    }

    public struct Format0 : IEnumerable<KerningPair> {
        public ushort PairCount;
        public Memory<byte> PairsBytes;

        public Format0(Memory<byte> memory) {
            this.PairCount = memory.Span.UInt16At(0);
            this.PairsBytes = memory.Slice(8, Math.Min(8, this.PairCount * Unsafe.SizeOf<KerningPair>()));
            this.PairCount = (ushort)(this.PairsBytes.Length / Unsafe.SizeOf<KerningPair>());
        }

        public IEnumerator<KerningPair> GetEnumerator() {
            var elementSize = Unsafe.SizeOf<KerningPair>();
            for (var i = 0; i < this.PairsBytes.Length; i += elementSize)
                yield return new(this.PairsBytes.Span[i..]);
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public struct Version0 : IEnumerable<(KerningPair Pair, bool Override)> {
        public ushort NumSubtables;
        public Memory<byte> Data;

        public Version0(Memory<byte> memory) {
            var span = memory.Span;
            this.NumSubtables = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
            this.Data = memory[4..];
        }

        public IEnumerator<(KerningPair Pair, bool Override)> GetEnumerator() {
            var data = this.Data;
            for (var i = 0; i < this.NumSubtables && !data.IsEmpty; i++) {
                var st = new Subtable(data);
                foreach (var p in st)
                    yield return p;

                data = data[st.Length..];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        [Flags]
        public enum CoverageFlags : byte {
            Horizontal = 1 << 0,
            Minimum = 1 << 1,
            CrossStream = 1 << 2,
            Override = 1 << 3,
        }

        public struct Subtable : IEnumerable<(KerningPair Pair, bool Override)> {
            public ushort Length;
            public CoverageFlags Flags;
            public byte Format;
            public Memory<byte> Data;

            public Subtable(Memory<byte> memory) {
                var span = memory.Span;
                this.Length = BinaryPrimitives.ReadUInt16BigEndian(span[2..]);
                this.Flags = (CoverageFlags)span[4];
                this.Format = span[5];
                this.Data = memory.Slice(6, Math.Min(memory.Length - 6, this.Length - 6));
            }

            public IEnumerator<(KerningPair Pair, bool Override)> GetEnumerator() {
                var @override = (this.Flags & CoverageFlags.Override) != 0;
                switch (Format) {
                    case 0 when (this.Flags & CoverageFlags.Horizontal) != 0: {
                        foreach (var p in new Format0(Data))
                            yield return (p, @override);

                        break;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }

    public struct Version1 : IEnumerable<KerningPair> {
        public uint NumSubtables;
        public Memory<byte> Data;

        public Version1(Memory<byte> memory) {
            var span = memory.Span;
            this.NumSubtables = BinaryPrimitives.ReadUInt32BigEndian(span);
            this.Data = memory[4..];
        }

        public IEnumerator<KerningPair> GetEnumerator() {
            var data = this.Data;
            for (var i = 0; i < this.NumSubtables && !data.IsEmpty; i++) {
                var st = new Subtable(data);
                foreach (var p in st)
                    yield return p;

                data = data[st.Length..];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        [Flags]
        public enum CoverageFlags : byte {
            Vertical = 1 << 0,
            CrossStream = 1 << 1,
            Variation = 1 << 2,
        }

        public struct Subtable : IEnumerable<KerningPair> {
            public int Length;
            public CoverageFlags Flags;
            public byte Format;
            public ushort TupleIndex;
            public Memory<byte> Data;

            public Subtable(Memory<byte> memory) {
                var span = memory.Span;
                this.Length = BinaryPrimitives.ReadInt32BigEndian(span);
                this.Flags = (CoverageFlags)span[4];
                this.Format = span[5];
                this.TupleIndex = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
                this.Data = memory.Slice(6, Math.Min(memory.Length - 8, this.Length - 8));
            }

            public IEnumerator<KerningPair> GetEnumerator() {
                if (this.TupleIndex != 0)
                    yield break;

                switch (Format) {
                    case 0 when (this.Flags & CoverageFlags.Vertical) == 0: {
                        foreach (var p in new Format0(Data))
                            yield return p;

                        break;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}
