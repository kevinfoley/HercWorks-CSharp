using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Reverse-engineered from SKIMMER.DAT — see <see cref="FlyerSimData"/> for the format notes.</summary>
public class FlyerSimDataTransformer : ByteTransformer<FlyerSimData> {
	private const int NameFieldLen = 28;

	public override FlyerSimData? Parse(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		var data = new FlyerSimData();

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

	public override byte[]? Write(FlyerSimData data) {
		using var outStream = new MemoryStream();

		Emit(outStream, WriteShortLE(data.SpeedTurn));
		Emit(outStream, WriteShortLE(data.SpeedReverse));
		Emit(outStream, WriteShortLE(data.SpeedForward));
		Emit(outStream, WriteShortLE(data.SpeedAccelDecel));
		Emit(outStream, WriteShortLE(data.DecelTurning));
		Emit(outStream, WriteShortLE(data.CameraBoneId));
		Emit(outStream, WriteShortLE(data.AnimId_Walk));
		Emit(outStream, WriteShortLE(data.Unk14_val));
		Emit(outStream, WriteShortLE(data.Unk16_val));

		outStream.Write(data.NameBytes!, 0, data.NameBytes!.Length);

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
