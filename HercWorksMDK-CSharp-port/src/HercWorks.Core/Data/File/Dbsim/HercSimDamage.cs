using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - dmg\[herc].DMG — armor, critical component HP, and other damage-related data per unit,
/// tied to the unit by name from the corresponding .DAT file.
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

public class HercSimDamage : DataFile {
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
		public byte Unk_val { get; set; }
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
