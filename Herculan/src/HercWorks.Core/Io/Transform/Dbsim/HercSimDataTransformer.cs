using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.HercSimDataTransformer.
///
/// Expects the 216-byte record ALONE — i.e. <see cref="HercWorks.Vol.VolEntry.RawBytes"/>, which
/// VolFileReader has already advanced past the 9-byte VOL entry prefix (1 compression-type byte,
/// 4 size bytes, 4 magic bytes; see VolEntry's doc comment). Files dumped straight out of a VOL
/// to disk normally KEEP that prefix, so feeding one of those in unmodified shifts every field by
/// 9 bytes and yields plausible-looking nonsense rather than an obvious failure. Skip 9 bytes
/// first if the source is a raw dump. DBSIM reads the same 216 bytes into MECH_TYPE_DATA at
/// record offset 2, so a field at record offset N here is the exe's typeRecord+N+2.
/// </summary>
public class HercSimDataTransformer : ByteTransformer<HercSimDat> {
	public override HercSimDat? Parse(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): null input
			return null;
		}

		var data = new HercSimDat();

		SetBytes(inputArray);

		data.SpeedTurn = IndexShortLE();
		data.SpeedReverse = IndexShortLE();
		data.SpeedForward = IndexShortLE();
		data.SpeedAccelDecel = IndexShortLE();
		data.DecelTurning = IndexShortLE();

		data.CameraBoneId = IndexShortLE();

		data.AnimId_Walk = IndexShortLE();

		data.AnimId_Run = IndexShortLE();
		data.AnimId_StopMove = IndexShortLE();
		data.AnimId_StopReverse = IndexShortLE();
		data.UnitOffsetYAdjust = IndexShortLE();
		data.Unk22_Val750Razor0 = IndexShortLE();

		data.AiAimTargOffset = IndexShortLE();

		data.AnimId_TorsoTwist = IndexShortLE();
		data.TorsoTwistSpeed = IndexShortLE();

		data.TorsoRotateAccel = IndexShortLE();

		data.TorsoTwistDegreeMax = IndexShortLE();
		data.AnimId_TorsoPitch = IndexShortLE();
		data.TorsoPitchMaxRate = IndexShortLE();
		data.TorsoPitchRate = IndexShortLE();
		data.TorsoPitchMax = IndexShortLE();
		data.TorsoPitchMin = IndexShortLE();

		data.GaitThreshold = IndexShortLE();

		for (int i = 0; i < 20; i++) {
			data.ModelLoDBoneIds[i] = IndexByte();
		}

		data.Unk66_Val1000 = IndexShortLE();

		data.AnimId_Death = IndexShortLE();
		data.LegsCritFlags2 = IndexShortLE();
		data.ModelLegsTotal = IndexShortLE();
		data.ModelFlagNoDebris = IndexShortLE();

		data.Unk76_Val = IndexShortLE();

		data.InputFlagFlyer = IndexShortLE();

		data.Unk80_ValHudId = IndexShortLE();

		data.Unk82_val = IndexShortLE();
		data.Unk84_val = IndexShortLE();

		data.NameBytes = IndexSegment(12);

		data.CameraYAxisAdj = IndexShortLE();
		data.CameraXAxisAdj = IndexShortLE();

		Skip(2); // blank bytes 0x102

		data.CameraExtOrgOffset = IndexShortLE();

		Skip(2); // blank bytes 0x106

		data.GaitThresholdReverse = IndexShortLE();
		data.Unk110_camExtVal2 = IndexShortLE();

		data.ModelFlagsShadow1 = IndexShortLE();
		data.ModelFlagsShadow2 = IndexShortLE();

		data.Unk116_val = IndexShortLE();
		data.Unk118_val = IndexShortLE();
		data.Unk120_val = IndexShortLE();

		data.AnimId_TurnInPlace = IndexShortLE();

		var seg500 = new short[12];
		for (int i = 0; i < 12; i++) {
			seg500[i] = IndexShortLE();
		}
		data.Unk124_all500 = seg500;

		data.ModelSkinId = IndexShortLE();

		data.Unk150_val = IndexShortLE();
		data.Unk152_val = IndexShortLE();
		data.Unk154_fixedVal = IndexShortLE();
		data.Unk156_400or800 = IndexShortLE();

		Skip(12);

		data.Unk170_val = IndexShortLE();
		data.Unk172_val = IndexShortLE();
		data.Unk174_250or275 = IndexShortLE();

		Skip(14);

		data.ShieldMaxTotal = IndexShortLE();
		data.Unk192_val = IndexShortLE();
		data.StrideScaleDivisor = IndexShortLE();
		data.StrideScaleNumerator = IndexShortLE();

		Skip(6);

		data.DebrisFile = IndexSegment(12);

		return data;
	}

	public override byte[]? Write(HercSimDat data) {
		using var outStream = new MemoryStream();

		Emit(outStream, WriteShortLE(data.SpeedTurn));
		Emit(outStream, WriteShortLE(data.SpeedReverse));
		Emit(outStream, WriteShortLE(data.SpeedForward));
		Emit(outStream, WriteShortLE(data.SpeedAccelDecel));
		Emit(outStream, WriteShortLE(data.DecelTurning));

		Emit(outStream, WriteShortLE(data.CameraBoneId));

		Emit(outStream, WriteShortLE(data.AnimId_Walk));

		Emit(outStream, WriteShortLE(data.AnimId_Run));
		Emit(outStream, WriteShortLE(data.AnimId_StopMove));
		Emit(outStream, WriteShortLE(data.AnimId_StopReverse));
		Emit(outStream, WriteShortLE(data.UnitOffsetYAdjust));

		Emit(outStream, WriteShortLE(data.Unk22_Val750Razor0));

		Emit(outStream, WriteShortLE(data.AiAimTargOffset));

		Emit(outStream, WriteShortLE(data.AnimId_TorsoTwist));
		Emit(outStream, WriteShortLE(data.TorsoTwistSpeed));
		Emit(outStream, WriteShortLE(data.TorsoRotateAccel));
		Emit(outStream, WriteShortLE(data.TorsoTwistDegreeMax));
		Emit(outStream, WriteShortLE(data.AnimId_TorsoPitch));
		Emit(outStream, WriteShortLE(data.TorsoPitchMaxRate));
		Emit(outStream, WriteShortLE(data.TorsoPitchRate));
		Emit(outStream, WriteShortLE(data.TorsoPitchMax));
		Emit(outStream, WriteShortLE(data.TorsoPitchMin));

		Emit(outStream, WriteShortLE(data.GaitThreshold));

		for (int i = 0; i < 20; i++) {
			outStream.WriteByte(data.ModelLoDBoneIds[i]);
		}

		Emit(outStream, WriteShortLE(data.Unk66_Val1000));

		Emit(outStream, WriteShortLE(data.AnimId_Death));
		Emit(outStream, WriteShortLE(data.LegsCritFlags2));
		Emit(outStream, WriteShortLE(data.ModelLegsTotal));
		Emit(outStream, WriteShortLE(data.ModelFlagNoDebris));

		Emit(outStream, WriteShortLE(data.Unk76_Val));

		Emit(outStream, WriteShortLE(data.InputFlagFlyer));

		Emit(outStream, WriteShortLE(data.Unk80_ValHudId));

		Emit(outStream, WriteShortLE(data.Unk82_val));

		Emit(outStream, WriteShortLE(data.Unk84_val));

		// write name
		outStream.Write(data.NameBytes!, 0, data.NameBytes!.Length);

		Emit(outStream, WriteShortLE(data.CameraYAxisAdj));
		Emit(outStream, WriteShortLE(data.CameraXAxisAdj));

		// blank bytes 0x102
		outStream.WriteByte(0x00);
		outStream.WriteByte(0x00);

		Emit(outStream, WriteShortLE(data.CameraExtOrgOffset));

		// blank bytes 0x106
		outStream.WriteByte(0x00);
		outStream.WriteByte(0x00);

		Emit(outStream, WriteShortLE(data.GaitThresholdReverse));
		Emit(outStream, WriteShortLE(data.Unk110_camExtVal2));

		Emit(outStream, WriteShortLE(data.ModelFlagsShadow1));
		Emit(outStream, WriteShortLE(data.ModelFlagsShadow2));

		Emit(outStream, WriteShortLE(data.Unk116_val));
		Emit(outStream, WriteShortLE(data.Unk118_val));
		Emit(outStream, WriteShortLE(data.Unk120_val));
		Emit(outStream, WriteShortLE(data.AnimId_TurnInPlace));

		// range
		for (int i = 0; i < 12; i++) {
			Emit(outStream, WriteShortLE(data.Unk124_all500![i]));
		}

		Emit(outStream, WriteShortLE(data.ModelSkinId));

		Emit(outStream, WriteShortLE(data.Unk150_val));
		Emit(outStream, WriteShortLE(data.Unk152_val));
		Emit(outStream, WriteShortLE(data.Unk154_fixedVal));
		Emit(outStream, WriteShortLE(data.Unk156_400or800));

		// 158 - 169 - BLANK BYTES
		for (int i = 0; i < 12; i++) {
			outStream.WriteByte(0x00);
		}

		Emit(outStream, WriteShortLE(data.Unk170_val));
		Emit(outStream, WriteShortLE(data.Unk172_val));
		Emit(outStream, WriteShortLE(data.Unk174_250or275));

		// 176 - 189 - BLANK BYTES
		for (int i = 0; i < 14; i++) {
			outStream.WriteByte(0x00);
		}

		Emit(outStream, WriteShortLE(data.ShieldMaxTotal));
		Emit(outStream, WriteShortLE(data.Unk192_val));
		Emit(outStream, WriteShortLE(data.StrideScaleDivisor));
		Emit(outStream, WriteShortLE(data.StrideScaleNumerator));

		// 198 - 203 - BLANK BYTES
		for (int i = 0; i < 6; i++) {
			outStream.WriteByte(0x00);
		}

		outStream.Write(data.DebrisFile!, 0, data.DebrisFile!.Length);

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
