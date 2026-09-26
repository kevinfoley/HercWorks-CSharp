using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Sav;

namespace Herculan.Engine.Shell;

/// <summary>
/// One machine in a hangar bay, as the shell's screens read it — <c>DAT_00482ac3</c>'s eight pointers,
/// each to the 122-byte HERC record in the loaded save (docs/formats/save-games.md).
///
/// <para><b>The 66-byte status block is three arrays and one accessor.</b> <c>HercStatus_Get</c>
/// (<c>FUN_00411d06</c>) takes a mode and an index and every screen in the shell reads a machine's
/// damage through it, so <see cref="Condition"/> is the whole of what the repair bay needs.
/// <see cref="ShellRepairCategory.ExternalGroup"/> is the one that is not a plain array read: the 13
/// external entries are facets and a group is their mean, taken through the six-group table at
/// <c>0046f8a4</c>.</para>
/// </summary>
public sealed class ShellBayMachine {
	/// <summary>
	/// <c>HercComponentGroupTable</c> (<c>0046f8a4</c>) — which of the 13 external facets each of the
	/// six named groups covers. The six partition all 13 exactly once, which is what makes the group
	/// the granularity the repair bay and the scrap screen price at.
	/// </summary>
	public static readonly int[][] ExternalGroupFacets = {
		new[] { 0, 1 },        // Cockpit: front, rear
		new[] { 2, 4 },        // Left Torso: front, rear
		new[] { 3, 5 },        // Right Torso: front, rear
		new[] { 6 },           // Chassis
		new[] { 7, 9, 11 },    // Left Leg: thigh, calf, foot
		new[] { 8, 10, 12 },   // Right Leg: thigh, calf, foot
	};

	/// <summary>
	/// The four internals <c>FUN_00411681</c> tests for a machine to count as deployable: both leg
	/// servos, the engine and life support. See <see cref="IsFlightworthy"/>.
	/// </summary>
	private static readonly int[] FlightworthyInternals = { 0, 1, 5, 8 };

	/// <summary>The condition each of those four must be strictly above.</summary>
	private const int FlightworthyFloor = 50;

	/// <summary>A machine is deliverable only at 100% built; anything less is still in the workshop.</summary>
	private const int Complete = 100;

	private readonly short[] _external;
	private readonly short[] _internal;
	private readonly short[] _hardpoint;
	private readonly short[] _weaponId;

	private ShellBayMachine(int chassisType, int mountCapacity, int buildPercent, short[] external,
			short[] internals, short[] hardpoint, short[] weaponId) {
		ChassisType = chassisType;
		MountCapacity = mountCapacity;
		BuildPercent = buildPercent;
		_external = external;
		_internal = internals;
		_hardpoint = hardpoint;
		_weaponId = weaponId;
	}

	/// <summary>
	/// The chassis type, 0-8. The record carries it twice and the two coincide — <c>+0x02</c> is
	/// <c>+0x00</c> through an identity map — and this is the one the stat tables, the cost tables and
	/// the <c>estext.bin</c> name are all indexed by.
	/// </summary>
	public int ChassisType { get; }

	/// <summary>The mount capacity at <c>+0x4c</c>: how many hardpoint rows the repair list offers.</summary>
	public int MountCapacity { get; }

	/// <summary>Build progress at <c>+0x4a</c>. Construction state, not damage.</summary>
	public int BuildPercent { get; }

	/// <summary>Whether the machine has been delivered — <c>FUN_00410a64</c>'s test.</summary>
	public bool IsBuilt => BuildPercent == Complete;

	/// <summary>
	/// <c>FUN_00411681</c> — whether the machine can be flown: both leg servos, the engine and life
	/// support all above 50. A machine that fails this is still in its bay but does not count towards
	/// the fleet the scrap gate measures.
	/// </summary>
	public bool IsFlightworthy {
		get {
			foreach (int component in FlightworthyInternals) {
				if (Condition(ShellRepairCategory.Internal, component) <= FlightworthyFloor) {
					return false;
				}
			}

			return true;
		}
	}

	/// <summary>
	/// <c>HercStatus_Get(block, category, index)</c>. An index the machine does not have reads 100,
	/// which is what an absent machine reads throughout the repair screen too.
	/// </summary>
	public int Condition(ShellRepairCategory category, int index) {
		if (index < 0) {
			return Complete;
		}

		switch (category) {
			case ShellRepairCategory.ExternalGroup:
				return index < ExternalGroupFacets.Length ? GroupMean(index) : Complete;
			case ShellRepairCategory.Internal:
				return index < _internal.Length ? _internal[index] : Complete;
			default:
				return index < _hardpoint.Length ? _hardpoint[index] : Complete;
		}
	}

	/// <summary>
	/// The weapon id fitted in one mount slot, or 0 for an empty slot — the first <c>int16</c> of the
	/// mount record at <c>+0x50 + slot*4</c>, which every screen reads the same way.
	/// </summary>
	public int WeaponAt(int slot) => slot >= 0 && slot < _weaponId.Length ? _weaponId[slot] : 0;

	/// <summary>
	/// <c>HercStatus_OverallCondition</c> (<c>00411bd4</c>) — all 13 external facets, the nine
	/// internals and one entry per mount slot up to the capacity, over <c>capacity + 22</c>. It is
	/// called with <c>+0x4c</c>, so an empty slot's 100 counts towards the mean.
	/// </summary>
	public int OverallCondition {
		get {
			int total = 0;
			foreach (short facet in _external) {
				total += facet;
			}

			foreach (short component in _internal) {
				total += component;
			}

			for (int slot = 0; slot < MountCapacity; slot++) {
				total += Condition(ShellRepairCategory.Hardpoint, slot);
			}

			return total / (MountCapacity + _external.Length + _internal.Length);
		}
	}

	/// <summary>
	/// <c>HercStatus_GroupMean</c> (<c>00411c29</c>) — a group's facets summed and divided by how many
	/// there are, integer division as the original does it.
	/// </summary>
	private int GroupMean(int group) {
		int[] facets = ExternalGroupFacets[group];
		int total = 0;
		foreach (int facet in facets) {
			total += facet < _external.Length ? _external[facet] : Complete;
		}

		return total / facets.Length;
	}

	/// <summary>Builds the view over one parsed bay record.</summary>
	public static ShellBayMachine From(HercBayEntry entry) {
		var external = new short[HercExternals.Values().Count];
		foreach (var facet in HercExternals.Values()) {
			external[facet.Id] = entry.HealthExternals != null
				&& entry.HealthExternals.TryGetValue(facet, out var part) ? part.Health : (short)Complete;
		}

		// Ids 0-8 are the nine named components; id 9 is the machine's overall condition and is not one
		// of them, so the repair list stops short of it.
		var internals = new short[DamageRepairCost.InternalCount];
		for (int i = 0; i < internals.Length; i++) {
			var component = HercInternals.GetById((short)i);
			internals[i] = component != null && entry.HealthInternals != null
				&& entry.HealthInternals.TryGetValue(component, out var part) ? part.Health : (short)Complete;
		}

		var hardpoint = new short[entry.HealthHardpoints.Length];
		var weaponId = new short[entry.HealthHardpoints.Length];
		for (int slot = 0; slot < hardpoint.Length; slot++) {
			hardpoint[slot] = entry.HealthHardpoints[slot]?.Health ?? (short)Complete;
			weaponId[slot] = entry.Weapons.TryGetValue((short)slot, out var weapon) && weapon.Id != null
				? (short)weapon.Id.Id : (short)0;
		}

		return new ShellBayMachine(entry.Id?.Id ?? 0, entry.HardpointMax, entry.BuildPercent,
			external, internals, hardpoint, weaponId);
	}
}

/// <summary>One pilot record as the shell's screens print it (docs/formats/save-games.md, "The pilot record").</summary>
/// <param name="RosterId">
/// <c>+0x00</c>, 0-11 — also the pilot's portrait, the frame of <c>dba\c_pilots.dba</c> the crew screen shows.
/// </param>
/// <param name="Bay">The hangar bay at <c>+0x22</c>, <c>-1</c> when unassigned.</param>
/// <param name="Skill">The skill ladder at <c>+0x25</c>, 0-3; the panel prints <c>estext.bin</c> <c>0x35 + skill</c>.</param>
/// <param name="SquadPosition">
/// <c>+0x27</c> — which of the crew screen's three wingman rows, 1-3, the pilot fills, or <c>-1</c>.
/// </param>
public sealed record ShellBayPilot(string Name, int RosterId, int Bay, int Skill, int SquadPosition);

/// <summary>
/// The eight hangar bays and the salvage pool — what every tab from WEAPONS to CREW works over, and
/// what the repair screen in particular reads a machine out of.
///
/// <para>VSHELL keeps the bays as eight pointers at <c>00482ac3</c>, null for an empty bay, with the
/// selected slot in <c>DAT_00482ae5</c> (<c>-1</c> for none). Sparse is normal: a save really can have
/// a machine in bay 3 and nothing in bay 2, so the bays are addressed by index rather than packed.</para>
/// </summary>
public sealed class ShellHangar {
	/// <summary>How many bays there are. Every loop in the shell that walks them bounds at eight.</summary>
	public const int BayCount = 8;

	/// <summary>The squad block's shape: three squads of twelve pilot records, one squad member drawn from each.</summary>
	private const int SquadCount = 3;
	private const int PilotsPerSquad = 12;

	private readonly ShellBayMachine?[] _bays = new ShellBayMachine?[BayCount];
	private readonly List<ShellBayPilot> _squad = new();
	private readonly HashSet<int> _availableChassis = new();

	private ShellHangar() { }

	/// <summary>The salvage pool in kilograms, as the save carries it.</summary>
	public int SalvageKilograms { get; private set; }

	/// <summary>The player's own pilot record, embedded in the player structure at <c>+0x04</c>.</summary>
	public ShellBayPilot? Player { get; private set; }

	/// <summary>
	/// The three squad members the player structure points at from <c>+0x3f</c>, in pointer order —
	/// the pilots the crew screen offers as <c>Available Pilots</c>. Fewer than three only when the
	/// save's squad block is short.
	/// </summary>
	public IReadOnlyList<ShellBayPilot> SquadMembers => _squad;

	/// <summary>
	/// The player structure's leading <c>int16</c>, <c>DAT_00482a78</c>: how many squad positions,
	/// counting the player's as position 0, are in play. The auto-repair pass and the
	/// <c>player.mec</c> export both bound their per-position loops by it, a squad member counts as on
	/// strength only in a position below it (<c>FUN_00410366</c>), and the crew screen draws the rows
	/// below it lit.
	/// </summary>
	public int SquadPositions { get; private set; }

	/// <summary>
	/// <c>FUN_004102d6</c> — the squad member whose <c>+0x27</c> is <paramref name="position"/>, or null.
	/// It tests the three in pointer order and takes the first.
	/// </summary>
	public ShellBayPilot? SquadMemberAt(int position) {
		foreach (var pilot in _squad) {
			if (pilot.SquadPosition == position) {
				return pilot;
			}
		}

		return null;
	}

	/// <summary>The machine in one bay, or null when the bay is empty or the index is out of range.</summary>
	public ShellBayMachine? Bay(int slot) => slot >= 0 && slot < BayCount ? _bays[slot] : null;

	/// <summary>
	/// <c>FUN_00410220(00482a78, slot)</c> — the pilot assigned to a bay, or null. It searches exactly
	/// four records: the player's own, then the three squad members the player structure points at
	/// (<c>+0x3f</c>). Each of those pointers is set on load to record <c>DAT_00483b48[k]</c> of squad
	/// <c>k</c>, so a pilot in the squad block who is not one of the three is never found here.
	/// </summary>
	public ShellBayPilot? PilotFor(int slot) {
		if (slot < 0) {
			return null;
		}

		if (Player?.Bay == slot) {
			return Player;
		}

		foreach (var pilot in _squad) {
			if (pilot.Bay == slot) {
				return pilot;
			}
		}

		return null;
	}

	/// <summary>
	/// A chassis type's availability flag — save block 7, <c>herc_inf.dat</c> record <c>+0x0e</c> at
	/// <c>(&amp;DAT_00483b62)[type * 8]</c>, the flag <c>Herc_GrantUnlocks</c> (<c>004118c5</c>) sets.
	/// </summary>
	public bool IsChassisAvailable(int chassisType) => _availableChassis.Contains(chassisType);

	/// <summary>
	/// <c>FUN_00410c2a</c> — the first bay holding a fully built machine, or <c>-1</c> when there is
	/// none. It is what the repair screen falls back to when the selected bay holds nothing it can
	/// work on.
	/// </summary>
	public int FirstBuiltBay() {
		for (int slot = 0; slot < BayCount; slot++) {
			if (_bays[slot] is { IsBuilt: true }) {
				return slot;
			}
		}

		return -1;
	}

	/// <summary>
	/// <c>FUN_00410add</c> — whether exactly one bay holds a machine that is built and deployable.
	/// The repair screen's SCRAP button is dead while this holds, which is what stops a player
	/// scrapping the last thing they can fly.
	/// </summary>
	public bool HasSingleDeployable() {
		int count = 0;
		for (int slot = 0; slot < BayCount; slot++) {
			if (_bays[slot] is { IsBuilt: true, IsFlightworthy: true }) {
				count++;
			}
		}

		return count == 1;
	}

	/// <summary>
	/// The bays out of an already-parsed save. Returns an empty hangar for a null save, which is what
	/// the screen draws its furniture against when there is nothing to load.
	/// </summary>
	public static ShellHangar From(PlayerSave? save) {
		var hangar = new ShellHangar();
		if (save == null) {
			return hangar;
		}

		hangar.SalvageKilograms = save.SalvageTotal;
		foreach (var (slot, entry) in save.HercBay) {
			if (slot >= 0 && slot < BayCount && entry != null) {
				hangar._bays[slot] = ShellBayMachine.From(entry);
			}
		}

		foreach (var (herc, flag) in save.UnlockedHercs) {
			if (flag != 0) {
				hangar._availableChassis.Add(herc.Id);
			}
		}

		// FUN_004101b8 reads the player block's two leading shorts into 00482a78 and 00482a7a, then
		// points the player structure's three squad pointers at record DAT_00483b48[k] of squad k. The
		// save model keeps all five among the eight shorts between the squad block and the player's
		// record: DAT_00483b48's three first, the player block's two last.
		hangar.SquadPositions = save.UnkRange_prePlayer[SquadPositionsShort];
		hangar.Player = Pilot(save.PlayerPilot);
		for (int squad = 0; squad < SquadCount; squad++) {
			int member = save.UnkRange_prePlayer[squad];
			if (member >= 0 && member < PilotsPerSquad
				&& Pilot(save.Squadmates?.ElementAtOrDefault(squad * PilotsPerSquad + member)) is { } pilot) {
				hangar._squad.Add(pilot);
			}
		}

		return hangar;
	}

	/// <summary>Where <c>DAT_00482a78</c> sits among the save model's eight shorts before the player's record.</summary>
	private const int SquadPositionsShort = 6;

	private static ShellBayPilot? Pilot(PilotEntry? pilot) =>
		pilot == null ? null
			: new ShellBayPilot((pilot.Name ?? string.Empty).TrimEnd('\0'), pilot.SquadmateId, pilot.BayId,
				pilot.Skill?.Id ?? 0, pilot.CrewRowNum);
}
