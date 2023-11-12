using System.Buffers.Binary;
using DynamicFontAtlasLib.TrueType.CommonStructs;
using DynamicFontAtlasLib.TrueType.Files;

namespace DynamicFontAtlasLib.TrueType.Tables;

public struct Kern {
    // https://docs.microsoft.com/en-us/typography/opentype/spec/kern
    // https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6kern.html

    public static readonly TagStruct DirectoryTableTag = new('k', 'e', 'r', 'n');

    public PointerSpan<byte> Memory;

    public Kern(SfntFile file) : this(file[DirectoryTableTag]) { }

    public Kern(PointerSpan<byte> memory) => this.Memory = memory;

    public ushort Version => this.Memory.ReadU16Big(0);

    public IEnumerable<KerningPair> EnumerateHorizontalPairs() => this.Version switch {
        0 => new Version0(this.Memory).EnumerateHorizontalPairs(),
        1 => new Version1(this.Memory).EnumerateHorizontalPairs(),
        _ => Array.Empty<KerningPair>(),
    };

    public struct KerningPair {
        public ushort Left;
        public ushort Right;
        public short Value;

        public KerningPair(PointerSpan<byte> span) {
            var offset = 0;
            span.ReadBig(ref offset, out this.Left);
            span.ReadBig(ref offset, out this.Right);
            span.ReadBig(ref offset, out this.Value);
        }

        public static KerningPair ReverseEndianness(KerningPair pair) => new() {
            Left = BinaryPrimitives.ReverseEndianness(pair.Left),
            Right = BinaryPrimitives.ReverseEndianness(pair.Right),
            Value = BinaryPrimitives.ReverseEndianness(pair.Value),
        };
    }

    public struct Format0 {
        public PointerSpan<byte> Memory;

        public Format0(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort PairCount => this.Memory.ReadU16Big(0);
        public ushort SearchRange => this.Memory.ReadU16Big(2);
        public ushort EntrySelector => this.Memory.ReadU16Big(4);
        public ushort RangeShift => this.Memory.ReadU16Big(6);

        public BigEndianPointerSpan<KerningPair> Pairs => new(
            this.Memory[8..].As<KerningPair>(this.PairCount),
            KerningPair.ReverseEndianness);
    }

    public struct Version0 {
        public PointerSpan<byte> Memory;

        public Version0(PointerSpan<byte> memory) => this.Memory = memory;

        public ushort Version => this.Memory.ReadU16Big(0);
        public ushort NumSubtables => this.Memory.ReadU16Big(2);
        public PointerSpan<byte> Data => this.Memory[4..];

        public IEnumerable<Subtable> EnumerateSubtables() {
            var data = this.Data;
            for (var i = 0; i < this.NumSubtables && !data.IsEmpty; i++) {
                var st = new Subtable(data);
                data = data[st.Length..];
                yield return st;
            }
        }

        public IEnumerable<KerningPair> EnumerateHorizontalPairs() {
            var accumulator = new Dictionary<(ushort Left, ushort Right), short>();
            foreach (var subtable in this.EnumerateSubtables()) {
                var isOverride = (subtable.Flags & CoverageFlags.Override) != 0;
                var isMinimum = (subtable.Flags & CoverageFlags.Minimum) != 0;
                foreach (var t in subtable.EnumeratePairs()) {
                    if (isOverride) {
                        accumulator[(t.Left, t.Right)] = t.Value;
                    } else if (isMinimum) {
                        accumulator[(t.Left, t.Right)] = Math.Max(
                            accumulator.GetValueOrDefault((t.Left, t.Right), t.Value),
                            t.Value);
                    } else {
                        accumulator[(t.Left, t.Right)] = (short)(
                            accumulator.GetValueOrDefault((t.Left, t.Right)) + t.Value);
                    }
                }
            }

            return accumulator.Select(x => new KerningPair { Left = x.Key.Left, Right = x.Key.Right, Value = x.Value });
        }

        [Flags]
        public enum CoverageFlags : byte {
            Horizontal = 1 << 0,
            Minimum = 1 << 1,
            CrossStream = 1 << 2,
            Override = 1 << 3,
        }

        public struct Subtable {
            public PointerSpan<byte> Memory;

            public Subtable(PointerSpan<byte> memory) => this.Memory = memory;

            public ushort Version => this.Memory.ReadU16Big(0);
            public ushort Length => this.Memory.ReadU16Big(2);
            public byte Format => this.Memory[4];
            public CoverageFlags Flags => this.Memory.ReadEnumBig<CoverageFlags>(5);
            public PointerSpan<byte> Data => this.Memory[6..];

            public IEnumerable<KerningPair> EnumeratePairs() => this.Format switch {
                0 => new Format0(this.Data).Pairs,
                _ => Array.Empty<KerningPair>(),
            };
        }
    }

    public struct Version1 {
        public PointerSpan<byte> Memory;

        public Version1(PointerSpan<byte> memory) => this.Memory = memory;

        public Fixed Version => new(this.Memory);

        public int NumSubtables => this.Memory.ReadI16Big(4);

        public PointerSpan<byte> Data => this.Memory[8..];

        public IEnumerable<Subtable> EnumerateSubtables() {
            var data = this.Data;
            for (var i = 0; i < this.NumSubtables && !data.IsEmpty; i++) {
                var st = new Subtable(data);
                data = data[st.Length..];
                yield return st;
            }
        }

        public IEnumerable<KerningPair> EnumerateHorizontalPairs() => this
            .EnumerateSubtables()
            .Where(x => x.Flags == 0)
            .SelectMany(x => x.EnumeratePairs());

        [Flags]
        public enum CoverageFlags : byte {
            Vertical = 1 << 0,
            CrossStream = 1 << 1,
            Variation = 1 << 2,
        }

        public struct Subtable {
            public PointerSpan<byte> Memory;

            public Subtable(PointerSpan<byte> memory) => this.Memory = memory;

            public int Length => this.Memory.ReadI32Big(0);
            public byte Format => this.Memory[4];
            public CoverageFlags Flags => this.Memory.ReadEnumBig<CoverageFlags>(5);
            public ushort TupleIndex => this.Memory.ReadU16Big(6);
            public PointerSpan<byte> Data => this.Memory[8..];

            public IEnumerable<KerningPair> EnumeratePairs() => this.Format switch {
                0 => new Format0(this.Data).Pairs,
                _ => Array.Empty<KerningPair>(),
            };
        }
    }
}
