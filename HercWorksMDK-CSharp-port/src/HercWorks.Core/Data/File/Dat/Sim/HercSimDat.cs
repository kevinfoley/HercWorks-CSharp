using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - dat\[herc].dat — full documented byte layout is extensive (~216 bytes covering speed,
/// turning, camera, animation IDs, torso pitch/twist, model flags, shadow flags, shield max,
/// physics friction, debris file, etc.). See org.hercworks.core.data.file.dat.sim.HercSimDat in
/// the Java source for the complete offset-by-offset documentation — reproduced there rather than
/// duplicated here since it's ~100 lines of field-by-field notes.
/// Ported from org.hercworks.core.data.file.dat.sim.HercSimDat.
/// </summary>
public class HercSimDat : DataFile {
	public short SpeedTurn { get; set; }
	public short SpeedReverse { get; set; }
	public short SpeedForward { get; set; }
	public short SpeedAccelDecel { get; set; }

	public short DecelTurning { get; set; }

	public short CameraBoneId { get; set; }

	public short AnimId_Walk { get; set; }
	public short AnimId_Run { get; set; } = 2;
	public short AnimId_StopMove { get; set; } = 3;
	public short AnimId_TorsoPitch { get; set; } = 4;
	public short UnitOffsetYAdjust { get; set; }

	public short Unk22_Val750Razor0 { get; set; }

	public short AiAimTargOffset { get; set; }

	public short InputTorsoRazrFlag { get; set; }
	public short TorsoTwistSpeed { get; set; }
	public short TorsoRotateAccel { get; set; }
	public short TorsoTwistDegreeMax { get; set; }
	public short InputFlagsTorso { get; set; }
	public short TorsoPitchMaxRate { get; set; }
	public short TorsoPitchRate { get; set; }
	public short TorsoPitchMax { get; set; }
	public short TorsoPitchMin { get; set; }

	public short Unk44_MoveAnimRate { get; set; }

	public static int ModelLodArrOFs { get; set; } = 46;
	public byte[] ModelLoDBoneIds { get; set; } = new byte[20];

	// 0x58-0x65 - extra boneId space in array, as seen in PITBULL.DAT

	public short Unk66_Val1000 { get; set; } = 1000;

	public short LegsCritFlags1 { get; set; }
	public short LegsCritFlags2 { get; set; }
	public short ModelLegsTotal { get; set; }
	public short ModelFlagNoDebris { get; set; }

	public short Unk76_Val { get; set; }

	/// <summary>Hercs have 0, razor has 1.</summary>
	public short InputFlagFlyer { get; set; }

	/// <summary>Possibly HUD id or even palette ID.</summary>
	public short Unk80_ValHudId { get; set; }

	/// <summary>Different per herc, usually 1024, 1500, or 800.</summary>
	public short Unk82_val { get; set; }

	/// <summary>Usually 1.</summary>
	public short Unk84_val { get; set; }

	/// <summary>0x86 - 0x97.</summary>
	public byte[]? NameBytes { get; set; }

	public short CameraYAxisAdj { get; set; }
	public short CameraXAxisAdj { get; set; }

	// blank bytes 0x102

	public short CameraExtOrgOffset { get; set; }

	// blank bytes 0x106

	public short Unk108_camExtVal1 { get; set; }
	public short Unk110_camExtVal2 { get; set; }

	public short ModelFlagsShadow1 { get; set; }
	public short ModelFlagsShadow2 { get; set; }

	public short Unk116_val { get; set; }
	public short Unk118_val { get; set; }
	public short Unk120_val { get; set; }
	public short Unk122_mdlFlagVal { get; set; }

	public static int Unk124_range { get; set; } = 12;
	public short[]? Unk124_all500 { get; set; }

	public short ModelSkinId { get; set; }

	public short Unk150_val { get; set; }
	public short Unk152_val { get; set; }
	public short Unk154_fixedVal { get; set; }
	public short Unk156_400or800 { get; set; }

	// 158 - 169 - BLANK BYTES

	public short Unk170_val { get; set; }
	public short Unk172_val { get; set; }
	public short Unk174_250or275 { get; set; }

	// 176 - 189 - BLANK BYTES

	public short ShieldMaxTotal { get; set; }
	public short Unk192_val { get; set; }
	public short PhysicsFrictionCoef { get; set; }
	public short PhysicsFrctionAccel { get; set; }

	// 198 - 203 - BLANK BYTES

	public byte[]? DebrisFile { get; set; }
}
