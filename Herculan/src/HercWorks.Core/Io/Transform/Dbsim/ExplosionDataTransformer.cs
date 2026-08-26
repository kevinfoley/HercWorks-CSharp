using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Reads and writes <c>dat\EXPLOS.DAT</c> — see <see cref="ExplosionData"/> for the layout and
/// where it came from. Round-trips byte-exactly: retail's 964 bytes are 2 + 20*4 + 2 + 22*0x28
/// with nothing left over.
/// </summary>
public class ExplosionDataTransformer : ThreeSpaceByteTransformer {
	/// <summary>Bytes one <see cref="ExplosionTypeEntry"/> occupies — <c>FUN_00407b20</c>'s stride.</summary>
	private const int TypeEntryLength = 0x28;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new ExplosionData {
			Ext = FileType.Dat,
			Dir = FileType.Dat,
			RawBytes = inputArray
		};

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

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			return null;
		}

		using var outStream = new MemoryStream();
		var data = (ExplosionData)source;

		var shapes = data.Shapes ?? Array.Empty<ExplosionShapeEntry>();
		Write(outStream, WriteShortLE((short)shapes.Length));
		foreach (var shape in shapes) {
			Write(outStream, WriteShortLE(shape.AnimSequence));
			Write(outStream, WriteShortLE(shape.TextureBankIndex));
		}

		var types = data.Types ?? Array.Empty<ExplosionTypeEntry>();
		Write(outStream, WriteShortLE((short)types.Length));
		foreach (var type in types) {
			long start = outStream.Position;

			Write(outStream, WriteShortLE(type.ShapeIndex));
			Write(outStream, WriteShortLE(type.FrameInterval));
			Write(outStream, WriteShortLE(type.TrailEffect));
			Write(outStream, WriteShortLE(type.LightMode));

			for (int f = 0; f < ExplosionTypeEntry.FrameIntensityCount; f++) {
				Write(outStream, WriteShortLE(type.FrameIntensity[f]));
			}

			Write(outStream, WriteIntLE(type.ProximityRadius));
			Write(outStream, WriteShortLE(type.SoundId));
			Write(outStream, WriteShortLE(type.ObjectClass));

			while (outStream.Position - start < TypeEntryLength) {
				outStream.WriteByte(0);
			}
		}

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
