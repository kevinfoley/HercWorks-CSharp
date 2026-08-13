using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.HercSimDataTransformer.</summary>
public class HercSimDataTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): null input
			return null;
		}

		var data = new HercSimDat {
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

		data.AnimId_Run = IndexShortLE();
		data.AnimId_StopMove = IndexShortLE();
		data.AnimId_TorsoPitch = IndexShortLE();
		data.UnitOffsetYAdjust = IndexShortLE();
		data.Unk22_Val750Razor0 = IndexShortLE();

		data.AiAimTargOffset = IndexShortLE();

		data.InputTorsoRazrFlag = IndexShortLE();
		data.TorsoTwistSpeed = IndexShortLE();

		data.TorsoRotateAccel = IndexShortLE();

		data.TorsoTwistDegreeMax = IndexShortLE();
		data.InputFlagsTorso = IndexShortLE();
		data.TorsoPitchMaxRate = IndexShortLE();
		data.TorsoPitchRate = IndexShortLE();
		data.TorsoPitchMax = IndexShortLE();
		data.TorsoPitchMin = IndexShortLE();

		data.Unk44_MoveAnimRate = IndexShortLE();

		for (int i = 0; i < 20; i++) {
			data.ModelLoDBoneIds[i] = IndexByte();
		}

		data.Unk66_Val1000 = IndexShortLE();

		data.LegsCritFlags1 = IndexShortLE();
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

		data.Unk108_camExtVal1 = IndexShortLE();
		data.Unk110_camExtVal2 = IndexShortLE();

		data.ModelFlagsShadow1 = IndexShortLE();
		data.ModelFlagsShadow2 = IndexShortLE();

		data.Unk116_val = IndexShortLE();
		data.Unk118_val = IndexShortLE();
		data.Unk120_val = IndexShortLE();

		data.Unk122_mdlFlagVal = IndexShortLE();

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
		data.PhysicsFrictionCoef = IndexShortLE();
		data.PhysicsFrctionAccel = IndexShortLE();

		Skip(6);

		data.DebrisFile = IndexSegment(12);

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? dataObj) {
		using var outStream = new MemoryStream();

		var data = (HercSimDat)dataObj!;

		Write(outStream, WriteShortLE(data.SpeedTurn));
		Write(outStream, WriteShortLE(data.SpeedReverse));
		Write(outStream, WriteShortLE(data.SpeedForward));
		Write(outStream, WriteShortLE(data.SpeedAccelDecel));
		Write(outStream, WriteShortLE(data.DecelTurning));

		Write(outStream, WriteShortLE(data.CameraBoneId));

		Write(outStream, WriteShortLE(data.AnimId_Walk));

		Write(outStream, WriteShortLE(data.AnimId_Run));
		Write(outStream, WriteShortLE(data.AnimId_StopMove));
		Write(outStream, WriteShortLE(data.AnimId_TorsoPitch));
		Write(outStream, WriteShortLE(data.UnitOffsetYAdjust));

		Write(outStream, WriteShortLE(data.Unk22_Val750Razor0));

		Write(outStream, WriteShortLE(data.AiAimTargOffset));

		Write(outStream, WriteShortLE(data.InputTorsoRazrFlag));
		Write(outStream, WriteShortLE(data.TorsoTwistSpeed));
		Write(outStream, WriteShortLE(data.TorsoRotateAccel));
		Write(outStream, WriteShortLE(data.TorsoTwistDegreeMax));
		Write(outStream, WriteShortLE(data.InputFlagsTorso));
		Write(outStream, WriteShortLE(data.TorsoPitchMaxRate));
		Write(outStream, WriteShortLE(data.TorsoPitchRate));
		Write(outStream, WriteShortLE(data.TorsoPitchMax));
		Write(outStream, WriteShortLE(data.TorsoPitchMin));

		Write(outStream, WriteShortLE(data.Unk44_MoveAnimRate));

		for (int i = 0; i < 20; i++) {
			outStream.WriteByte(data.ModelLoDBoneIds[i]);
		}

		Write(outStream, WriteShortLE(data.Unk66_Val1000));

		Write(outStream, WriteShortLE(data.LegsCritFlags1));
		Write(outStream, WriteShortLE(data.LegsCritFlags2));
		Write(outStream, WriteShortLE(data.ModelLegsTotal));
		Write(outStream, WriteShortLE(data.ModelFlagNoDebris));

		Write(outStream, WriteShortLE(data.Unk76_Val));

		Write(outStream, WriteShortLE(data.InputFlagFlyer));

		Write(outStream, WriteShortLE(data.Unk80_ValHudId));

		Write(outStream, WriteShortLE(data.Unk82_val));

		Write(outStream, WriteShortLE(data.Unk84_val));

		// write name
		outStream.Write(data.NameBytes!, 0, data.NameBytes!.Length);

		Write(outStream, WriteShortLE(data.CameraYAxisAdj));
		Write(outStream, WriteShortLE(data.CameraXAxisAdj));

		// blank bytes 0x102
		outStream.WriteByte(0x00);
		outStream.WriteByte(0x00);

		Write(outStream, WriteShortLE(data.CameraExtOrgOffset));

		// blank bytes 0x106
		outStream.WriteByte(0x00);
		outStream.WriteByte(0x00);

		Write(outStream, WriteShortLE(data.Unk108_camExtVal1));
		Write(outStream, WriteShortLE(data.Unk110_camExtVal2));

		Write(outStream, WriteShortLE(data.ModelFlagsShadow1));
		Write(outStream, WriteShortLE(data.ModelFlagsShadow2));

		Write(outStream, WriteShortLE(data.Unk116_val));
		Write(outStream, WriteShortLE(data.Unk118_val));
		Write(outStream, WriteShortLE(data.Unk120_val));
		Write(outStream, WriteShortLE(data.Unk122_mdlFlagVal));

		// range
		for (int i = 0; i < 12; i++) {
			Write(outStream, WriteShortLE(data.Unk124_all500![i]));
		}

		Write(outStream, WriteShortLE(data.ModelSkinId));

		Write(outStream, WriteShortLE(data.Unk150_val));
		Write(outStream, WriteShortLE(data.Unk152_val));
		Write(outStream, WriteShortLE(data.Unk154_fixedVal));
		Write(outStream, WriteShortLE(data.Unk156_400or800));

		// 158 - 169 - BLANK BYTES
		for (int i = 0; i < 12; i++) {
			outStream.WriteByte(0x00);
		}

		Write(outStream, WriteShortLE(data.Unk170_val));
		Write(outStream, WriteShortLE(data.Unk172_val));
		Write(outStream, WriteShortLE(data.Unk174_250or275));

		// 176 - 189 - BLANK BYTES
		for (int i = 0; i < 14; i++) {
			outStream.WriteByte(0x00);
		}

		Write(outStream, WriteShortLE(data.ShieldMaxTotal));
		Write(outStream, WriteShortLE(data.Unk192_val));
		Write(outStream, WriteShortLE(data.PhysicsFrictionCoef));
		Write(outStream, WriteShortLE(data.PhysicsFrctionAccel));

		// 198 - 203 - BLANK BYTES
		for (int i = 0; i < 6; i++) {
			outStream.WriteByte(0x00);
		}

		outStream.Write(data.DebrisFile!, 0, data.DebrisFile!.Length);

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
