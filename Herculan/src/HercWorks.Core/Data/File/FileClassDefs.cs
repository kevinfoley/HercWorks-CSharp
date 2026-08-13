using T_ArmHerc = HercWorks.Core.Data.File.Dat.Shell.ArmHerc;
using T_ArmWeap = HercWorks.Core.Data.File.Dat.Shell.ArmWeap;
using T_BeamData = HercWorks.Core.Data.File.Dat.Sim.BeamData;
using T_CareerMissions = HercWorks.Core.Data.File.Dat.Shell.CareerMissions;
using T_DamageRepairCost = HercWorks.Core.Data.File.Dat.Shell.DamageRepairCost;
using T_FlightModel = HercWorks.Core.Data.File.Dbsim.FlightModel;
using T_GunLayout = HercWorks.Core.Data.File.Dbsim.GunLayout;
using T_HardpointOverlayConfig = HercWorks.Core.Data.File.Dat.Shell.HardpointOverlayConfig;
using T_HercInf = HercWorks.Core.Data.File.Dat.Shell.HercInf;
using T_Hercs = HercWorks.Core.Data.File.Dat.Shell.Hercs;
using T_HercSimDamage = HercWorks.Core.Data.File.Dbsim.HercSimDamage;
using T_HercSimDat = HercWorks.Core.Data.File.Dat.Sim.HercSimDat;
using T_InitHerc = HercWorks.Core.Data.File.Dat.Shell.InitHerc;
using T_MissileDatFile = HercWorks.Core.Data.File.Dat.Sim.MissileDatFile;
using T_PaperDollGraphic = HercWorks.Core.Data.File.Dbsim.PaperDollGraphic;
using T_ProjectileData = HercWorks.Core.Data.File.Dat.Sim.ProjectileData;
using T_RprHerc = HercWorks.Core.Data.File.Dat.Shell.RprHerc;
using T_TrainingHercs = HercWorks.Core.Data.File.Dat.Shell.TrainingHercs;
using T_Weapons = HercWorks.Core.Data.File.Dat.Sim.Weapons;
using T_WeaponsDat = HercWorks.Core.Data.File.Dat.Shell.WeaponsDat;

namespace HercWorks.Core.Data.File;

/// <summary>
/// Why: DBSIM and VSHELL are hardcoded to load specific files, with specific names and
/// extensions — each exe knows which files to pull from which folders. Any outside program
/// doesn't have this limitation or meta-info.
/// Example: /GAM/ARM_OUTL.DAT and /DAT/OUTLAW.DAT are both '.dat' files, but /GAM/ is for VSHELL
/// and /DAT/ is DBSIM. Worse, only /ARM_OUTL.DAT has any sort of unique key-phrase in the file
/// name. To let other users name their files however they'd like while still binding to a known
/// ES2 file type, here's FileClassDefs.
/// Ported from org.hercworks.core.data.file.FileClassDefs. Java's Class&lt;? extends DataFile&gt;
/// maps to System.Type here. Type aliases (T_*) above avoid ambiguity between each static
/// field's name and the identically-named class it points to (mirroring the Java enum, whose
/// constants are also named the same as their bound class).
/// </summary>
public sealed class FileClassDefs {
	// SHELL
	public static readonly FileClassDefs ArmHerc = new("ArmHerc", typeof(T_ArmHerc));
	public static readonly FileClassDefs ArmWeap = new("ArmWeap", typeof(T_ArmWeap));
	public static readonly FileClassDefs CareerMissions = new("CareerMissions", typeof(T_CareerMissions));
	public static readonly FileClassDefs DamageRepairCost = new("DamageRepairCost", typeof(T_DamageRepairCost));
	public static readonly FileClassDefs HardpointOverlay = new("HardpointOverlay", typeof(T_HardpointOverlayConfig));
	public static readonly FileClassDefs HercInf = new("HercInfo", typeof(T_HercInf));
	public static readonly FileClassDefs Hercs = new("Hercs", typeof(T_Hercs));
	public static readonly FileClassDefs InitHerc = new("InitHerc", typeof(T_InitHerc));
	public static readonly FileClassDefs RprHerc = new("RepairHerc", typeof(T_RprHerc));
	public static readonly FileClassDefs TrainingHercs = new("TrainingHercs", typeof(T_TrainingHercs));
	public static readonly FileClassDefs WeaponsDat = new("ShellWeaponsDat", typeof(T_WeaponsDat));

	// SIM
	public static readonly FileClassDefs BeamData = new("BeamData", typeof(T_BeamData));
	public static readonly FileClassDefs HercSimData = new("HercSimData", typeof(T_HercSimDat));
	public static readonly FileClassDefs MissileData = new("MissileData", typeof(T_MissileDatFile));
	public static readonly FileClassDefs ProjectileData = new("ProjectileData", typeof(T_ProjectileData));
	public static readonly FileClassDefs WeaponsSimData = new("WeaponsSimData", typeof(T_Weapons));
	public static readonly FileClassDefs FlightModel = new("FlightModel", typeof(T_FlightModel));
	public static readonly FileClassDefs GunLayout = new("GunLayout", typeof(T_GunLayout));
	public static readonly FileClassDefs HercSimDamageInfo = new("HercSimDamageInfo", typeof(T_HercSimDamage));
	public static readonly FileClassDefs PaperDollGraphic = new("PaperDollGraphic", typeof(T_PaperDollGraphic));

	public string Val { get; }
	public Type ClassType { get; }

	private FileClassDefs(string val, Type type) {
		Val = val;
		ClassType = type;
	}
}
