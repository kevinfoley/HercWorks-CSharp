using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform;

/// <summary>
/// Maps a VolEntry (by directory + extension + file name) to the ThreeSpaceByteTransformer that
/// knows how to parse it, so callers — chiefly the WinForms VOL browser — can show formatted
/// content for a selected file without needing to know every file-naming convention themselves.
///
/// Deliberately conservative: only covers file types this project actually has a real, working
/// transformer for (see HercWorks.Core.Io.Transform.{Common,Dbsim,Shell}). Plenty of other file
/// types exist in the game data with a parsed C# data-model class but no ported
/// ThreeSpaceByteTransformer (e.g. MapInfo, MapLOCS, Theater, Mech*.BND, WorldData) — those
/// intentionally have no registration here and will report "no parser available" rather than
/// risk a wrong/guessed match. A couple of borderline cases were left out for the same reason:
/// HercSimDataTransformer's target ("dat\[herc].dat") is too ambiguous to distinguish reliably
/// from other .DAT files without a more specific real-world naming sample, and
/// HardpointOverlayTransformer is only confirmed here for RPR_HOTS.DAT — its doc comment doesn't
/// establish it also covers ARM_HOTS.DAT, so that file is left unmatched rather than guessed.
/// </summary>
public static class TransformerRegistry {
	private sealed record Registration(string Label, Func<VolEntry, bool> Matches, Func<ThreeSpaceByteTransformer> Create);

	private static readonly List<Registration> Registrations = new() {
		// --- shell (Dir=Gam, Ext=Dat) ---
		new("Herc Armory Panel", e => NameStartsWith(e, "ARM_") && !NameIs(e, "ARM_WEAP.DAT") && !NameIs(e, "ARM_HOTS.DAT"),
			() => new Shell.ArmHercTransformer()),
		new("Armory Weapon Icons", e => NameIs(e, "ARM_WEAP.DAT"), () => new Shell.ArmWeapTransformer()),
		new("Career Missions", e => NameIs(e, "CAREER.DAT"), () => new Shell.CareerDataTransformer()),
		new("Repair Hardpoint Overlay", e => NameIs(e, "RPR_HOTS.DAT"), () => new Shell.HardpointOverlayTransformer()),
		new("Herc Info", e => NameIs(e, "HERC_INF.DAT"), () => new Shell.HercInfoTransformer()),
		new("Starting Hercs", e => NameIs(e, "HERCS.DAT"), () => new Shell.HercsStartTransformer()),
		new("Herc Init Data", e => NameStartsWith(e, "INI_"), () => new Shell.InitHercTransformer()),
		new("Repair Herc Panel", e => NameStartsWith(e, "RPR_") && !NameIs(e, "RPR_HOTS.DAT"), () => new Shell.RprHercTransform()),
		new("Training Hercs", e => NameIs(e, "TRN_HERCS.DAT"), () => new Shell.TrainingHercsTransform()),
		new("Weapons Catalog", e => NameIs(e, "WEAPONS.DAT") && DirIs(e, FileType.Gam), () => new Shell.WeaponsDatTransformer()),

		// --- dbsim (various dirs) ---
		new("Beam Data", e => NameIs(e, "BEAM.DAT"), () => new Dbsim.BeamDatFileTransformer()),
		new("Herc Collider", e => ExtIs(e, FileType.Col), () => new Dbsim.HercColliderTransformer()),
		new("3D Model (DTS)", e => ExtIs(e, FileType.Dts), () => new Dbsim.DTSModelTransformer()),
		new("Herc Debris Data", e => NameEndsWith(e, "_DEB.DAT"), () => new Dbsim.DebrisHercTransformer()),
		new("Flight Model", e => ExtIs(e, FileType.Fm), () => new Dbsim.FlightModelTransformer()),
		new("Gun Layout", e => ExtIs(e, FileType.Gl), () => new Dbsim.GunLayoutTransformer()),
		new("Herc Damage Data", e => ExtIs(e, FileType.Dmg), () => new Dbsim.HercDamageFileTransformer()),
		new("Missile/Bullet Data", e => NameIs(e, "BULLETS.DAT") || NameIs(e, "ROCKETS.DAT"), () => new Dbsim.MissileDatFileTransformer()),
		new("Weapons Paper Diagram", e => NameIs(e, "WEAPONS.PDG"), () => new Dbsim.WeaponPDGTransformer()),
		new("Paper Diagram Graphic", e => ExtIs(e, FileType.Pdg) && !NameIs(e, "WEAPONS.PDG"), () => new Dbsim.PaperDiagramGraphTransformer()),
		new("Projectile Data", e => NameIs(e, "PROJ.DAT"), () => new Dbsim.ProjectileDataTransformer()),
		new("Viewport Data", e => ExtIs(e, FileType.Vue), () => new Dbsim.VueTransformer()),

		// --- common ---
		new("Dynamix Bitmap Array", e => ExtIs(e, FileType.Dba), () => new Common.DynamixBitmapArrayTransformer()),
		new("Dynamix Bitmap", e => ExtIs(e, FileType.Dbm), () => new Common.DynamixBitmapTransformer()),
		new("Dynamix Palette", e => ExtIs(e, FileType.Dpl), () => new Common.DynamixPaletteTransformer()),
		new("Mission File", e => ExtIs(e, FileType.Msn), () => new Common.MissionFileTransformer()),
		new("Player Save", e => ExtIs(e, FileType.Sav), () => new Common.PlayerSaveTransform()),
		new("String Table", e => ExtIs(e, FileType.Str), () => new Common.StringFileTransformer()),

		// .HBA and .HB0/.HB1/.HB2 turned out to be byte-identical to the .DBA container format
		// (same 12-byte "01 00 28 00" + size + count header, same embedded DynamixBitmap-per-entry
		// layout, same 1-byte inter-entry padding) — confirmed against every real HBA/HB0/HB1/HB2
		// file in simvol0 by walking the existing DynamixBitmapArrayTransformer's exact algorithm
		// by hand. HB0/HB1/HB2 always parse to a single 640x480 frame (a full-screen cockpit
		// background, one per team color); HBA holds several smaller gauge/UI sprites.
		new("Herc Cockpit Bitmap Array", e => ExtIs(e, FileType.Hba), () => new Common.DynamixBitmapArrayTransformer()),
		new("Herc Cockpit Texture (640x480)", e => ExtIs(e, FileType.Hb0) || ExtIs(e, FileType.Hb1) || ExtIs(e, FileType.Hb2),
			() => new Common.DynamixBitmapArrayTransformer()),
	};

	/// <summary>Returns a fresh transformer instance for the given entry, or null if no known parser matches.</summary>
	public static ThreeSpaceByteTransformer? FindTransformer(VolEntry entry) {
		foreach (var reg in Registrations) {
			if (reg.Matches(entry)) {
				return reg.Create();
			}
		}
		return null;
	}

	/// <summary>Human-readable label for the matched file type, or null if no known parser matches.</summary>
	public static string? FindLabel(VolEntry entry) {
		foreach (var reg in Registrations) {
			if (reg.Matches(entry)) {
				return reg.Label;
			}
		}
		return null;
	}

	private static bool NameIs(VolEntry e, string name) =>
		string.Equals(e.FileName?.Trim(), name, StringComparison.OrdinalIgnoreCase);

	private static bool NameStartsWith(VolEntry e, string prefix) =>
		e.FileName != null && e.FileName.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

	private static bool NameEndsWith(VolEntry e, string suffix) =>
		e.FileName != null && e.FileName.Trim().EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

	private static bool ExtIs(VolEntry e, FileType ext) => e.Ext == ext;

	private static bool DirIs(VolEntry e, FileType dir) => e.Dir == dir;
}
