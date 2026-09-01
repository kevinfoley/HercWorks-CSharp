namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - dat\[herc].dat — full documented byte layout is extensive (~216 bytes covering speed,
/// turning, camera, animation IDs, torso pitch/twist, model flags, shadow flags, shield max,
/// physics friction, debris file, etc.). See org.hercworks.core.data.file.dat.sim.HercSimDat in
/// the Java source for the complete offset-by-offset documentation — reproduced there rather than
/// duplicated here since it's ~100 lines of field-by-field notes.
/// Ported from org.hercworks.core.data.file.dat.sim.HercSimDat.
/// </summary>
public class HercSimDat {
	public short SpeedTurn { get; set; }
	public short SpeedReverse { get; set; }
	public short SpeedForward { get; set; }
	/// <summary>Offset 6 — how far the speed scalar moves toward its target each tick.</summary>
	public short SpeedAccelDecel { get; set; }

	/// <summary>Offset 8 — the same step for the turn rate.</summary>
	public short DecelTurning { get; set; }

	/// <summary>Offset 10 — the model node the cockpit eye rides.</summary>
	public short CameraBoneId { get; set; }

	public short AnimId_Walk { get; set; }
	public short AnimId_Run { get; set; } = 2;
	public short AnimId_StopMove { get; set; } = 3;
	/// <summary>
	/// Offset 18 — the stop / step-off sequence played when slowing to a halt in reverse. The torso 
	/// pitch parameters are offsets 36-42.
	/// </summary>
	public short AnimId_StopReverse { get; set; } = 4;
	public short UnitOffsetYAdjust { get; set; }

	public short Unk22_Val750Razor0 { get; set; }

	public short AiAimTargOffset { get; set; }

	/// <summary>
	/// Offset 26 — the torso-twist sequence id. <c>Mech_Constructor</c> builds the mech's second
	/// animation thread on it, and <c>Mech_TorsoTwistTick</c> seeks that thread by the twist angle;
	/// the sequence is a full turn of the torso node.
	/// </summary>
	public short AnimId_TorsoTwist { get; set; }
	public short TorsoTwistSpeed { get; set; }
	public short TorsoRotateAccel { get; set; }
	public short TorsoTwistDegreeMax { get; set; }

	/// <summary>
	/// Offset 34 — the torso-pitch sequence id, the pitch counterpart of
	/// <see cref="AnimId_TorsoTwist"/>.
	/// </summary>
	public short AnimId_TorsoPitch { get; set; }
	public short TorsoPitchMaxRate { get; set; }
	public short TorsoPitchRate { get; set; }
	public short TorsoPitchMax { get; set; }
	public short TorsoPitchMin { get; set; }

	/// <summary>Offset 44 — the speed at which the walk gait gives way to the run gait.</summary>
	public short GaitThreshold { get; set; }

	public static int ModelLodArrOFs { get; set; } = 46;
	public byte[] ModelLoDBoneIds { get; set; } = new byte[20];

	// 0x58-0x65 - extra boneId space in array, as seen in PITBULL.DAT

	public short Unk66_Val1000 { get; set; } = 1000;

	/// <summary>
	/// Offset 68 — the death / fall sequence.
	/// </summary>
	public short AnimId_Death { get; set; }

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

	/// <summary>
	/// Offset 108 — <see cref="GaitThreshold"/> on the reverse side.
	/// </summary>
	public short GaitThresholdReverse { get; set; }
	public short Unk110_camExtVal2 { get; set; }

	public short ModelFlagsShadow1 { get; set; }
	public short ModelFlagsShadow2 { get; set; }

	public short Unk116_val { get; set; }
	public short Unk118_val { get; set; }
	public short Unk120_val { get; set; }

	/// <summary>
	/// Offsets 112 and 117 read as <b>bytes</b>, one per leg — the per-leg kind byte
	/// (<c>typeRec+0x72</c>) and the shape part id the leg's node hangs on (<c>typeRec+0x77</c>).
	/// <c>Mech_PlaceLegsOnGround</c> (<c>004195c8</c>) walks both with the leg index, for
	/// <see cref="ModelLegsTotal"/> legs. Every retail HERC states two legs, kinds 0 and 0, on parts
	/// 14 and 15.
	///
	/// <para><b>Read-only views.</b> These bytes overlap the shorts declared above —
	/// <see cref="ModelFlagsShadow1"/> covers 112-113, and 117-118 straddle
	/// <see cref="Unk116_val"/> and <see cref="Unk118_val"/> — which are what the writer emits. They
	/// are exposed separately because the exe reads them per byte and those shorts are not what the
	/// bytes mean; setting them changes nothing on the way out.</para>
	/// </summary>
	public byte[] LegKinds { get; set; } = System.Array.Empty<byte>();

	/// <inheritdoc cref="LegKinds"/>
	public byte[] LegPartIds { get; set; } = System.Array.Empty<byte>();
	/// <summary>
	/// Offset 122 — the turn-in-place sequence. Uniform across the fleet: 7 frames of 1820 
	/// BAM each, no translation.
	/// </summary>
	public short AnimId_TurnInPlace { get; set; }

	public static int Unk124_range { get; set; } = 12;
	public short[]? Unk124_all500 { get; set; }

	/// <summary>
	/// File offset 148 — selects which shared texture atlas DBSIM binds to every TSShapeInstance
	/// sub-component of this mech at spawn time. <c>MechType_InitOne</c> (<c>004201a8</c>) writes
	/// <c>&amp;g_MechTextureGroupSlots + value*8</c> into <c>TSShapeInstance+0x26</c>, the bound-DBA
	/// field the render code reads.
	///
	/// <para>Values 0-6 index a literal 7-entry name table in the exe; see
	/// <see cref="TextureGroupDbaBaseName"/> for the names and
	/// docs/formats/dts-texture-binding.md's "DBSIM's mech-to-texture mapping" for the per-mech
	/// roster, byte-verified against every retail <c>simvol0/dat/*.DAT</c>.</para>
	/// </summary>
	public short ModelSkinId { get; set; }

	/// <summary>
	/// Maps ModelSkinId to the simvol0/dba/&lt;name&gt;.DBA basename DBSIM actually loads for that
	/// group (see ModelSkinId's doc comment) -- null for an out-of-range value rather than guessing.
	/// </summary>
	public static string? TextureGroupDbaBaseName(short modelSkinId) => modelSkinId switch {
		0 => "LIGHT",
		1 => "MEDIUM",
		2 => "HEAVY",
		3 => "ENEMY",
		4 => "APOCATEX",
		5 => "RAZORTEX",
		6 => "NEWHERCS",
		_ => null
	};

	/// <summary>
	/// Offsets 150, 152, 154 and 156 — the height a leg node's fore/aft position must cross for a
	/// <b>footfall</b>, one per gait: walking forward, walking backward, running, and the fourth the
	/// falling/landing case. <c>Mech_PlaceLegsOnGround</c> (<c>004195c8</c>) reads them as
	/// <c>typeRec+0x98 + gait*2</c>.
	///
	/// <para>A leg arms when it passes <see cref="FootfallRearmWalk"/> and fires when it comes back
	/// through this one, which is the instant the foot plants: the original plays sound <c>0x1d</c>
	/// and, for the player, kicks the cockpit view. The reverse gait's pair is negative and its two
	/// comparisons are the other way round, since the foot swings the other way.</para>
	/// </summary>
	public short FootfallTriggerWalk { get; set; }

	/// <inheritdoc cref="FootfallTriggerWalk"/>
	public short FootfallTriggerReverse { get; set; }

	/// <inheritdoc cref="FootfallTriggerWalk"/>
	public short FootfallTriggerRun { get; set; }

	/// <inheritdoc cref="FootfallTriggerWalk"/>
	public short FootfallTriggerLand { get; set; }

	// 158 - 169 - BLANK BYTES

	/// <summary>
	/// Offsets 170, 172 and 174 — the arming counterpart of <see cref="FootfallTriggerWalk"/>, in the
	/// same gait order (<c>typeRec+0xac + gait*2</c>). A leg that has not passed this since its last
	/// footfall cannot fire another.
	///
	/// <para>The landing gait's entry, at offset 176, is inside the blank run below: it is zero in
	/// every retail file and is left unparsed.</para>
	/// </summary>
	public short FootfallRearmWalk { get; set; }

	/// <inheritdoc cref="FootfallRearmWalk"/>
	public short FootfallRearmReverse { get; set; }

	/// <inheritdoc cref="FootfallRearmWalk"/>
	public short FootfallRearmRun { get; set; }

	// 176 - 189 - BLANK BYTES

	public short ShieldMaxTotal { get; set; }
	public short Unk192_val { get; set; }
	/// <summary>
	/// Offsets 194 and 196 — the stride-calibration pair MechType_InitOne (004201a8) turns into the
	/// Q16 factor it rescales the speed fields by, <c>Q16Divide(offset196 * 400, offset194)</c>.
	/// See Herculan.Engine.Sim.MechTypeRecord, which applies the rescale.
	/// </summary>
	public short StrideScaleDivisor { get; set; }

	/// <inheritdoc cref="StrideScaleDivisor"/>
	public short StrideScaleNumerator { get; set; }

	// 198 - 203 - BLANK BYTES

	public byte[]? DebrisFile { get; set; }
}
