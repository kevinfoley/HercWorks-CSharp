namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #12 (144 bytes/record) — one HERC the mission can field, exported as <c>script.dat</c> block 7
/// (<see cref="Script.ScriptSpawnRecordExport"/>). A second, distinct 144-byte record type from row #4
/// (<see cref="RewardPackage144"/>); despite sharing a byte count and both being candidate "spawn"
/// records, they have no structural relationship (this row has an identity field and the file's
/// heaviest template-inheritance usage, 48%; row #4 has neither). Its two position refs and two
/// action refs are almost entirely dead in retail (&lt;=2.4% used) — the record's real payload is
/// the 10-slot weapon fit at <see cref="WeaponRefs"/> and its ammunition twin
/// <see cref="WeaponSecondary"/>.
/// The member names match <see cref="Script.ScriptSpawnRecordExport"/>'s and the engine's
/// <c>MissionPlacement</c> where they are the same datum.
/// See docs/formats/msn-mission-file.md, "Row #12 field decode".
/// </summary>
public class MechRosterEntry144 : MapObject {
	/// <summary>
	/// 0x02 — condition ref; 43% real. Every record has at least one of GUID or condition
	/// populated — the file carves records into named/inheritable (GUID, 48%), named-fresh (11%),
	/// and fully anonymous/condition-only (41%) groups.
	/// </summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — parent/inherit index; 48% real, the highest inheritance usage of any row in the file.
	/// Whenever this is real, GUID is always also real (0 counterexamples).
	/// </summary>
	public short InheritIndex { get; set; }

	/// <summary>
	/// 0x06 — compound-condition partner: 3.9% real, values only -99 or 2, always co-occurring with
	/// a real <see cref="ConditionRef"/>. Same idiom as row #15's 0x02/0x06 and row #16's 0x02/0x04.
	/// </summary>
	public short CompoundConditionPartner { get; set; }

	/// <summary>
	/// 0x08 — the machine's standing AI radar setting, 0/1 (PASSIVE/ACTIVE), populated in every
	/// record. <c>DBSim_SpawnMissionObjects</c> copies it to <c>mech+0x97</c>.
	/// </summary>
	public short AiRadarActive { get; set; }

	/// <summary>
	/// 0x0A — the speed the machine's AI walks at, copied to <c>mech+0x252</c>. Zero (91% of records)
	/// means the AI's own default; the rest are large outliers (220/255/256).
	/// </summary>
	public short AiCruiseSpeed { get; set; }

	/// <summary>0x0C-0x2E — 18-short dead zone, always exactly 0 in all real data; round-tripped raw.</summary>
	public short[] DeadZone { get; set; } = new short[18];

	/// <summary>
	/// 0x30 — the mech type, an index into <c>nam\MECHS.NAM</c>; 47% real, range 0-20.
	/// An "Invalid mech type" assert in DBSIM is on this field.
	/// </summary>
	public short TypeIndex { get; set; }

	/// <summary>
	/// 0x32-0x44 — the record's real workhorse: the mech's <b>10-slot weapon fit</b>, one weapon
	/// catalog id per slot. The load loop resolves nothing out of it because it is not an
	/// intra-file cross-reference; DBSIM hands the array straight to <c>Mech_ConfigureLoadout</c>,
	/// the same call the player's own fit from <c>player.mec</c> goes through.
	/// <para><b>Slot positions are load-bearing.</b> Each hardpoint picks its slot by index, so
	/// compacting the array fits the wrong weapon to the wrong hardpoint and a slot no hardpoint
	/// addresses contributes nothing. See docs/simulation/weapon-mounts.md, "The join".</para>
	/// </summary>
	public short[] WeaponRefs { get; set; } = new short[10];

	/// <summary>0x46 — ref into row #6 (<see cref="MapPoint22"/>) — a spawn-position override; 0.1% populated, but live: unset means "use the group's point".</summary>
	public short PositionRef { get; set; }

	/// <summary>0x48 — ref into row #7 (<see cref="Heading10"/>) — the heading override, on the same terms; never used in retail data.</summary>
	public short HeadingRef { get; set; }

	/// <summary>0x4A — how many of the (counter ref, operation) pairs at 0x4C are filled, from the front; 0-4, dominant 0 (84%). Not exported to script.dat.</summary>
	public short PairCount { get; set; }

	/// <summary>
	/// 0x4C-0x72 — the machine's out-of-action report: ten interleaved (counter ref, operation) pairs,
	/// written to the mission counters when it goes out of the fight. Usage decays in matched pairs
	/// from 15.9% down to 0.5%, remaining pairs never used. First element of each pair has an
	/// unusually wide range (20-480); second is narrow (2-23, 4 distinct values). The export
	/// separates them into <see cref="Script.ScriptSpawnRecordExport.CounterRefs"/> and
	/// <see cref="Script.ScriptSpawnRecordExport.CounterOps"/>.
	/// </summary>
	public short[] OutOfActionReport { get; set; } = new short[20];

	/// <summary>
	/// 0x74-0x87 — the ammunition type per weapon slot, the second of the two parallel arrays
	/// <c>Mech_ConfigureLoadout</c> takes alongside <see cref="WeaponRefs"/>. Populated in every real
	/// record, values 0-5: a launcher slot reads 1 in the retail mission and every other slot carries
	/// the filler 5. See <see cref="Script.ScriptSpawnRecordExport.WeaponSecondary"/> for the evidence.
	/// </summary>
	public short[] WeaponSecondary { get; set; } = new short[10];

	/// <summary>0x88 — always exactly 2 in all real data.</summary>
	public short Constant2 { get; set; }

	/// <summary>0x8A — ref into row #10 (<see cref="Action82"/>): the action this machine fires when it is engaged; nearly dead (0.7% real).</summary>
	public short EngagementActionRef { get; set; }

	/// <summary>0x8C — ref into row #10: the action this machine fires when it is defeated, which chains a mission's reinforcement waves; nearly dead (2.4% real).</summary>
	public short DefeatActionRef { get; set; }

	/// <summary>
	/// 0x8E — the machine's starting condition as a percentage, 100 being pristine: 100 (98.5%) or
	/// 50 (1.5%). Under 80 the machine spawns pre-damaged, under 20 as a wreck.
	/// </summary>
	public short StartingCondition { get; set; }
}
