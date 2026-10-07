using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Sav;

namespace Herculan.Engine.Shell;

/// <summary>
/// The eight hangar bays and the salvage pool — what every tab from WEAPONS to CREW works over, and
/// what the repair screen in particular reads a machine out of.
///
/// <para>VSHELL keeps the bays as eight pointers at <c>Hangar_BayRecords</c> (<c>00482ac3</c>), null for an empty bay, with the
/// selected slot in <c>SelectedBaySlot</c> (<c>00482ae5</c>) (<c>-1</c> for none). Sparse is normal: a save really can have
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

	/// <summary>
	/// The salvage pool in kilograms, <c>CareerSalvage</c> (<c>00482af4</c>): the save's, then moved by
	/// BUILD, SCRAP and the repair screen, whose CANCEL writes it back outright.
	/// </summary>
	public int SalvageKilograms { get; internal set; }

	/// <summary>
	/// The armory's stock of one weapon: its unlock flag, and the units it holds — the list at
	/// <c>weapons.dat</c> record <c>+0x19</c>, kept here with its head last. <c>Armory_AddUnit</c>
	/// (<c>00411efd</c>) pushes onto the head and <c>Armory_PopUnit</c> (<c>00411ec7</c>) takes it off, so the
	/// unit fitted next is the one that went in last.
	/// </summary>
	private sealed class WeaponStock {
		/// <summary>The unlock byte as the save holds it; any value but 0 is unlocked.</summary>
		public byte UnlockFlag;
		public readonly List<ShellWeaponUnit> Units = new();

		public bool Unlocked => UnlockFlag != 0;
	}

	private readonly Dictionary<int, WeaponStock> _stock = new();

	private WeaponStock Stock(int weaponId) {
		if (!_stock.TryGetValue(weaponId, out var stock)) {
			stock = new WeaponStock();
			_stock[weaponId] = stock;
		}

		return stock;
	}

	/// <summary>
	/// A weapon's unlock flag, <c>weapons.dat</c> record <c>+0x16</c> at <c>(&amp;DAT_00483bfa)[id * 0x1d]</c> — save
	/// block 1's leading byte for that id (docs/retail/formats/weapons-dat.md).
	/// </summary>
	public bool IsWeaponUnlocked(int weaponId) => _stock.TryGetValue(weaponId, out var entry) && entry.Unlocked;

	/// <summary>How many units of a weapon the armory holds, record <c>+0x17</c> at <c>DAT_00483bfb</c>.</summary>
	public int WeaponsOwned(int weaponId) => _stock.TryGetValue(weaponId, out var entry) ? entry.Units.Count : 0;

	/// <summary><c>Armory_AddUnit</c> (<c>00411efd</c>) — the unit onto the head of its weapon's list.</summary>
	private void AddUnit(ShellWeaponUnit unit) => Stock(unit.WeaponId).Units.Add(unit);

	/// <summary><c>Armory_PopUnit</c> (<c>00411ec7</c>) — the head of a weapon's list, or null when it is empty.</summary>
	private ShellWeaponUnit? PopUnit(int weaponId) {
		var units = Stock(weaponId).Units;
		if (units.Count == 0) {
			return null;
		}

		var unit = units[^1];
		units.RemoveAt(units.Count - 1);
		return unit;
	}

	/// <summary>
	/// <c>Herc_FitMount</c> (<c>004114ec</c>), which the weapons screen's row click runs through
	/// <c>Arming_FitSelected</c>: the unit in <paramref name="slot"/> goes back onto its weapon's stock
	/// list, and then <c>None</c> (0) leaves the slot empty at condition 100, and any other weapon takes
	/// the head of that weapon's list — its <see cref="ShellWeaponUnit.FitCondition"/> becomes the
	/// hardpoint's condition and its guidance is reset, to ARH for the three missile racks and to none for
	/// everything else, the Razor's launcher included. With that list empty the slot is left empty and
	/// its condition untouched. See docs/retail/shell/screen-layout.md#fitting-a-weapon.
	/// </summary>
	public void FitMount(ShellBayMachine machine, int slot, int weaponId) {
		if (machine.Mount(slot) is { } old) {
			AddUnit(old);
		}

		if (weaponId == 0) {
			machine.SetMount(slot, null);
			machine.SetCondition(ShellRepairCategory.Hardpoint, slot, 100);
			return;
		}

		var unit = PopUnit(weaponId);
		machine.SetMount(slot, unit);
		if (unit != null) {
			machine.SetCondition(ShellRepairCategory.Hardpoint, slot, unit.FitCondition);
			unit.Guidance = weaponId is >= FirstGuidedRack and <= LastGuidedRack ? ArhGuidance : ShellWeaponUnit.NoGuidance;
		}
	}

	/// <summary>The weapons <c>Herc_FitMount</c> fits with a guidance kind — the three missile racks, <c>0xd</c> to <c>0xf</c>.</summary>
	private const int FirstGuidedRack = 0xd;
	private const int LastGuidedRack = 0xf;
	private const int ArhGuidance = 1;

	/// <summary>The armory build queue's five slots, <c>0046f8d6</c>: one weapon id each, 0 for an empty slot.</summary>
	private readonly int[] _queue = new int[QueueSlots];

	/// <summary>How many slots the armory build queue has (docs/retail/shell/armory.md).</summary>
	public const int QueueSlots = 5;

	/// <summary>The queue's free-slot count, <c>0046f8d4</c> — the armory's <c>Workspace Available:</c>.</summary>
	public int QueueFreeSlots { get; private set; } = QueueSlots;

	/// <summary>The weapon id in each queue slot, 0 for an empty one.</summary>
	public IReadOnlyList<int> QueuedWeapons => _queue;

	/// <summary><c>Armory_QueuedCount</c> (<c>00412642</c>) — how many of the five queue slots hold <paramref name="weaponId"/>.</summary>
	public int QueuedCount(int weaponId) => _queue.Count(id => id == weaponId);

	/// <summary>
	/// <c>Armory_Enqueue</c> (<c>004125e7</c>) — the weapon into the first empty slot, one free slot fewer.
	/// Nothing while <see cref="QueueFreeSlots"/> is 0, which <c>Armory_FirstFreeSlot</c> (<c>004125bb</c>)
	/// tests before it looks at the slots.
	/// </summary>
	public void Enqueue(int weaponId) {
		if (QueueFreeSlots == 0) {
			return;
		}

		int slot = Array.IndexOf(_queue, 0);
		if (slot != -1) {
			_queue[slot] = weaponId;
			QueueFreeSlots--;
		}
	}

	/// <summary><c>Armory_Dequeue</c> (<c>0041260d</c>) — every slot holding the weapon emptied, one free slot more for each.</summary>
	public void Dequeue(int weaponId) {
		for (int slot = 0; slot < QueueSlots; slot++) {
			if (_queue[slot] == weaponId) {
				_queue[slot] = 0;
				QueueFreeSlots++;
			}
		}
	}

	/// <summary><c>Armory_ResetQueue</c> (<c>0041213d</c>) — five free slots, all empty.</summary>
	public void ResetQueue() {
		Array.Clear(_queue);
		QueueFreeSlots = QueueSlots;
	}

	/// <summary>
	/// <c>Armory_TrimQueueToBudget</c> (<c>004123ba</c>) — while what the queue has committed is more than
	/// the pool, compared unsigned, slots are emptied from the first, one free slot more for each.
	/// </summary>
	public void TrimQueueToBudget(Func<int, int> priceKilograms) {
		uint total = (uint)_queue.Where(id => id != 0).Sum(priceKilograms);
		for (int slot = 0; slot < QueueSlots && (uint)SalvageKilograms < total; slot++) {
			if (_queue[slot] != 0) {
				total -= (uint)priceKilograms(_queue[slot]);
				_queue[slot] = 0;
				QueueFreeSlots++;
			}
		}
	}

	/// <summary>
	/// The weapon scrap dialog's ACCEPT, <c>Armory_ScrapWeapons</c> (<c>0040e7b2</c>) through
	/// <c>Armory_ScrapStock</c> (<c>00412555</c>): every unit of the weapon the armory holds is freed and
	/// <paramref name="valueTons"/> times 1000 goes into the pool.
	/// </summary>
	public void ScrapStock(int weaponId, int valueTons) {
		Stock(weaponId).Units.Clear();
		SalvageKilograms += valueTons * ShellRepairCosts.KilogramsPerTon;
	}

	/// <summary>
	/// <c>WeaponGrant_UnlockSlots</c> (<c>0046fa82</c>) — per weapon id, the campaign-flag slot whose value
	/// unlocks it, <c>-1</c> for none.
	/// </summary>
	private static readonly short[] WeaponUnlockSlots = {
		-1, -1, -1, -1, 0x34, 0x35, -1, -1, -1, -1, -1, 0x32, 0x33, -1, -1, -1, -1,
		-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0x39, 0x38, 0x3a, 0x3b,
	};

	/// <summary><c>WeaponGrant_UnlockValues</c> (<c>0046fa40</c>) — per weapon id, the value that slot must hold.</summary>
	private static readonly short[] WeaponUnlockValues = {
		0, 0, 0, 0, 2, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 1,
	};

	/// <summary>The first campaign-flag slot the unit-grant pass reads.</summary>
	private const int FirstUnitGrantFlag = 0x15;

	/// <summary>
	/// <c>WeaponGrant_UnitIds</c> (<c>0046f8e0</c>) — the weapon each flag from <see cref="FirstUnitGrantFlag"/> stocks.
	/// Retail's table is the first 22 words; its loop runs to slot <c>0x31</c>, so the last seven are the
	/// words that follow it in the image, read as ids just as the original reads them.
	/// </summary>
	private static readonly short[] WeaponUnitGrantIds = {
		18, 3, 4, 5, 10, 11, 12, 15, 27, 26, 17, 7, 6, 24, 23, 22, 25, 28, 29, 30, 31, 32,
		10169, 65, 8199, 0, -4, -1, 0,
	};

	/// <summary>
	/// <c>Armory_GrantCampaignWeapons</c> (<c>004126be</c>), the campaign debrief's weapon grants over the flag
	/// array the mission left (docs/retail/formats/weapons-dat.md#campaign-grants--armory_grantcampaignweapons-004126be):
	/// each still-locked weapon with a flag slot is unlocked when the slot holds its value, and the slot is
	/// zeroed either way; then each flag from <c>0x15</c> to <c>0x31</c> adds that many units of its weapon to
	/// stock at condition 100, leaving the flag set. Returns how many units were added.
	///
	/// <para>An id past the catalog in the table's overrun appends, in retail, to a list outside the
	/// weapon record array. This engine cannot reproduce that write, so it counts the unit and stocks
	/// nothing; no retail save holds a nonzero flag there.</para>
	/// </summary>
	public int GrantCampaignWeapons(short[] flags) {
		for (int id = 0; id < WeaponUnlockSlots.Length; id++) {
			int slot = WeaponUnlockSlots[id];
			if (slot != -1 && !IsWeaponUnlocked(id)) {
				if (flags[slot] == WeaponUnlockValues[id]) {
					Stock(id).UnlockFlag = 1;
				}

				flags[slot] = 0;
			}
		}

		int granted = 0;
		for (int k = 0; k < WeaponUnitGrantIds.Length; k++) {
			int weaponId = WeaponUnitGrantIds[k];
			for (int n = 0; n < flags[FirstUnitGrantFlag + k]; n++) {
				granted++;
				if (weaponId >= 0 && weaponId < ShellMissionLaunch.WeaponCatalogCount) {
					// Armory_AddNewUnit(id, 100) (0041229d): WeaponUnit_Init(unit, id, 100, 100, 5), then Armory_AddUnit.
					AddUnit(new ShellWeaponUnit(weaponId, fitCondition: 100, condition: 100, guidance: ShellWeaponUnit.NoGuidance));
				}
			}
		}

		return granted;
	}

	/// <summary>
	/// A new unit into stock — <c>WeaponUnit_Init(unit, id, fitCondition, 100, guidance)</c> then
	/// <c>Armory_AddUnit</c>. <c>Armory_AddNewUnit</c> (<c>0041229d</c>) makes the pair with guidance 5 for a
	/// <c>results.dat</c> salvage pair or a campaign grant, and <c>Armory_DeliverQueue</c> makes it inline.
	/// </summary>
	internal void AddNewUnit(int weaponId, int fitCondition, int guidance) =>
		AddUnit(new ShellWeaponUnit(weaponId, fitCondition: fitCondition, condition: 100, guidance: guidance));

	/// <summary>The missile racks <c>Armory_DeliverQueue</c> delivers with guidance 1: ids <c>0xd</c> to <c>0x10</c>, Razor's included.</summary>
	private const int FirstDeliveredRack = 0xd;
	private const int LastDeliveredRack = 0x10;

	/// <summary>
	/// <c>Armory_DeliverQueue</c> (<c>00412428</c>) — one new unit at condition 100 per occupied queue slot,
	/// guidance 1 for the four missile racks and none for anything else, and the queue's total price,
	/// which the caller takes off the pool. The queue itself is left as it was, so a hand-built one
	/// delivers again at the next debrief (docs/retail/shell/campaign-loop.md#the-debrief--game_processmissionresults-0040eae7).
	/// </summary>
	public int DeliverQueue(Func<int, int> priceKilograms) {
		int total = 0;
		foreach (int weaponId in _queue) {
			if (weaponId != 0) {
				total += priceKilograms(weaponId);
				AddNewUnit(weaponId, 100, weaponId is >= FirstDeliveredRack and <= LastDeliveredRack ? ArhGuidance : ShellWeaponUnit.NoGuidance);
			}
		}

		return total;
	}

	/// <summary>The overall condition below which the debrief scraps a machine.</summary>
	public const int SettleScrapCondition = 30;

	/// <summary>
	/// <c>Herc_SettleAfterMission</c> (<c>00410c7c</c>) — the machine in <paramref name="bay"/> below
	/// <see cref="SettleScrapCondition"/> overall (<see cref="ShellBayMachine.OverallCondition"/>) is valued,
	/// its mounts stripped into stock and the bay emptied, and the value returned with <c>Scrapped</c> set; at
	/// or above, only its overall slot goes back to 100. Unlike <see cref="Scrap"/> the pilot keeps the bay.
	/// An empty bay settles nothing: the original writes the overall slot through the empty bay's null record.
	/// </summary>
	public (int Salvage, bool Scrapped) SettleAfterMission(int bay, ShellRepairCosts? costs) {
		if (Bay(bay) is not { } machine) {
			return (0, false);
		}

		if (machine.OverallCondition < SettleScrapCondition) {
			int value = costs?.ScrapValue(machine) ?? 0;
			StripMounts(machine);
			_bays[bay] = null;
			return (value, true);
		}

		machine.SetOverallSlot(100);
		return (0, false);
	}

	/// <summary>
	/// <c>Repair_Auto</c> (<c>00411328</c>) — the repair level, from 0 down, that <paramref name="budget"/>
	/// covers: while the cost of repairing to the level's target is above the budget, step down a level,
	/// unless the machine's own overall condition is already at or above that level. A cost below the
	/// budget is applied (<see cref="ShellBayMachine.ApplyRepair"/>) and returned; otherwise nothing is
	/// repaired and 0 returned. Both comparisons are unsigned, as the original's are.
	/// </summary>
	public static uint AutoRepair(ShellBayMachine machine, uint budget, ShellRepairCosts costs) {
		int level = 0;
		uint cost = (uint)costs.HercCost(machine, ShellRepairCosts.RepairTarget[level]);
		while (budget < cost) {
			if (ShellRepairCosts.LevelForCondition(machine.OverallCondition) <= level) {
				break;
			}

			level++;
			cost = (uint)costs.HercCost(machine, ShellRepairCosts.RepairTarget[level]);
		}

		if (cost < budget) {
			machine.ApplyRepair(ShellRepairCosts.RepairTarget[level]);
			return cost;
		}

		return 0;
	}

	/// <summary>
	/// <c>HercList_RemoveFirstOfType</c> (<c>00410bbe</c>) — the first bay holding a <paramref name="chassisType"/> machine has its mounts
	/// stripped into stock and is emptied, for no salvage, and its index is returned. With none, it returns
	/// what its last probe read, bay 7's chassis type, or <c>-1</c> for an empty bay 7 — which its caller,
	/// <c>Hangar_WithdrawChassis</c> (<c>0040e7cd</c>), then uses as a bay.
	/// </summary>
	public int RemoveFirstOfType(int chassisType) {
		int probe = -1;
		for (int bay = 0; bay < BayCount; bay++) {
			probe = _bays[bay]?.ChassisType ?? -1;
			if (probe == chassisType) {
				StripMounts(_bays[bay]!);
				_bays[bay] = null;
				return bay;
			}
		}

		return probe;
	}

	/// <summary>
	/// <c>Squad_TakeMember</c> (<c>0040fb4f</c>) as the debrief uses it to replace squad member
	/// <paramref name="member"/>: the squad's next record by its cursor becomes the member, the cursor steps
	/// modulo 12, and the record is reset by <c>Pilot_SetDefaults</c> (<c>0040fd17</c>) — no bay, off strength,
	/// no position, condition 100 and every counter 0. The debrief then takes one off
	/// <see cref="MachinesOnStrength"/>. The record written is <paramref name="save"/>'s, the game this hangar
	/// was read from; the pilot it replaces keeps its own record as it was.
	/// </summary>
	internal void ReplaceSquadMember(int member, PlayerSave save) {
		if (member < 0 || member >= _squad.Count || save.Squadmates == null) {
			return;
		}

		int squad = _squadRecords[member] / PilotsPerSquad;
		short next = save.SquadTailAndPlayerHead[SquadCount + squad];
		save.SquadTailAndPlayerHead[squad] = next;
		save.SquadTailAndPlayerHead[SquadCount + squad] = (short)((next + 1) % PilotsPerSquad);
		int record = squad * PilotsPerSquad + next;
		if (save.Squadmates.ElementAtOrDefault(record) is not { } pilot) {
			return;
		}

		pilot.Bay = -1;
		pilot.OnStrength = 0;
		pilot.SquadPosition = -1;
		pilot.Condition = 100;
		pilot.HercKills = pilot.FlyerKills = pilot.BaseKills = 0;
		pilot.TotalHercKills = pilot.TotalFlyerKills = pilot.TotalBaseKills = 0;
		pilot.MissionsFlown = 0;
		_squad[member] = Pilot(pilot)!;
		_squadRecords[member] = record;
		MachinesOnStrength--;
	}

	/// <summary>Which of the save's 36 squad records squad member <paramref name="member"/> is.</summary>
	internal int SquadRecordIndex(int member) => _squadRecords[member];

	/// <summary>The number of <c>herc_inf.dat</c> records <c>Herc_GrantUnlocks</c> walks; record <c>i</c> is chassis type <c>i</c>.</summary>
	private const int ChassisTypeCount = 9;

	/// <summary>
	/// <c>Herc_GrantUnlocks</c> (<c>004118c5</c>), the campaign debrief's chassis grant, run just before
	/// <see cref="GrantCampaignWeapons"/> (docs/retail/formats/herc-catalogs.md#chassis-unlocks--herc_grantunlocks-004118c5):
	/// each still-unavailable Raptor II, Ogre, Maverick or Razor becomes available when its flag slot holds
	/// the expected value, and the slot is zeroed either way. The expected value is the original's
	/// loop-carried local — 2, set to 1 in the Razor's branch — so the Razor, last in type order, is the
	/// only chassis that tests 1.
	///
	/// <para>The debrief that calls this is not ported (ROADMAP), so nothing calls it yet.</para>
	/// </summary>
	public void GrantChassis(short[] flags) {
		int expected = 2;
		for (int type = 0; type < ChassisTypeCount; type++) {
			if (IsChassisAvailable(type)) {
				continue;
			}

			int slot = type switch {
				1 => 0x3c,
				6 => 0x3e,
				7 => 0x3d,
				8 => 0x3f,
				_ => -1,
			};
			if (type == 8) {
				expected = 1;
			}

			if (slot != -1) {
				if (flags[slot] == expected) {
					_availableChassis.Add(type);
				}

				flags[slot] = 0;
			}
		}
	}

	/// <summary>
	/// The condition at which a mount survives its machine being scrapped: <c>Herc_StripMounts</c>
	/// (<c>00411795</c>) returns one at or above it to stock, and <c>Herc_ScrapValue</c> (<c>00413b50</c>)
	/// pays salvage for one below it.
	/// </summary>
	public const int ReturnToStockCondition = 80;

	/// <summary>
	/// BUILD's order, <c>Hangar_BuySelected</c> (<c>0040e91c</c>): <c>HercList_OrderIntoSelected</c> (<c>00410982</c>) puts a new record in <paramref name="bay"/> — the
	/// selected one, whatever it holds — and <see cref="ShellBayMachine.Ordered"/> fills it with
	/// <paramref name="buildMissions"/> to go, and the price, <paramref name="priceTons"/> times 1000, comes
	/// off the pool. Returns the price.
	/// </summary>
	public int Order(int bay, int chassisType, int priceTons, int buildMissions) {
		if (bay < 0 || bay >= BayCount) {
			return 0;
		}

		_bays[bay] = ShellBayMachine.Ordered(chassisType, buildMissions);
		int price = priceTons * ShellRepairCosts.KilogramsPerTon;
		SalvageKilograms -= price;
		return price;
	}

	/// <summary>
	/// The scrap dialog's ACCEPT, <c>Hangar_ScrapSelected</c> (<c>0040e757</c>, docs/retail/shell/armory.md#scrapping): the machine in
	/// <paramref name="bay"/> is valued, its mounts stripped into stock and the bay emptied
	/// (<c>HercList_ScrapSelected</c>, <c>00410922</c>), the value goes into the pool, and the pilot the bay had loses it — and whoever
	/// holds that pilot's squad position is taken off strength. Returns the salvage credited.
	/// </summary>
	public int Scrap(int bay, ShellRepairCosts? costs) {
		if (bay < 0 || bay >= BayCount) {
			return 0;
		}

		int value = 0;
		if (_bays[bay] is { } machine) {
			value = costs?.ScrapValue(machine) ?? 0;
			StripMounts(machine);
			_bays[bay] = null;
		}

		SalvageKilograms += value;
		if (PilotFor(bay) is { } pilot) {
			pilot.Bay = -1;
			int member = SquadMemberIndexAt(pilot.SquadPosition);
			if (member != -1) {
				SetOnStrength(member, false);
			}
		}

		return value;
	}

	/// <summary>
	/// <c>Herc_StripMounts</c> (<c>00411795</c>) — each fitted mount below the capacity at
	/// <see cref="ReturnToStockCondition"/> or better goes back into stock through <c>Armory_AddUnit</c>
	/// (<c>00411efd</c>); anything worse is destroyed.
	/// </summary>
	private void StripMounts(ShellBayMachine machine) {
		for (int slot = 0; slot < machine.MountCapacity; slot++) {
			if (machine.Mount(slot) is { } unit
				&& machine.Condition(ShellRepairCategory.Hardpoint, slot) >= ReturnToStockCondition) {
				AddUnit(unit);
			}
		}
	}

	/// <summary>The player's own pilot record, embedded in the player structure at <c>+0x04</c>.</summary>
	public ShellBayPilot? Player { get; private set; }

	/// <summary>
	/// The three squad members the player structure points at from <c>+0x3f</c>, in pointer order —
	/// the pilots the crew screen offers as <c>Available Pilots</c>. Fewer than three only when the
	/// save's squad block is short.
	/// </summary>
	public IReadOnlyList<ShellBayPilot> SquadMembers => _squad;

	/// <summary>
	/// The player structure's leading <c>int16</c>, <c>SquadPositionsInPlay</c> (<c>00482a78</c>): how many squad positions,
	/// counting the player's as position 0, are in play. The auto-repair pass and the
	/// <c>player.mec</c> export both bound their per-position loops by it, a squad member counts as on
	/// strength only in a position below it (<c>Squad_UpdateOnStrength</c>, <c>00410366</c>), and the crew screen draws the rows
	/// below it lit.
	/// </summary>
	public int SquadPositions { get; private set; }

	/// <summary>
	/// <c>00482a7a</c>, the player structure's second short: how many machines are on strength, the
	/// player's included. <see cref="SetOnStrength"/> moves it with the squad members' <c>+0x24</c> bytes.
	/// </summary>
	public int MachinesOnStrength { get; private set; }

	/// <summary>
	/// <c>Squad_MemberAtPosition</c> (<c>004102d6</c>) — the index among <see cref="SquadMembers"/> of
	/// the member whose <c>+0x27</c> is <paramref name="position"/>, or <c>-1</c>. It tests the three in
	/// pointer order and takes the first.
	/// </summary>
	public int SquadMemberIndexAt(int position) {
		for (int member = 0; member < _squad.Count; member++) {
			if (_squad[member].SquadPosition == position) {
				return member;
			}
		}

		return -1;
	}

	/// <summary>The squad member at <paramref name="position"/>, as <see cref="SquadMemberIndexAt"/> finds them, or null.</summary>
	public ShellBayPilot? SquadMemberAt(int position) =>
		SquadMemberIndexAt(position) is >= 0 and var member ? _squad[member] : null;

	/// <summary>
	/// Writes a pilot's bay, <c>+0x22</c> — for the player <c>Player_SetBay</c> (<c>0040e6c8</c>), which writes
	/// <c>00482a9e</c>; for a squad member the plain store the crew screen makes before
	/// <see cref="UpdateOnStrength"/>.
	/// </summary>
	public static void SetBay(ShellBayPilot pilot, int bay) => pilot.Bay = bay;

	/// <summary><c>Squad_SetMemberPosition</c> (<c>004102be</c>) — writes squad member <paramref name="member"/>'s position, <c>+0x27</c>.</summary>
	public void SetSquadPosition(int member, int position) {
		if (member >= 0 && member < _squad.Count) {
			_squad[member].SquadPosition = position;
		}
	}

	/// <summary>
	/// <c>Squad_SetMemberBay(member, -1)</c> (<c>0040e6d7</c>) — takes squad member <paramref name="member"/> out of any bay and
	/// off strength. That is the only way the crew screen calls it.
	/// </summary>
	public void UnassignSquadMemberBay(int member) {
		if (member >= 0 && member < _squad.Count) {
			_squad[member].Bay = -1;
			SetOnStrength(member, false);
		}
	}

	/// <summary>
	/// <c>Squad_UpdateOnStrength</c> (<c>00410366</c>) — the member at <paramref name="position"/> is on
	/// strength when the position is in play and they have a bay, and off it otherwise.
	/// </summary>
	public void UpdateOnStrength(int position) {
		int member = SquadMemberIndexAt(position);
		if (member != -1) {
			SetOnStrength(member, position < SquadPositions && _squad[member].Bay != -1);
		}
	}

	/// <summary>
	/// <c>Squad_SetOnStrength</c> (<c>00410327</c>) — writes a member's on-strength byte and moves <see cref="MachinesOnStrength"/>
	/// by one when the byte actually changes.
	/// </summary>
	private void SetOnStrength(int member, bool onStrength) {
		var pilot = _squad[member];
		if (onStrength && !pilot.OnStrength) {
			MachinesOnStrength++;
		} else if (!onStrength && pilot.OnStrength) {
			MachinesOnStrength--;
		}

		pilot.OnStrength = onStrength;
	}

	/// <summary>The machine in one bay, or null when the bay is empty or the index is out of range.</summary>
	public ShellBayMachine? Bay(int slot) => slot >= 0 && slot < BayCount ? _bays[slot] : null;

	/// <summary>
	/// <c>Squad_PilotForBay(00482a78, slot)</c> (<c>00410220</c>) — the pilot assigned to a bay, or null. It searches exactly
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
	/// <c>Herc_FirstBuiltBay</c> (<c>00410c2a</c>) — the first bay holding a fully built machine, or <c>-1</c> when there is
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
	/// <c>Herc_HasSingleDeployable</c> (<c>00410add</c>) — whether exactly one bay holds a machine that is built and deployable.
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
	/// <c>HercList_Deliver</c> (<c>00410b68</c>) — a machine into an empty bay. The original asserts the bay is empty; this
	/// replaces whatever is there.
	/// </summary>
	public void Deliver(int bay, ShellBayMachine machine) {
		if (bay >= 0 && bay < BayCount) {
			_bays[bay] = machine;
		}
	}

	/// <summary>
	/// <c>Squad_SetPositionsInPlay</c> (<c>004102ff</c>) — how many squad positions are in play, then <see cref="UpdateOnStrength"/> for
	/// positions 1 to 3.
	/// </summary>
	public void SetPositionsInPlay(int positions) {
		SquadPositions = positions;
		for (int position = 1; position < 4; position++) {
			UpdateOnStrength(position);
		}
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
		foreach (var item in save.Inventory?.Items ?? Array.Empty<Inventory.InventoryItem>()) {
			if (item?.Id is { } id) {
				// The save's stock reader, Armory_ReadWeaponStock (00411dbb), pushes each unit onto the head as it reads it, so the
				// file's last unit is the head.
				var stock = hangar.Stock(id.Id);
				stock.UnlockFlag = (byte)item.UnlockFlag;
				foreach (var entry in item.Units ?? Array.Empty<ShellWeaponEntry>()) {
					if (entry != null) {
						stock.Units.Add(ShellWeaponUnit.From(entry));
					}
				}
			}
		}

		hangar.QueueFreeSlots = save.BuildQueueFreeSlots;
		for (int slot = 0; slot < QueueSlots && slot < save.BuildQueue.Length; slot++) {
			hangar._queue[slot] = save.BuildQueue[slot]?.Id ?? 0;
		}
		foreach (var (slot, entry) in save.HercBay) {
			if (slot >= 0 && slot < BayCount && entry != null) {
				hangar._bays[slot] = ShellBayMachine.From(entry);
			}
		}

		foreach (var (herc, flag) in save.ChassisAvailability) {
			if (flag != 0) {
				hangar._availableChassis.Add(herc.Id);
			}
		}

		// Player_Read (004101b8) reads the player block's two leading shorts into 00482a78 and 00482a7a, then
		// points the player structure's three squad pointers at record DAT_00483b48[k] of squad k. The
		// save model keeps all five among the eight shorts between the squad block and the player's
		// record: DAT_00483b48's three first, the player block's two last.
		hangar.SquadPositions = save.SquadPositionsInPlay;
		hangar.MachinesOnStrength = save.MachinesOnStrength;
		hangar.Player = Pilot(save.PlayerPilot);
		for (int squad = 0; squad < SquadCount; squad++) {
			int member = save.SquadTailAndPlayerHead[squad];
			int record = squad * PilotsPerSquad + member;
			if (member >= 0 && member < PilotsPerSquad
				&& Pilot(save.Squadmates?.ElementAtOrDefault(record)) is { } pilot) {
				hangar._squad.Add(pilot);
				hangar._squadRecords.Add(record);
			}
		}

		return hangar;
	}

	/// <summary>Which of the save's 36 squad records each of <see cref="SquadMembers"/> was read from.</summary>
	private readonly List<int> _squadRecords = new();

	/// <summary>
	/// Writes what the screens change back into <paramref name="save"/>, the game it was read from, so
	/// that <c>Game_SaveSlot</c> (<c>0040e37b</c>) can write the whole of it: the armory stock and build
	/// queue, the bays, the salvage pool, the chassis flags <see cref="GrantChassis"/> sets, the player's
	/// bay, the three squad members' bay, position and on-strength byte, and the player block's two
	/// counts. Everything else the save carries — the career block, the other 33 squad records and the
	/// flag array — nothing here changes, and is left as it was read.
	///
	/// <para>A weapon's stock is written from the head of its list, as <c>Armory_Write</c>
	/// (<c>004121cf</c>) walks it, and read back by pushing each unit onto the head, so each save and load
	/// reverses the order (docs/retail/formats/save-games.md#armory-stock-record).</para>
	/// </summary>
	public void Store(PlayerSave save) {
		var items = new Inventory.InventoryItem[ShellMissionLaunch.WeaponCatalogCount];
		for (int id = 0; id < items.Length; id++) {
			var units = _stock.TryGetValue(id, out var stock) ? stock.Units : new List<ShellWeaponUnit>();
			items[id] = new Inventory.InventoryItem {
				Id = WeaponLUT.GetById(id),
				UnlockFlag = stock?.UnlockFlag ?? (short)0,
				Quantity = (short)units.Count,
				Units = Enumerable.Reverse(units).Select(unit => unit.ToEntry()).ToArray(),
			};
		}

		save.Inventory = new Inventory { Items = items };
		save.BuildQueueFreeSlots = (short)QueueFreeSlots;
		for (int slot = 0; slot < QueueSlots && slot < save.BuildQueue.Length; slot++) {
			save.BuildQueue[slot] = WeaponLUT.GetById(_queue[slot]) ?? WeaponLUT.None;
		}

		save.HercBay = new Dictionary<short, HercBayEntry>();
		for (int bay = 0; bay < BayCount; bay++) {
			if (_bays[bay] is { } machine) {
				save.HercBay[(short)bay] = machine.ToEntry();
			}
		}

		save.SalvageTotal = SalvageKilograms;
		for (int type = 0; type < ChassisTypeCount; type++) {
			if (HercLUT.GetById((short)type) is { } herc) {
				short flag = save.ChassisAvailability.TryGetValue(herc, out var read) ? read : (short)0;
				save.ChassisAvailability[herc] = !IsChassisAvailable(type) ? (short)0 : flag != 0 ? flag : (short)1;
			}
		}

		save.SquadPositionsInPlay = (short)SquadPositions;
		save.MachinesOnStrength = (short)MachinesOnStrength;
		if (Player != null && save.PlayerPilot != null) {
			save.PlayerPilot.Bay = (short)Player.Bay;
		}

		for (int member = 0; member < _squadRecords.Count; member++) {
			if (save.Squadmates?.ElementAtOrDefault(_squadRecords[member]) is { } record) {
				record.Bay = (short)_squad[member].Bay;
				record.SquadPosition = (short)_squad[member].SquadPosition;
				record.OnStrength = (byte)(_squad[member].OnStrength ? 1 : 0);
			}
		}
	}

	private static ShellBayPilot? Pilot(PilotEntry? pilot) =>
		pilot == null ? null
			: new ShellBayPilot((pilot.Name ?? string.Empty).TrimEnd('\0'), pilot.RosterId, pilot.Bay,
				pilot.Skill?.Id ?? 0, pilot.SquadPosition, pilot.OnStrength != 0, pilot.NameIndex);
}
