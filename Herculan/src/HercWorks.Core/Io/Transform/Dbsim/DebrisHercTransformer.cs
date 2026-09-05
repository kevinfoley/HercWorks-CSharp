using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Reads and writes <c>dat\{name}_DEB.DAT</c> — see <see cref="DebrisHerc"/> for the layout and
/// where it came from. Round-trips byte-exactly on all 24 retail files.
/// </summary>
public class DebrisHercTransformer : ByteTransformer<DebrisHerc> {
	public override DebrisHerc? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var debris = new DebrisHerc();
		var groups = new DebrisHerc.Group[IndexShortLE()];

		for (int i = 0; i < groups.Length; i++) {
			var group = debris.NewGroup();

			group.ThrowCount = IndexShortLE();

			var pieces = new DebrisHerc.Piece[IndexShortLE()];
			for (int j = 0; j < pieces.Length; j++) {
				pieces[j] = new DebrisHerc.Piece {
					ShapeIndex = IndexShortLE(),
					Weight = IndexShortLE(),
					ChildGroup = IndexShortLE(),
					DestroyEffect = IndexShortLE(),
					OrientationYaw = IndexShortLE(),
					ThrowYaw = IndexShortLE(),
					Mass = IndexShortLE()
				};
			}

			group.Pieces = pieces;
			groups[i] = group;
		}

		debris.Data = groups;

		return debris;
	}

	public override byte[]? Write(DebrisHerc data) {
		using var outStream = new MemoryStream();

		Emit(outStream, WriteShortLE((short)data.Data!.Length));

		foreach (var group in data.Data) {
			Emit(outStream, WriteShortLE(group.ThrowCount));
			Emit(outStream, WriteShortLE((short)group.Pieces.Length));

			foreach (var piece in group.Pieces) {
				Emit(outStream, WriteShortLE(piece.ShapeIndex));
				Emit(outStream, WriteShortLE(piece.Weight));
				Emit(outStream, WriteShortLE(piece.ChildGroup));
				Emit(outStream, WriteShortLE(piece.DestroyEffect));
				Emit(outStream, WriteShortLE(piece.OrientationYaw));
				Emit(outStream, WriteShortLE(piece.ThrowYaw));
				Emit(outStream, WriteShortLE(piece.Mass));
			}
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
