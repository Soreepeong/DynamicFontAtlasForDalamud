// using System;
// using System.Buffers.Binary;
// using System.Collections.Generic;
// using System.Diagnostics.Contracts;
//
// namespace DynamicFontAtlasLib.Internal.TrueType.Tables; 
//
// public struct Gpos {
// 		// https://docs.microsoft.com/en-us/typography/opentype/spec/gpos
//
//     public static readonly TagStruct DirectoryTableTag = new('G', 'P', 'O', 'S');
//
//     public Memory<byte> Memory;
//     public Fixed Version;
//     public ushort ScriptListOffset;
//     public ushort FeatureListOffset;
//     public ushort LookupOffsetListOffset;
//
//     public uint FeatureVariationsOffset;
//
//     public Gpos(Memory<byte> memory) {
//         var span = memory.Span;
//         this.Version = new(span);
//         this.ScriptListOffset = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
//         this.FeatureListOffset = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
//         this.LookupOffsetListOffset = BinaryPrimitives.ReadUInt16BigEndian(span[8..]);
//         this.FeatureVariationsOffset = BinaryPrimitives.ReadUInt16BigEndian(span[10..]);
//     }
//
//
//     public enum ValueFormatFlags : ushort{
// 	    PlacementX = 1 << 0,
// 	    PlacementY = 1 << 1,
// 	    AdvanceX = 1 << 2,
// 	    AdvanceY = 1 << 3,
// 	    PlaDeviceOffsetX = 1 << 4,
// 	    PlaDeviceOffsetY = 1 << 5,
// 	    AdvDeviceOffsetX = 1 << 6,
// 	    AdvDeviceOffsetY = 1 << 7,
//     }
// 		
// 		
// public struct PairAdjustmentPositioningSubtableFormat1 {
//     public Memory<byte> Memory;
//     
//     public ushort FormatId;
//     public ushort CoverageOffset;
//     public ValueFormatFlags ValueFormat1;
//     public ValueFormatFlags ValueFormat2;
//     public ushort PairSetCount;
//     
//     public PairAdjustmentPositioningSubtableFormat1(Memory<byte> memory) {
//         var span = memory.Span;
//         this.FormatId = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
//         this.CoverageOffset = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
//         this.ValueFormat1 = (ValueFormatFlags) BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
//         this.ValueFormat2 = (ValueFormatFlags)BinaryPrimitives.ReadUInt16BigEndian(span[8..]);
//         this.PairSetCount =  BinaryPrimitives.ReadUInt16BigEndian(span[10..]);
//     }
//
//
//     public struct PairSet {
//         public ushort Count;
//         public ushort Records[1];
//
//         public class View {
// 						union {
// 							const PairSet* m_obj;
// 							const char* m_bytes;
// 						}
//                         public size_t m_length;
//                         public ValueFormatFlags m_format1;
//                         public ValueFormatFlags m_format2;
//                         public uint m_bit;
//                         public size_t m_valueCountPerPairValueRecord;
//
// 					public:
//                     public View() : m_obj(nullptr),
//                     public m_length(0), m_format1{ 0 }, m_format2{ 0 },
//                     public m_bit(0),
//                     public m_valueCountPerPairValueRecord(0) {}
//                     public View(std.nullptr_t) : View() {}
//                     public View(decltype
//                     public (m_obj) pObject, size_t length,
//                     public ValueFormatFlags format1, ValueFormatFlags format2,
//                     public uint bit, size_t valueCountPerPairValueRecord)
// 							:
//                     public m_obj(pObject),
//                     public m_length(length),
//                     public m_format1(format1),
//                     public m_format2(format2),
//                     public m_bit(bit),
//                     public m_valueCountPerPairValueRecord(valueCountPerPairValueRecord) {}
//                     public View(View&&) = default;
//                     public View(const View&) = default;
// 						View& operator=(View&&) = default;
// 						View& operator=(const View&) = default;
// 						View& operator=(std.nullptr_t) { m_obj = nullptr; m_length = 0; return *this; }
//                         public View(
//                         public const void* pData, size_t length,
//                         public ValueFormatFlags format1, ValueFormatFlags format2) :
//                         public View(std.span(reinterpret_cast<const char*>(pData), length), format1, format2) {}
//                         public template<typename T>
//                         public View(std.span<T> data, ValueFormatFlags format1, ValueFormatFlags format2) : View() {
// 							if (data.size_bytes() < 2)
// 								return;
//
// 							var obj = reinterpret_cast<decltype(m_obj)>(&data[0]);
//
// 							var bit = (format2.Value << 16) | format1.Value;
// 							var valueCountPerPairValueRecord = static_cast<size_t>(1) + std.popcount<uint>(bit);
//
// 							if (data.size_bytes() < static_cast<size_t>(2) + 2 * valueCountPerPairValueRecord * (*obj->Count))
// 								return;
//
// 							m_obj = obj;
// 							m_length = data.size_bytes();
// 							m_format1 = format1;
// 							m_format2 = format2;
// 							m_bit = bit;
// 							m_valueCountPerPairValueRecord = valueCountPerPairValueRecord;
// 						}
//
// 						operator bool() {
// 							return !!m_obj;
// 						}
//
//                         public decltype(m_obj) operator*() {
// 							return m_obj;
// 						}
//
//                         public decltype(m_obj) operator->() {
// 							return m_obj;
// 						}
//
// 						[Pure]
//                         public const ushort* GetPairValueRecord(size_t index) {
// 							return &Records[m_valueCountPerPairValueRecord * index];
// 						}
//
// 						[Pure]
//                         public ushort GetSecondGlyph(size_t index) {
// 							return **GetPairValueRecord(index);
// 						}
//
// 						[Pure]
//                         public ushort GetValueRecord1(size_t index, ValueFormatFlags desiredRecord) {
// 							if (!(m_format1.Value & desiredRecord.Value))
// 								return 0;
// 							var bit = m_bit;
// 							var pRecord = GetPairValueRecord(index);
// 							for (var i = static_cast<uint>(desiredRecord.Value); i && bit; i >>= 1, bit >>= 1) {
// 								if (bit & 1)
// 									pRecord++;
// 							}
// 							return *pRecord;
// 						}
//
// 						[Pure]
//                         public ushort GetValueRecord2(size_t index, ValueFormatFlags desiredRecord) {
// 							if (!(m_format2.Value & desiredRecord.Value))
// 								return 0;
// 							var bit = m_bit;
// 							var pRecord = GetPairValueRecord(index);
// 							for (var i = static_cast<uint>(desiredRecord.Value) << 16; i && bit; i >>= 1, bit >>= 1) {
// 								if (bit & 1)
// 									pRecord++;
// 							}
// 							return *pRecord;
// 						}
// 					}
// 				}
//
//     public FormatHeader Header;
//     public ushort PairSetOffsets[1];
//
//     public class View {
// 					union {
// 						const Format1* m_obj;
// 						const char* m_bytes;
// 					}
//                     public size_t m_length;
//
// 				public:
//                 public View() : m_obj(nullptr),
//                 public m_length(0) {}
//                 public View(std.nullptr_t) : View() {}
//                 public View(decltype
//                 public (m_obj) pObject, size_t length)
// 						:
//                 public m_obj(pObject),
//                 public m_length(length) {}
//                 public View(View&&) = default;
//                 public View(const View&) = default;
// 					View& operator=(View&&) = default;
// 					View& operator=(const View&) = default;
// 					View& operator=(std.nullptr_t) { m_obj = nullptr; m_length = 0; return *this; }
//                     public View(
//                     public const void* pData, size_t length) :
//                     public View(std.span(reinterpret_cast<const char*>(pData), length)) {}
//                     public template<typename T>
//                     public View(std.span<T> data) : View() {
// 						if (data.size_bytes() < sizeof FormatHeader)
// 							return;
//
// 						var obj = reinterpret_cast<decltype(m_obj)>(&data[0]);
//
// 						if (obj->Header.FormatId != 1)
// 							return;
//
// 						if (data.size_bytes() < sizeof FormatHeader + static_cast<size_t>(2) * (*obj->Header.PairSetCount))
// 							return;
//
// 						if (CoverageTable.View coverageTable(reinterpret_cast<const char*>(obj) + *obj->Header.CoverageOffset, data.size_bytes() - *obj->Header.CoverageOffset); !coverageTable)
// 							return;
//
// 						for (size_t i = 0, i_ = *obj->Header.PairSetCount; i < i_; i++) {
// 							var off = static_cast<size_t>(*obj->PairSetOffsets[i]);
// 							if (data.size_bytes() < off + 2)
// 								return;
//
// 							var pPairSet = reinterpret_cast<const PairSet*>(reinterpret_cast<const char*>(obj) + off);
// 							if (data.size_bytes() < off + 2 + static_cast<size_t>(2) * (*pPairSet->Count))
// 								return;
// 						}
//
// 						m_obj = obj;
// 						m_length = data.size_bytes();
// 					}
//
// 					operator bool() {
// 						return !!m_obj;
// 					}
//
//                     public decltype(m_obj) operator*() {
// 						return m_obj;
// 					}
//
//                     public decltype(m_obj) operator->() {
// 						return m_obj;
// 					}
//
// 					[Pure] std.span<const ushort>
//                     public PairSetOffsetSpan() {
// 						return { PairSetOffsets, Header.PairSetCount }
// 					}
//
// 					[Pure]
//                     public PairSet.View PairSetView(size_t index) {
// 						var offset = static_cast<size_t>(*PairSetOffsets[index]);
// 						return { reinterpret_cast<const char*>(m_obj) + offset, m_length - offset, Header.ValueFormat1, Header.ValueFormat2 }
// 					}
//
// 					[Pure]
//                     public CoverageTable.View CoverageTableView() {
// 						var offset = static_cast<size_t>(*Header.CoverageOffset);
// 						return { reinterpret_cast<const char*>(m_obj) + offset, m_length - offset }
// 					}
// 				}
// 			}
//
// public struct PairAdjustmentPositioningSubtableFormat2 {
//     public struct FormatHeader {
//         public ushort FormatId;
//         public ushort CoverageOffset;
//         public BE<ValueFormatFlags> ValueFormat1;
//         public BE<ValueFormatFlags> ValueFormat2;
//         public ushort ClassDef1Offset;
//         public ushort ClassDef2Offset;
//         public ushort Class1Count;
//         public ushort Class2Count;
// 				}
//
// 				// Note:
// 				// ClassRecord1 { Class2Record[Class2Count]; }
// 				// ClassRecord2 { ValueFormat1; ValueFormat2; }
//
//                 public FormatHeader Header;
//                 public ushort Records[1];
//
//                 public class View {
// 					union {
// 						const Format2* m_obj;
// 						const char* m_bytes;
// 					}
//                     public size_t m_length;
//                     public uint m_bit;
//                     public size_t m_valueCountPerPairValueRecord;
//
// 				public:
//                 public View() : m_obj(nullptr),
//                 public m_length(0),
//                 public m_bit(0),
//                 public m_valueCountPerPairValueRecord(0) {}
//                 public View(std.nullptr_t) : View() {}
//                 public View(decltype
//                 public (m_obj) pObject, size_t length,
//                 public uint bit, size_t valueCountPerPairValueRecord)
// 						:
//                 public m_obj(pObject),
//                 public m_length(length),
//                 public m_bit(bit),
//                 public m_valueCountPerPairValueRecord(valueCountPerPairValueRecord) {}
//                 public View(View&&) = default;
//                 public View(const View&) = default;
// 					View& operator=(View&&) = default;
// 					View& operator=(const View&) = default;
// 					View& operator=(std.nullptr_t) { m_obj = nullptr; m_length = 0; return *this; }
//                     public View(
//                     public const void* pData, size_t length) :
//                     public View(std.span(reinterpret_cast<const char*>(pData), length)) {}
//                     public template<typename T>
//                     public View(std.span<T> data) : View() {
// 						if (data.size_bytes() < 2)
// 							return;
//
// 						var obj = reinterpret_cast<decltype(m_obj)>(&data[0]);
//
// 						var bit = ((*obj->Header.ValueFormat2).Value << 16) | (*obj->Header.ValueFormat1).Value;
// 						var valueCountPerPairValueRecord = static_cast<size_t>(1 + std.popcount<uint>(bit));
//
// 						if (data.size_bytes() < sizeof FormatHeader + sizeof ushort *valueCountPerPairValueRecord * (*obj->Header.Class1Count) * (*obj->Header.Class2Count))
// 							return;
//
// 						if (ClassDefTable.View v(reinterpret_cast<const char*>(obj) + *obj->Header.ClassDef1Offset, data.size_bytes() - *obj->Header.ClassDef1Offset); !v)
// 							return;
// 						if (ClassDefTable.View v(reinterpret_cast<const char*>(obj) + *obj->Header.ClassDef2Offset, data.size_bytes() - *obj->Header.ClassDef2Offset); !v)
// 							return;
//
// 						m_obj = obj;
// 						m_length = data.size_bytes();
// 						m_bit = bit;
// 						m_valueCountPerPairValueRecord = valueCountPerPairValueRecord;
// 					}
//
// 					operator bool() {
// 						return !!m_obj;
// 					}
//
//                     public decltype(m_obj) operator*() {
// 						return m_obj;
// 					}
//
//                     public decltype(m_obj) operator->() {
// 						return m_obj;
// 					}
//
// 					[Pure]
//                     public const ushort* GetPairValueRecord(size_t class1, size_t class2) {
// 						return &Records[m_valueCountPerPairValueRecord * (class1 * *Header.Class2Count + class2)];
// 					}
//
// 					[Pure]
//                     public ushort GetValueRecord1(size_t class1, size_t class2, ValueFormatFlags desiredRecord) {
// 						if (!((*Header.ValueFormat1).Value & desiredRecord.Value))
// 							return 0;
// 						var bit = m_bit;
// 						var pRecord = GetPairValueRecord(class1, class2);
// 						for (var i = static_cast<uint>(desiredRecord.Value); i && bit; i >>= 1, bit >>= 1) {
// 							if (bit & 1)
// 								pRecord++;
// 						}
// 						return **pRecord;
// 					}
//
// 					[Pure]
//                     public ushort GetValueRecord2(size_t class1, size_t class2, ValueFormatFlags desiredRecord) {
// 						if (!((*Header.ValueFormat2).Value & desiredRecord.Value))
// 							return 0;
// 						var bit = m_bit;
// 						var pRecord = GetPairValueRecord(class1, class2);
// 						for (var i = static_cast<uint>(desiredRecord.Value) << 16; i && bit; i >>= 1, bit >>= 1) {
// 							if (bit & 1)
// 								pRecord++;
// 						}
// 						return **pRecord;
// 					}
//
// 					[Pure]
//                     public ClassDefTable.View GetClassTableDefinition1() {
// 						return { m_bytes + *Header.ClassDef1Offset, m_length - *Header.ClassDef1Offset }
// 					}
//
// 					[Pure]
//                     public ClassDefTable.View GetClassTableDefinition2() {
// 						return { m_bytes + *Header.ClassDef2Offset, m_length - *Header.ClassDef2Offset }
// 					}
// 				}
// 			}
// 		}
//
// public struct ExtensionPositioningSubtableFormat1 {
//     public ushort PosFormat;
//     public BE<LookupType> ExtensionLookupType;
//     public UInt32BE ExtensionOffset;
// 			}
// 		}
//
// 		union {
// 			Fixed Version;
// 			Gpos.GposHeaderV1_0 HeaderV1_1;
// 			Gpos.GposHeaderV1_1 HeaderV1_0;
// 		}
//
// public class View {
// 			union {
// 				const Gpos* m_obj;
// 				const char* m_bytes;
// 			}
//             public size_t m_length;
//
// 		public:
//         public View() : m_obj(nullptr),
//         public m_length(0) {}
//         public View(std.nullptr_t) : View() {}
//         public View(decltype
//         public (m_obj) pObject, size_t length)
// 				:
//         public m_obj(pObject),
//         public m_length(length) {}
//         public View(View&&) = default;
//         public View(const View&) = default;
// 			View& operator=(View&&) = default;
// 			View& operator=(const View&) = default;
// 			View& operator=(std.nullptr_t) { m_obj = nullptr; m_length = 0; return *this; }
//             public View(
//             public const void* pData, size_t length) :
//             public View(std.span(reinterpret_cast<const char*>(pData), length)) {}
//             public template<typename T>
//             public View(std.span<T> data) : View() {
// 				if (data.size_bytes() < sizeof Gpos.GposHeaderV1_0)
// 					return;
//
// 				var obj = reinterpret_cast<decltype(m_obj)>(&data[0]);
//
// 				if (obj->Version.Major < 1)
// 					return;
//
// 				if (obj->Version.Major > 1 || (obj->Version.Major == 1 && obj->Version.Minor >= 1)) {
// 					if (data.size_bytes() < sizeof Gpos.GposHeaderV1_1)
// 						return;
// 				}
//
// 				m_obj = obj;
// 				m_length = data.size_bytes();
// 			}
//
// 			operator bool() {
// 				return !!m_obj;
// 			}
//
//             public decltype(m_obj) operator*() {
// 				return m_obj;
// 			}
//
//             public decltype(m_obj) operator->() {
// 				return m_obj;
// 			}
//
// 			[Pure] std.span<const ushort>
//             public LookupOffsetListOffsets() {
// 				var p = reinterpret_cast<const ushort*>(m_bytes + *HeaderV1_0.LookupOffsetListOffset);
// 				return { p + 1, **p }
// 			}
//
// 			[Pure]
//             public Dictionary<std.pair<char32_t, char32_t>, int> ExtractAdvanceX(const std.vector<SortedSet<char32_t>>& glyphToCharMap) {
// 				Dictionary<std.pair<char32_t, char32_t>, int> result;
//
// 				var LookupOffsetListOffset = *HeaderV1_0.LookupOffsetListOffset;
// 				LookupOffsetList.View LookupOffsetList(m_bytes + LookupOffsetListOffset, m_length - LookupOffsetListOffset);
// 				if (!LookupOffsetList)
// 					return {}
//
// 				for (var& lookupTableOffset : LookupOffsetList.Offsets()) {
// 					var offset = LookupOffsetListOffset + *lookupTableOffset;
// 					
// 					LookupTable.View lookupTable(m_bytes + offset, m_length - offset);
// 					if (!lookupTable)
// 						continue;
//
// 					for (size_t subtableIndex = 0, i_ = *lookupTable->Header.SubtableCount; subtableIndex < i_; subtableIndex++) {
// 						var subtableSpan = lookupTable.SubtableSpan(subtableIndex);
//
// 						switch (*lookupTable->Header.LookupType) {
// 						case LookupType.PairAdjustment:
// 							break;
//
// 						case LookupType.ExtensionPositioning: {
// 							if (subtableSpan.size() < sizeof(ExtensionPositioningSubtable.Format1))
// 								continue;
// 							var& table = *reinterpret_cast<const ExtensionPositioningSubtable.Format1*>(subtableSpan.data());
// 							if (*table.PosFormat != 1)
// 								continue;
// 							if (*table.ExtensionLookupType != LookupType.PairAdjustment)
// 								continue;
// 							subtableSpan = subtableSpan.subspan(table.ExtensionOffset);
// 							break;
// 						}
//
// 						default:
// 							continue;
// 						}
//
// 						if (PairAdjustmentPositioningSubtable.Format1.View v(subtableSpan); v) {
// 							if (!(*v->Header.ValueFormat1).AdvanceX && !(*v->Header.ValueFormat2).PlacementX)
// 								continue;
//
// 							var coverageTable = v.CoverageTableView();
// 							if (coverageTable->Header.FormatId == 1) {
// 								var glyphSpan = coverageTable.GlyphSpan();
// 								for (size_t coverageIndex = 0; coverageIndex < glyphSpan.size(); coverageIndex++) {
// 									var glyph1Id = *glyphSpan[coverageIndex];
// 									for (var c1 : glyphToCharMap[glyph1Id]) {
// 										var pairSetView = v.PairSetView(coverageIndex);
// 										for (size_t pairIndex = 0, j_ = *pairSetView->Count; pairIndex < j_; pairIndex++) {
// 											for (var c2 : glyphToCharMap[pairSetView.GetSecondGlyph(pairIndex)]) {
// 												var val = static_cast<int16_t>(pairSetView.GetValueRecord1(pairIndex, { .AdvanceX = 1 }))
// 													+ static_cast<int16_t>(pairSetView.GetValueRecord2(pairIndex, { .PlacementX = 1 }));
// 												if (val)
// 													result[std.make_pair(c1, c2)] = val;
// 											}
// 										}
// 									}
// 								}
//
// 							} else if (coverageTable->Header.FormatId == 2) {
// 								for (var& rangeRecord : coverageTable.RangeRecordSpan()) {
// 									var startGlyphId = static_cast<size_t>(*rangeRecord.StartGlyphId);
// 									var endGlyphId = static_cast<size_t>(*rangeRecord.EndGlyphId);
// 									var startCoverageIndex = static_cast<size_t>(*rangeRecord.StartCoverageIndex);
// 									for (size_t glyphIndex = 0, i_ = endGlyphId - startGlyphId + 1; glyphIndex < i_; glyphIndex++) {
// 										var glyph1Id = startGlyphId + glyphIndex;
// 										for (var c1 : glyphToCharMap[glyph1Id]) {
// 											var pairSetView = v.PairSetView(startCoverageIndex + glyphIndex);
// 											for (size_t pairIndex = 0, j_ = *pairSetView->Count; pairIndex < j_; pairIndex++) {
// 												for (var c2 : glyphToCharMap[pairSetView.GetSecondGlyph(pairIndex)]) {
// 													var val = static_cast<int16_t>(pairSetView.GetValueRecord1(pairIndex, { .AdvanceX = 1 }))
// 														+ static_cast<int16_t>(pairSetView.GetValueRecord2(pairIndex, { .PlacementX = 1 }));
// 													if (val)
// 														result[std.make_pair(c1, c2)] = val;
// 												}
// 											}
// 										}
// 									}
// 								}
// 							}
//
// 						} else if (PairAdjustmentPositioningSubtable.Format2.View v(subtableSpan); v) {
// 							if (!(*v->Header.ValueFormat1).AdvanceX && !(*v->Header.ValueFormat2).PlacementX)
// 								continue;
//
// 							for (var& [class1, glyphs1] : v.GetClassTableDefinition1().ClassToGlyphMap()) {
// 								if (class1 >= v->Header.Class1Count)
// 									continue;
//
// 								for (var& [class2, glyphs2] : v.GetClassTableDefinition2().ClassToGlyphMap()) {
// 									if (class2 >= v->Header.Class1Count)
// 										continue;
//
// 									var val = 0
// 										+ static_cast<int16_t>(v.GetValueRecord1(class1, class2, { .AdvanceX = 1 }))
// 										+ static_cast<int16_t>(v.GetValueRecord2(class1, class2, { .PlacementX = 1 }));
// 									if (!val)
// 										continue;
//
// 									for (var glyph1 : glyphs1) {
// 										for (var c1 : glyphToCharMap[glyph1]) {
// 											for (var glyph2 : glyphs2) {
// 												for (var c2 : glyphToCharMap[glyph2]) {
// 													result[std.make_pair(c1, c2)] = val;
// 												}
// 											}
// 										}
// 									}
// 								}
// 							}
// 						}
// 					}
// 				}
//
// 				return result;
// 			}
// 		}
// 	}
