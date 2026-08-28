using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Reads and writes <c>dat\EXPLOS.DAT</c> — see <see cref="ExplosionData"/> for the layout and
/// where it came from. Round-trips byte-exactly: retail's 964 bytes are 2 + 20*4 + 2 + 22*0x28
/// with nothing left over.
/// </summary>
public class ExplosionDataTransformer : ByteTransformer<ExplosionData> {
	/// <summary>Bytes one <see cref="ExplosionTypeEntry"/> occupies — <c>FUN_00407b20</c>'s stride.</summary>
	private const int TypeEntryLength = 0x28;

	public override ExplosionData? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new ExplosionData();

		short shapeCount = IndexShortLE();
		data.Shapes = new ExplosionShapeEntry[shapeCount];
		for (int s = 0; s < shapeCount; s++) {
			data.Shapes[s] = new ExplosionShapeEntry {
				AnimSequence = IndexShortLE(),
				TextureBankIndex = IndexShortLE()
			};
		}

		short typeCount = IndexShortLE();
		data.Types = new ExplosionTypeEntry[typeCount];
		for (int t = 0; t < typeCount; t++) {
			int start = Index;

			var type = new ExplosionTypeEntry {
				ShapeIndex = IndexShortLE(),
				FrameInterval = IndexShortLE(),
				TrailEffect = IndexShortLE(),
				LightMode = IndexShortLE()
			};

			for (int f = 0; f < ExplosionTypeEntry.FrameIntensityCount; f++) {
				type.FrameIntensity[f] = IndexShortLE();
			}

			type.ProximityRadius = IndexIntLE();
			type.SoundId = IndexShortLE();
			type.ObjectClass = IndexShortLE();

			// The row is a fixed stride and the fields above account for all of it; stepping to the
			// next row from its start rather than from wherever the reads finished keeps a future
			// added field from silently shifting every following row.
			Index = start + TypeEntryLength;
			data.Types[t] = type;
		}

		return data;
	}

	public override byte[]? Write(ExplosionData data) {
		using var outStream = new MemoryStream();

		var shapes = data.Shapes ?? Array.Empty<ExplosionShapeEntry>();
		Emit(outStream, WriteShortLE((short)shapes.Length));
		foreach (var shape in shapes) {
			Emit(outStream, WriteShortLE(shape.AnimSequence));
			Emit(outStream, WriteShortLE(shape.TextureBankIndex));
		}

		var types = data.Types ?? Array.Empty<ExplosionTypeEntry>();
		Emit(outStream, WriteShortLE((short)types.Length));
		foreach (var type in types) {
			long start = outStream.Position;

			Emit(outStream, WriteShortLE(type.ShapeIndex));
			Emit(outStream, WriteShortLE(type.FrameInterval));
			Emit(outStream, WriteShortLE(type.TrailEffect));
			Emit(outStream, WriteShortLE(type.LightMode));

			for (int f = 0; f < ExplosionTypeEntry.FrameIntensityCount; f++) {
				Emit(outStream, WriteShortLE(type.FrameIntensity[f]));
			}

			Emit(outStream, WriteIntLE(type.ProximityRadius));
			Emit(outStream, WriteShortLE(type.SoundId));
			Emit(outStream, WriteShortLE(type.ObjectClass));

			while (outStream.Position - start < TypeEntryLength) {
				outStream.WriteByte(0);
			}
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
