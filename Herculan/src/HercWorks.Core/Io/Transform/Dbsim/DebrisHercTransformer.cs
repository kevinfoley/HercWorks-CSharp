using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.DebrisHercTransformer.
///
/// NOTE: the Java write method calls `entry.setSpawnDebrisFlag()` with NO arguments, in a spot
/// symmetric with every other getter call around it (and with the read path's
/// `entry.setSpawnDebrisFlag(indexShortLE())`, which does take an argument). A zero-argument
/// overload of that setter doesn't exist on the ported DebrisHerc.Entry class (nor, as far as
/// this port could tell, anywhere sensible in the data model) — this looks like a straightforward
/// typo for `entry.getSpawnDebrisFlag()`, and as written wouldn't compile. Since a literal port
/// isn't possible here (there's no equivalent bug to preserve — it's just broken Java), this uses
/// the getter, matching the read path and the pattern of every other field in this method.
/// </summary>
public class DebrisHercTransformer : ByteTransformer<DebrisHerc> {
	public override DebrisHerc? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var debris = new DebrisHerc();

		var entries = new DebrisHerc.Entry[IndexShortLE()];

		for (int i = 0; i < entries.Length; i++) {
			var entry = debris.NewEntry();

			entry.Unk1Val = IndexShortLE();
			entry.SpawnDebrisFlag = IndexShortLE();
			entry.MeshGroupId = IndexShortLE();
			entry.Unk4_0A = IndexShortLE();
			entry.Unk5_03 = IndexShortLE();
			entry.ThrowDir[0] = IndexShortLE();
			entry.ThrowDir[1] = IndexShortLE();
			entry.ThrowDir[2] = IndexShortLE();
			entry.Mass = IndexShortLE();

			entries[i] = entry;
		}

		debris.Data = entries;

		return debris;
	}

	public override byte[]? Write(DebrisHerc data) {
		using var outStream = new MemoryStream();

		var totalBytes = WriteShortLE((short)data.Data!.Length);
		outStream.Write(totalBytes, 0, totalBytes.Length);

		foreach (var entry in data.Data) {
			Emit(outStream, WriteShortLE(entry.Unk1Val));
			Emit(outStream, WriteShortLE(entry.SpawnDebrisFlag)); // see class doc
			Emit(outStream, WriteShortLE(entry.MeshGroupId));
			Emit(outStream, WriteShortLE(entry.Unk4_0A));
			Emit(outStream, WriteShortLE(entry.Unk5_03));
			Emit(outStream, WriteShortLE(entry.ThrowDir[0]));
			Emit(outStream, WriteShortLE(entry.ThrowDir[1]));
			Emit(outStream, WriteShortLE(entry.ThrowDir[2]));
			Emit(outStream, WriteShortLE(entry.Mass));
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
