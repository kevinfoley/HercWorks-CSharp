using HercWorks.Core.Data.File.Dgs;
using HercWorks.Core.Data.File.Dts;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Reads <c>dgs\BASES.DGS</c> (and the structurally-identical <c>BHULKS.DGS</c>) — the static
/// structure shape library <c>dat\BASES.DAT</c>'s <c>ShapeIndex</c> field selects into for the 57
/// of 65 structure types that aren't in <c>dts\BASES_AN.DTS</c>.
///
/// <para><b>RE summary</b> (DBSIM.EXE, see docs/formats/dgs-hd0-notes.md for the full derivation).
/// <c>BaseType_LoadShape</c> (<c>00405ebc</c>) resolves a shape by index through
/// <c>FUN_00474cd8</c>, which reopens <c>dgs\bases</c> and calls the generic polymorphic resource
/// loader <c>ClassItem_LoadResource</c> (<c>0047a038</c>) once per record until it reaches the
/// requested index — i.e. the file is a flat, sequential list of tagged records, not a
/// random-access table. Each record's 8-byte header is <c>[classId:int32][payloadSize:int32]</c>
/// (little-endian); the type code DBSIM passes for this library, <c>0x02BC0001</c>, is exactly
/// the record's own leading 4 bytes on disk — the "recordSize&lt;&lt;16|version" reading an
/// earlier pass of this doc gave that constant was a coincidence, not the real container
/// scheme.</para>
///
/// <para><b>Record layout</b>, traced through the class's Watcom C++ base-constructor chain
/// (<c>FUN_0042762c</c> → <c>FUN_00490d5c</c> → <c>FUN_0048fd94</c> → <c>FUN_0048f894</c>) and
/// verified byte-exact against the retail file (see below):</para>
/// <list type="bullet">
/// <item>3 <c>int16</c> id/name fields, then 6 raw bytes (base class header, unmodelled beyond
/// <see cref="BaseShape.Id"/>, the third field).</item>
/// <item>an <c>int16</c> child count, then that many recursively-loaded <c>ClassItem</c> objects.
/// <b>Every retail record's one child is an ordinary TSObjectHeader-family chunk</b> (observed tag
/// <c>0x0014000c</c> = <c>TSDetailPart</c>) — byte-identical to a plain <c>.DTS</c> file's own
/// chunk format, so it is read with <see cref="DTSModelTransformer.ReadOneObject"/> rather than a
/// new reader. This is the key finding that makes the format usable: no new mesh format, just a
/// new envelope around the existing one.</item>
/// <item>an <c>int16</c> count and a per-entry 32-byte record (a per-vertex table, likely
/// BSP-plane-classification data judging by its consumers — <c>FUN_00476a1c</c>'s BSP walk reads a
/// point from an array at this same stride) — read but not modelled, the engine has no use for it.</item>
/// <item>an <c>int16</c> count and that many <c>int16</c> values (a parallel index/remap array) —
/// read but not modelled.</item>
/// <item>5 more <c>int16</c> scalar fields, then a fixed 1024-byte raw block (both undecoded —
/// likely BSP node data and/or collision metadata, given the class's <c>0x1c</c>-vtable-slot BSP
/// walk over similarly-shaped nested records; not needed to draw the shape).</item>
/// <item>if the second-to-last of those 5 fields (the sub-record count) is nonzero: that many
/// raw records, each sized by the field before it (the sub-record byte size) — read but not
/// modelled.</item>
/// </list>
///
/// <para><b>Padding.</b> Every record's total on-disk footprint (8-byte header + payload) is
/// padded to an even byte count — confirmed by an independent whole-file scan for the
/// <c>0x02BC0001</c> tag pattern against the retail file: 44 of 45 records have an even
/// <c>payloadSize</c> and need no pad; the one record with an odd <c>payloadSize</c> (its
/// sub-records happen to be an odd byte size) has exactly one pad byte before the next record's
/// header, landing exactly on the next real tag.</para>
///
/// <para><b>Verified against the retail file</b> (<c>simvol0/dgs/BASES.DGS</c>, 565882 content
/// bytes): all 45 records parse with this record shape (an independent tag-pattern scan finds the
/// same 45 offsets the sequential reader does), every embedded child parses through
/// <see cref="DTSModelTransformer"/> with zero exceptions, and the resulting geometry is
/// substantial (1536 groups, 8978 polys total across all 45 shapes) — not degenerate placeholder
/// data.</para>
/// </summary>
public class BasesDgsTransformer : ThreeSpaceByteTransformer {
	/// <summary>The record header's classId, and the record's own leading 4 on-disk bytes.</summary>
	private const int ShapeTag = 0x02BC0001;

	/// <summary><c>[classId:int32][payloadSize:int32]</c>.</summary>
	private const int RecordHeaderLength = 8;

	/// <summary>Fixed-size trailing block whose contents aren't modelled — see the class doc comment.</summary>
	private const int OpaqueBlockLength = 0x400;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var dtsReader = new DTSModelTransformer();
		var shapes = new List<BaseShape>();

		while (Index + RecordHeaderLength <= inputArray.Length) {
			int recordStart = Index;
			int tag = IndexIntLE();
			int payloadSize = IndexIntLE();

			if (payloadSize < 0 || (long)Index + payloadSize > inputArray.Length) {
				throw new InvalidDataException(
					$"BASES.DGS record at offset {recordStart} declares a {payloadSize}-byte " +
					$"payload, which does not fit the {inputArray.Length}-byte file.");
			}

			int payloadEnd = Index + payloadSize;

			shapes.Add(tag == ShapeTag ? ReadShape(dtsReader, payloadEnd) : SkipUnrecognized(payloadEnd));

			if (Index != payloadEnd) {
				throw new InvalidDataException(
					$"BASES.DGS record at offset {recordStart} (tag 0x{tag:x8}) declares " +
					$"{payloadSize} payload bytes but the reader consumed {Index - (recordStart + RecordHeaderLength)}" +
					$" — the record shape does not match this file.");
			}

			// Every record's on-disk footprint (header + payload) is padded to an even total.
			if ((payloadSize & 1) != 0) {
				Index += 1;
			}
		}

		return new BaseShapeLibrary {
			RawBytes = inputArray,
			Ext = FileType.Dgs,
			Dir = FileType.Dgs,
			Shapes = shapes.ToArray()
		};
	}

	/// <summary>Not a real record — reserved for a tag this library doesn't recognize, skipped by its own declared length rather than guessed at.</summary>
	private BaseShape SkipUnrecognized(int payloadEnd) {
		Index = payloadEnd;
		return default;
	}

	private BaseShape ReadShape(DTSModelTransformer dtsReader, int payloadEnd) {
		IndexShortLE(); // id1 -- unmodelled
		IndexShortLE(); // id2 -- unmodelled
		short id = IndexShortLE();
		Index += 6; // unmodelled base-class raw fields

		short childCount = IndexShortLE();
		TSObject? geometry = null;
		for (int i = 0; i < childCount; i++) {
			byte[] bytes = GetBytes();
			int index = Index;
			var child = dtsReader.ReadOneObject(bytes, ref index);
			Index = index;
			geometry ??= child; // retail data has exactly one child; first one wins if that ever changes.
		}

		short vertexCount = IndexShortLE();
		short indexCount = IndexShortLE();
		Index += indexCount * 2;   // parallel int16 array -- unmodelled
		Index += vertexCount * 32; // per-vertex table -- unmodelled

		short subRecordSize = IndexShortLE();
		short subRecordCount = IndexShortLE();
		Index += 2 * 3; // three more undecoded scalar fields
		Index += OpaqueBlockLength;

		if (subRecordCount != 0) {
			Index += subRecordCount * subRecordSize;
		}

		if (Index > payloadEnd) {
			throw new InvalidDataException(
				$"BASES.DGS shape record overran its own payload by {Index - payloadEnd} bytes " +
				"-- the record shape does not match this file.");
		}

		return new BaseShape(id, geometry);
	}

	public override byte[]? ObjectToBytes(DataFile? source) =>
		throw new NotSupportedException("BasesDgsTransformer is read-only -- the engine only draws structures, it never writes .DGS.");
}
