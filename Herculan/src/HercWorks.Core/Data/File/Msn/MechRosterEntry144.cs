namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #12 (144 bytes/record) — one HERC the mission can field, exported as <c>script.dat</c> block 7
/// (<see cref="Script.ScriptMechRecord"/>): its type, weapon fit, AI settings, optional placement,
/// out-of-action report and action links. The member names match <see cref="Script.ScriptMechRecord"/>'s
/// and the engine's <c>MissionPlacement</c> where they are the same datum.
/// See docs/formats/msn-mission-file.md, "Row #12 field decode".
/// </summary>
public class MechRosterEntry144 : MapObject {
	/// <summary>0x02 — condition ref.</summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — variant key: unless <c>-1</c>, everything from 0x08 to 0x8F except <see cref="PairCount"/>
	/// is copied from a randomly picked variant (docs/formats/msn-mission-file.md#variants).
	/// </summary>
	public short VariantKey { get; set; }

	/// <summary>0x06 — compound-condition partner: -99 or 2, set only alongside <see cref="ConditionRef"/>.</summary>
	public short CompoundConditionPartner { get; set; }

	/// <summary>0x08 — the machine's standing AI radar setting, 0 PASSIVE or 1 ACTIVE; DBSIM copies it to <c>mech+0x97</c>.</summary>
	public short AiRadarActive { get; set; }

	/// <summary>0x0A — the speed the machine's AI walks at, copied to <c>mech+0x252</c>; 0 means the AI's default.</summary>
	public short AiCruiseSpeed { get; set; }

	/// <summary>0x0C-0x2F — 18 shorts, 0 in every retail record.</summary>
	public short[] DeadZone { get; set; } = new short[18];

	/// <summary>0x30 — the mech type, an index into <c>nam\MECHS.NAM</c>.</summary>
	public short TypeIndex { get; set; }

	/// <summary>
	/// 0x32-0x45 — the 10-slot weapon fit, one weapon catalog id per slot, <c>-1</c> for empty; DBSIM
	/// hands it to <c>Mech_ConfigureLoadout</c>.
	/// <para><b>Slot positions are load-bearing.</b> Each hardpoint picks its slot by index, so
	/// compacting the array fits the wrong weapon to the wrong hardpoint and a slot no hardpoint
	/// addresses contributes nothing. See docs/simulation/weapon-mounts.md, "The join".</para>
	/// </summary>
	public short[] WeaponRefs { get; set; } = new short[10];

	/// <summary>0x46 — ref into row #6 (<see cref="MapPoint22"/>): a spawn-position override; unset means the group's point.</summary>
	public short PositionRef { get; set; }

	/// <summary>0x48 — ref into row #7 (<see cref="Heading10"/>): a heading override.</summary>
	public short HeadingRef { get; set; }

	/// <summary>0x4A — how many of the pairs in <see cref="OutOfActionReport"/> are filled, from the front. Not exported to <c>script.dat</c>; a variant does not copy it.</summary>
	public short PairCount { get; set; }

	/// <summary>
	/// 0x4C-0x73 — the machine's out-of-action report: ten interleaved (counter ref, operation) pairs,
	/// written to the mission counters when it goes out of the fight. The export separates them into
	/// <see cref="Script.ScriptMechRecord.CounterRefs"/> and <see cref="Script.ScriptMechRecord.CounterOps"/>.
	/// </summary>
	public short[] OutOfActionReport { get; set; } = new short[20];

	/// <summary>
	/// 0x74-0x87 — the ammunition type per weapon slot, the second array <c>Mech_ConfigureLoadout</c>
	/// takes alongside <see cref="WeaponRefs"/>; non-launcher slots carry the filler 5. See
	/// <see cref="Script.ScriptMechRecord.WeaponSecondary"/>.
	/// </summary>
	public short[] WeaponSecondary { get; set; } = new short[10];

	/// <summary>0x88 — always 2 in retail; what reads it is not established.</summary>
	public short Constant2 { get; set; }

	/// <summary>0x8A — ref into row #10 (<see cref="MissionAction82"/>): the action this machine fires when it is engaged.</summary>
	public short EngagementActionRef { get; set; }

	/// <summary>0x8C — ref into row #10: the action this machine fires when it is defeated.</summary>
	public short DefeatActionRef { get; set; }

	/// <summary>0x8E — the machine's starting condition, per cent; see <see cref="Script.ScriptMechRecord.StartingCondition"/>.</summary>
	public short StartingCondition { get; set; }
}
