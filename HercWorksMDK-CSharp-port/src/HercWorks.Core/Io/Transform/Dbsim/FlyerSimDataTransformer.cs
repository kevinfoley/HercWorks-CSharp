using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Reverse-engineered from SKIMMER.DAT — see <see cref="FlyerSimData"/> for the format notes.</summary>
public class FlyerSimDataTransformer : ThreeSpaceByteTransformer {
	private const int NameFieldLen = 28;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		var data = new FlyerSimData {
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Dat
		};

		SetBytes(inputArray);

		data.SpeedTurn = IndexShortLE();
		data.SpeedReverse = IndexShortLE();
		data.SpeedForward = IndexShortLE();
		data.SpeedAccelDecel = IndexShortLE();
		data.DecelTurning = IndexShortLE();
		data.CameraBoneId = IndexShortLE();
		data.AnimId_Walk = IndexShortLE();
		data.Unk14_val = IndexShortLE();
		data.Unk16_val = IndexShortLE();

		data.NameBytes = IndexSegment(NameFieldLen);

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? dataObj) {
		using var outStream = new MemoryStream();

		var data = (FlyerSimData)dataObj!;

		Write(outStream, WriteShortLE(data.SpeedTurn));
		Write(outStream, WriteShortLE(data.SpeedReverse));
		Write(outStream, WriteShortLE(data.SpeedForward));
		Write(outStream, WriteShortLE(data.SpeedAccelDecel));
		Write(outStream, WriteShortLE(data.DecelTurning));
		Write(outStream, WriteShortLE(data.CameraBoneId));
		Write(outStream, WriteShortLE(data.AnimId_Walk));
		Write(outStream, WriteShortLE(data.Unk14_val));
		Write(outStream, WriteShortLE(data.Unk16_val));

		outStream.Write(data.NameBytes!, 0, data.NameBytes!.Length);

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
