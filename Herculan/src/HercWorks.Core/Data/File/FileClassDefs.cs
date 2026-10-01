using T_ArmHerc = HercWorks.Core.Data.File.Dat.Shell.ArmHerc;
using T_ArmWeap = HercWorks.Core.Data.File.Dat.Shell.ArmWeap;
using T_BeamData = HercWorks.Core.Data.File.Dat.Sim.BeamData;
using T_BulletData = HercWorks.Core.Data.File.Dat.Sim.BulletData;
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
using T_PaperDollGraphic = HercWorks.Core.Data.File.Dbsim.PaperDollGraphic;
using T_ProjectileData = HercWorks.Core.Data.File.Dat.Sim.ProjectileData;
using T_RocketData = HercWorks.Core.Data.File.Dat.Sim.RocketData;
using T_RprHerc = HercWorks.Core.Data.File.Dat.Shell.RprHerc;
using T_TrainingHercs = HercWorks.Core.Data.File.Dat.Shell.TrainingHercs;
using T_Weapons = HercWorks.Core.Data.File.Dat.Sim.Weapons;
using T_WeaponsDat = HercWorks.Core.Data.File.Dat.Shell.WeaponsDat;

namespace HercWorks.Core.Data.File;

/// <summary>
/// Binds a file to the model class that reads it, independent of its name. DBSIM and VSHELL each
/// load fixed paths, so a file's folder and name are what say which format it is — and names do not
/// always tell: <c>GAM\ARM_OUTL.DAT</c> (VSHELL) and <c>DAT\OUTLAW.DAT</c> (DBSIM) are both
/// <c>.DAT</c>. An outside tool that lets a user name files freely binds them through this list.
/// The <c>T_*</c> aliases above keep each field's name apart from the class it points to.
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
	public static readonly FileClassDefs BulletData = new("BulletData", typeof(T_BulletData));
	public static readonly FileClassDefs RocketData = new("RocketData", typeof(T_RocketData));
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
