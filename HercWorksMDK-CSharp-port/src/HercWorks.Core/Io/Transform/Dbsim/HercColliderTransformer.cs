using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .COL herc collider files (see <see cref="HercCollider"/>
/// for the format writeup). New: no Java equivalent existed — HercCollider.java was modeled but
/// never wired to a transformer, and only ever read its 10-byte header, dropping the rest of the
/// file entirely.
///
/// Read-only for the same reason as <see cref="Common.StringFileTransformer"/>: the component
/// data past the header isn't decoded, so there's no confirmed structure to write back.
/// </summary>
public class HercColliderTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var col = new HercCollider {
			RawBytes = inputArray,
			Ext = FileType.Col,

			PrimaryBBoxesTotal = IndexShortLE(),
			Unk2_flag = IndexShortLE(),
			CollideType = IndexShortLE(),
			Unk4_val = IndexShortLE(),
			Unk8_val = IndexShortLE(),
		};

		int remainingShorts = (inputArray.Length - Index) / 2;
		col.ComponentData = IndexShortLEArray(remainingShorts);

		return col;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		// TODO: not implemented — component data past the header isn't decoded (see class doc
		// comment), so a byte-exact round-trip isn't currently achievable.
		return null;
	}
}
