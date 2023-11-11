using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using DynamicFontAtlasLib.Internal.TrueType.CommonStructs;

namespace DynamicFontAtlasLib.Internal.TrueType.Tables;

public struct Kern : IEnumerable<(Kern.KerningPair Pair, bool Override)> {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/kern
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6kern.html

    public static readonly TagStruct DirectoryTableTag = new('k', 'e', 'r', 'n');

    public PointerSpan<byte> Memory;

    public Kern(PointerSpan<byte> memory) {
        this.Memory = memory;
    }

    public ushort Version => this.Memory.ReadU16BE(0);

    public IEnumerator<(KerningPair Pair, bool Override)> GetEnumerator() {
        switch (this.Version) {
            case 0: {
                foreach (var f in new Version0(this.Memory))
                    yield return f;

                break;
            }
            case 1: {
                foreach (var f in new Version1(this.Memory))
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

        public KerningPair(PointerSpan<byte> span) {
            var offset = 0;
            span.ReadBE(ref offset, out this.Left);
            span.ReadBE(ref offset, out this.Right);
            span.ReadBE(ref offset, out this.Value);
        }

        public static KerningPair ReverseEndianness(KerningPair pair) => new() {
            Left = BinaryPrimitives.ReverseEndianness(pair.Left),
            Right = BinaryPrimitives.ReverseEndianness(pair.Right),
            Value = BinaryPrimitives.ReverseEndianness(pair.Value),
        };
    }

    public struct Format0 : IEnumerable<KerningPair> {
        public PointerSpan<byte> Memory;

        public Format0(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort PairCount => this.Memory.ReadU16BE(0);
        public ushort SearchRange => this.Memory.ReadU16BE(2);
        public ushort EntrySelector => this.Memory.ReadU16BE(4);
        public ushort RangeShift => this.Memory.ReadU16BE(6);

        public BigEndianPointerSpan<KerningPair> Pairs => new(
            this.Memory[8..].As<KerningPair>(this.PairCount),
            KerningPair.ReverseEndianness);

        public IEnumerator<KerningPair> GetEnumerator() => this.Pairs.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public struct Version0 : IEnumerable<(KerningPair Pair, bool Override)> {
        public PointerSpan<byte> Memory;

        public Version0(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Version => this.Memory.ReadU16BE(0);
        public ushort NumSubtables => this.Memory.ReadU16BE(2);
        public PointerSpan<byte> Data => this.Memory[4..];

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
            public PointerSpan<byte> Memory;

            public Subtable(PointerSpan<byte> memory) => this.Memory = memory;

            public ushort Version => this.Memory.ReadU16BE(0);
            public ushort Length => this.Memory.ReadU16BE(2);
            public CoverageFlags Flags => this.Memory.ReadEnumBE<CoverageFlags>(4);
            public byte Format => this.Memory[5];
            public PointerSpan<byte> Data => this.Memory[6..];

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
        public PointerSpan<byte> Memory;

        public Version1(PointerSpan<byte> memory) => this.Memory = memory;

        public int Version => this.Memory.ReadI32BE(0);
        public int NumSubtables => this.Memory.ReadI16BE(4);
        public PointerSpan<byte> Data => this.Memory[8..];

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
            public PointerSpan<byte> Memory;

            public Subtable(PointerSpan<byte> memory) => this.Memory = memory;
            
            public int Length => this.Memory.ReadI32BE(0);
            public CoverageFlags Flags => this.Memory.ReadEnumBE<CoverageFlags>(4);
            public byte Format => this.Memory[5];
            public ushort TupleIndex => this.Memory.ReadU16BE(6);
            public PointerSpan<byte> Data => this.Memory[8..];

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
