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

	private readonly ShellBayMachine?[] _bays = new ShellBayMachine?[BayCount];

	private ShellHangar() { }

	/// <summary>The salvage pool in kilograms, as the save carries it.</summary>
	public int SalvageKilograms { get; private set; }

	/// <summary>The machine in one bay, or null when the bay is empty or the index is out of range.</summary>
	public ShellBayMachine? Bay(int slot) => slot >= 0 && slot < BayCount ? _bays[slot] : null;

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

		return hangar;
	}
}
