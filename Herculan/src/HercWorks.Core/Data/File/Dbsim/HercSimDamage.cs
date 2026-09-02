using HercWorks.Core.Data.Struct.Herc;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - dmg\[herc].DMG — armor, critical component HP, and other damage-related data per unit,
/// tied to the unit by name from the corresponding .DAT file.
///
/// Independently confirmed as DBSIM.EXE's own per-mech hit-zone/component table (see
/// docs/simulation/damage-system.md): loaded at runtime via a filename built from the
/// mech's own name string plus an extension, matching this file's own `dmg\[herc].DMG` location;
/// <see cref="HercPiece"/> is exactly DBSIM's 18-byte per-component record (`Armor`=max health at
/// offset 0, `DebrisFlags`=offsets 2-3, `BoneId`=offset 4, `DestructionFlags`=offset 5,
/// `MappedInternals`=the offset-6/8 dependent-list). This also settles (as far as the code goes)
/// the manual's Structural/Internal/Weaponry HDD terminology, which is not a clean
/// 3-way partition of one index space: **Structural** = most of this class's 29-slot
/// <see cref="HercSimDamage.ComponentData"/> array (the named body pieces in the doc comment
/// below — TORSO, LEG/UPPER/LOWER, FOOT, SHOULDER, etc.); **Weaponry** = a *subset of that same
/// array* distinguished only by name/position (`WEPN_BRACK/LEFT`/`RIGHT`), not a separate array —
/// weapon-specific runtime state (ammo, heat) is tracked elsewhere by DBSIM's weapon-mount-manager
/// object, not in this file; **Internal** = the *separate*, smaller <see cref="HercInternals"/>
/// table (Engine, Shield Generator, Sensor Array, Life Support, Pilot, etc.), reached
/// probabilistically through a struck structural piece's own <see cref="HercPiece.MappedInternals"/>
/// / <see cref="InternalsTarget.CritChance"/> list rather than being directly targetable — i.e. an
/// Internal system doesn't have its own health slot in the 29-component array, it's a chance-based
/// side effect of damaging whichever structural piece maps to it. `COCKPIT/FRONT`/`COCKPIT/REAR`
/// (the doc comment's own first 2 named pieces, indices 0-1) are additionally confirmed as the
/// mech's individually-checked death-trigger components (DBSIM gates its final "is this mech
/// actually dead" determination on these two specific slots being destroyed) — a plausible reading
/// of "Internal" in the manual's more casual sense of "the critical stuff," even though
/// mechanically they're ordinary Structural-array slots.
/// Ported from org.hercworks.core.data.file.dbsim.HercSimDamage.
/// </summary>
/*
 *  *	NOTE - the following are an array of values, the Skimmer only has 1 crit
 *		2-leg Hercs: 9 internals, terminates with a 0x32(50)
 *      Pitbull: 12 internals no 0x32(50) terminator
 *      
 *  
 *  0- UINT16 - Internals count, most hercs 22, skimmer  1, 
 *  UINT16 - SERVO\LEG\LEFT - Hitpoints
 *  UINT16 - SERVO\LEG\RIGHT - Hitpoints
 *  UINT16 - SENSOR ARRAY
 *  UINT16 - TARGETING COMPUTER
 *  UINT16 - SHIELD GENERATOR
 *  UINT16 - ENGINE
 *  UINT16 - HYDRAULICS
 *  UINT16 - STABILIZERS
 *  UINT16 - LIFE SUPPORT
 *  UINT16 - 0x32 value on bipedal hercs, pilot HP possibly
 *  20- 44 - UINT16 - slots for critical components, PITBULL has more components (4 legs and turret vs normal herc setup)
 *  XX- UINT16 - 32 - always ends with 50, Either this is "PILOT" HP, array terminator, or both.
 *  
 *  46- UINT16 - Total Unit components, hercs have 29, skimmer has 1
 *  
 *  UINT16 - ? - Hercs have 29, setting to 1 crashes game
 *  UINT16 - External part HP (starting with cockpit)
 *  	UINT16 - MODEL\FLAGS\TORSO\DEBRIS - 
 *  		0xFFFF = -1 = No flame, no torso mesh debris thrown.
 *  		0x0000 = 0 = Yes flame, no torso mesh debris, mesh removed.
 *  		0x0607 = 1798 = yes flame, somehow knows to throw torso mesh
 *  			note - other values remove other mesh pieces
 *  				256 = LEG_LEFT_CALF
 *  				512 = LEG_RIGHT_CALF
 *  	        	768 = LEG_LEFT_THIGH
 *  		   	1024 = LEG_RIGHT_THIGH
 *             	1280 = LEG_RIGHT_FOOT
 *             	1536 = LEG_LEFT_FOOT
 *              1798 = somehow is TORSO_CENTER
 *     UINT8 - MODEL\BONE_ID ? maybe
 *     UINT8 - unknown byte val
 *     
 *     UINT16 - Child HercInternals count
 *       Array
 *       	UINT16 - 0x14 (20) unknown use
 *          UINT16 - HercInternals ID
 *          
 *  Component list:
 *  	COCKPIT\FRONT
 *  	COCKPIT\REAR
 *  	SHOULDER\LEFT
 *  	SHOULDER\RIGHT
 *  	WEPN_BRACK\LEFT
 *  	WEPN_BRACK\RIGHT
 *  	TORSO
 *  	LEG\LEFT\UPPER
 *  	LEG\RIGHT\UPPER
 *  	LEG\LEFT\LOWER
 *  	LEG\RIGHT\LOWER
 *  	FOOT\LEFT
 *  	FOOT\RIGHT
 */

public class HercSimDamage {
	/// <summary>
	/// Source file name. <see cref="Io.Transform.Dbsim.HercDamageFileTransformer.Write"/> checks it
	/// to tell a skimmer's .DMG (one internals slot) from a herc's (22), so a caller that wants to
	/// write must set it. The read path does not populate it — that was already true when this was
	/// inherited from DataFile, and nothing in the project calls the write path today.
	/// </summary>
	public string? FileName { get; set; }

	public short InternalsTotal { get; set; }
	public InternalsHealth[]? Internals { get; set; }
	public HercPiece[]? ComponentData { get; set; }

	public InternalsHealth NewInternalsHealth() => new();
	public HercPiece NewHercPiece() => new();
	public InternalsTarget NewInternalsTarget() => new();

	public class HercPiece {
		public short Armor { get; set; }
		public short DebrisFlags { get; set; }
		public byte BoneId { get; set; }

		/// <summary>
		/// Was <c>Unk_val</c> — resolved via DBSIM.EXE disassembly of this record's
		/// consumers (<c>FUN_0040da38</c>/<c>FUN_0040d434</c>, see
		/// docs/simulation/damage-system.md). A bitfield: bit 0 = this piece has dependents
		/// to cascade-destroy (checked before walking the dependency list); bit 1 = selects an
		/// alternate destruction-effect callback mode (0 vs 2, passed to the same effect
		/// function); bit 2 = a one-shot "major destruction alert already played" latch (checked
		/// against a global, set once and never read back to false in this record); bit 3 =
		/// triggers a secondary effect callback that the alt-mode branch (bit 1) does not. Real
		/// per-piece values not yet surveyed across multiple files — bit meanings are confirmed
		/// by their code use, not by a labeled constant.
		/// </summary>
		public byte DestructionFlags { get; set; }

		public InternalsTarget[]? MappedInternals { get; set; }

		public HercPiece() { }

		public HercPiece(short boneCount) {
			MappedInternals = new InternalsTarget[boneCount];
		}
	}

	public class InternalsHealth {
		public short Id { get; set; }
		public short Armor { get; set; }
		public HercInternals? Name { get; set; }

		public InternalsHealth() { }

		public InternalsHealth(short id, short armor) {
			Id = id;
			Armor = armor;
		}
	}

	public class InternalsTarget {
		/// <summary>Defaults to 0x14 (20) in every known example.</summary>
		public short CritChance { get; set; } = 20;

		public HercInternals? InternalsId { get; set; }

		public InternalsTarget() { }

		public InternalsTarget(short critChance, HercInternals internalsId) {
			CritChance = critChance;
			InternalsId = internalsId;
		}
	}
}
