using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - dmg\[herc].DMG — armor, critical component HP, and other damage-related data per unit,
/// tied to the unit by name from the corresponding .DAT file. See the Java source for the full
/// documented byte layout (internals array, component/debris-flag list, bone IDs, and the
/// crit-chance mapping table).
/// Ported from org.hercworks.core.data.file.dbsim.HercSimDamage.
/// </summary>
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
