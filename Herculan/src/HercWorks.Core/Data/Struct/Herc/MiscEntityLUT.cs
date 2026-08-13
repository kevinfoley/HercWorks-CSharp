namespace HercWorks.Core.Data.Struct.Herc;

/// <summary>Ported from org.hercworks.core.data.struct.herc.MiscEntityLUT.</summary>
public sealed class MiscEntityLUT {
	public static readonly MiscEntityLUT SupplyDepot = new(0, "Supply Depot");
	public static readonly MiscEntityLUT HercFactory = new(1, "Herc Factory");
	public static readonly MiscEntityLUT MiningFacility = new(2, "Mining Facility");
	public static readonly MiscEntityLUT Generator = new(3, "Generator");
	public static readonly MiscEntityLUT Hangar = new(4, "Hangar");
	public static readonly MiscEntityLUT ListeningPost = new(5, "Listening Post");
	public static readonly MiscEntityLUT RadarTower = new(6, "Radar Tower");
	public static readonly MiscEntityLUT Refinery = new(7, "Refinery");
	public static readonly MiscEntityLUT GunTower = new(8, "Gun Tower");
	public static readonly MiscEntityLUT PowerStation = new(9, "Power Station");
	// 0A - 10 - CRASH
	public static readonly MiscEntityLUT MissileTower = new(11, "Missile Tower");
	public static readonly MiscEntityLUT BridgePylon = new(12, "Bridge");
	public static readonly MiscEntityLUT UnkBuilding = new(13, "Building");
	public static readonly MiscEntityLUT SmokeStack = new(14, "Smoke Stack");
	public static readonly MiscEntityLUT OilTank = new(15, "Oil Tank");
	public static readonly MiscEntityLUT ControlTower = new(16, "Control Tower");
	public static readonly MiscEntityLUT LargeBunker = new(17, "Large Bunker");
	public static readonly MiscEntityLUT SmallBunker = new(18, "Small Bunker");
	public static readonly MiscEntityLUT FuelTankLong = new(19, "Fuel Tank");
	public static readonly MiscEntityLUT ConduitCorner = new(20, "Conduit");
	public static readonly MiscEntityLUT RuinsA = new(21, "Building");
	public static readonly MiscEntityLUT RuinsB = new(22, "Building");
	public static readonly MiscEntityLUT RuinsC = new(23, "Building");
	public static readonly MiscEntityLUT SupplyDepotCybrid = new(24, "Supply Depot");
	public static readonly MiscEntityLUT FactoryCybrid = new(25, "Factory");
	public static readonly MiscEntityLUT MiningFacilityCybrid = new(26, "Mining Facility");
	public static readonly MiscEntityLUT GeneratorCybrid = new(27, "Generator");
	public static readonly MiscEntityLUT HangarCybrid = new(28, "Hangar");
	public static readonly MiscEntityLUT ListeningPostCybrid = new(29, "Listening Post");
	public static readonly MiscEntityLUT RadarTowerCybrid = new(30, "Radar Tower");
	public static readonly MiscEntityLUT RefineryCybrid = new(31, "Refinery");
	public static readonly MiscEntityLUT GunTowerCybrid = new(32, "Guntower");
	public static readonly MiscEntityLUT PowerStationCybrid = new(33, "Power Station");
	public static readonly MiscEntityLUT Transport = new(34, "Transport");
	public static readonly MiscEntityLUT MissileTowerCybrid = new(35, "Missile Tower");
	public static readonly MiscEntityLUT BaseCore = new(36, "Base Core");
	public static readonly MiscEntityLUT BuildingCybrid = new(37, "Building");
	public static readonly MiscEntityLUT Conduit = new(38, "Conduit");
	public static readonly MiscEntityLUT FuelTank = new(39, "Fuel Tank");
	public static readonly MiscEntityLUT ControlTowerCybrid = new(40, "Control Tower");
	public static readonly MiscEntityLUT OilTankCybrid = new(41, "Oil Tank");
	public static readonly MiscEntityLUT UnkBuilding1 = new(42, "Building");
	public static readonly MiscEntityLUT UnkBuilding2 = new(43, "Building"); // looks like power station, non-animated
	public static readonly MiscEntityLUT UnkBuilding3 = new(44, "Building"); // looks like power station, non-animated
	public static readonly MiscEntityLUT SupplyTransport1 = new(45, "Supply Transport");
	public static readonly MiscEntityLUT SupplyTransport2 = new(46, "Supply Transport");
	// 47 - CRASH
	public static readonly MiscEntityLUT MobileCannon = new(48, "Mobile Cannon");
	public static readonly MiscEntityLUT ArmoredAssault = new(49, "Armored Assault");
	public static readonly MiscEntityLUT MobileMissile = new(50, "Mobile Missile");
	public static readonly MiscEntityLUT MobileMissileJeep = new(51, "Mobile Missile");
	public static readonly MiscEntityLUT MobileMissileRazor = new(52, "Mobile Missile");
	// 53, 54 - CRASH
	public static readonly MiscEntityLUT SupplyTransport3 = new(55, "Supply Transport");
	public static readonly MiscEntityLUT SupplyTransport4 = new(56, "Supply Transport");
	public static readonly MiscEntityLUT MobileMissileCybrid = new(57, "Mobile Missile");
	public static readonly MiscEntityLUT MobileCannonCybrid = new(58, "Mobile Cannon");
	public static readonly MiscEntityLUT ArmoredAssault2 = new(59, "Armored Assault");
	public static readonly MiscEntityLUT MobileMissileCybrid2 = new(60, "Mobile Missile");
	public static readonly MiscEntityLUT MobileMissileJeep2 = new(61, "Mobile Missile");
	// 62, 63 - CRASH

	private static readonly IReadOnlyList<MiscEntityLUT> All = new[]
	{
		SupplyDepot, HercFactory, MiningFacility, Generator, Hangar, ListeningPost, RadarTower,
		Refinery, GunTower, PowerStation, MissileTower, BridgePylon, UnkBuilding, SmokeStack,
		OilTank, ControlTower, LargeBunker, SmallBunker, FuelTankLong, ConduitCorner, RuinsA,
		RuinsB, RuinsC, SupplyDepotCybrid, FactoryCybrid, MiningFacilityCybrid, GeneratorCybrid,
		HangarCybrid, ListeningPostCybrid, RadarTowerCybrid, RefineryCybrid, GunTowerCybrid,
		PowerStationCybrid, Transport, MissileTowerCybrid, BaseCore, BuildingCybrid, Conduit,
		FuelTank, ControlTowerCybrid, OilTankCybrid, UnkBuilding1, UnkBuilding2, UnkBuilding3,
		SupplyTransport1, SupplyTransport2, MobileCannon, ArmoredAssault, MobileMissile,
		MobileMissileJeep, MobileMissileRazor, SupplyTransport3, SupplyTransport4,
		MobileMissileCybrid, MobileCannonCybrid, ArmoredAssault2, MobileMissileCybrid2,
		MobileMissileJeep2
	};

	private static readonly Dictionary<short, MiscEntityLUT> ById = All.ToDictionary(m => m.Id);

	public string Name { get; set; }
	public short Id { get; set; }

	private MiscEntityLUT(short id, string name) {
		Name = name;
		Id = id;
	}

	public static MiscEntityLUT? GetById(short id) => ById.GetValueOrDefault(id);

	public override string ToString() => Name;
}
