namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>
/// Bound to PlayerSave — a chunk of save data dealing just with squadmate state, and player state.
/// Variable-length, 31 bytes plus the name. On-disk layout, with the record's in-memory offsets
/// (VSHELL's reader is <c>FUN_0040fefc</c>, its mirror writer <c>FUN_0040fd5f</c>):
///   +0x00 - UINT16 - roster id, 0-11: squad index x 4 plus a per-squad shuffle
///   +0x02 - UINT16 - name index into esnames.bin, 0-35
///           UINT16 - name length, strlen+1
///           STRING - the name, NUL included
///   +0x22 - UINT16 - assigned herc bay slot; FF FF is unassigned
///   +0x24 - UINT8  - on strength; gates repair billing, results accounting and the .mec export
///   +0x25 - UINT16 - skill 0-3: Rookie, Regular, Veteran, Elite
///   +0x27 - UINT16 - squad slot, FF FF initially; selects the promotion divisor pair
///   +0x29 - UINT16 - rank 0-3: Lieutenant, Captain, Major, Lt Colonel
///   +0x2b - UINT16 - condition; overwritten at debrief from the machine's overall damage
///   +0x2d,+0x2f,+0x31 - UINT16 - Herc, Flyer and Base kills for the last mission
///   +0x33,+0x35,+0x37 - UINT16 - the same three as career totals
///   +0x39 - UINT16 - missions flown
///
/// <para><b>Skill and rank are two independent 0-3 fields</b>, at +0x25 and +0x29, and both stay
/// inside 0-3 in every retail save. They read their labels from one contiguous run of eight UI
/// strings at different base offsets, which is why a single 0-7 enum appears to work for display
/// while conflating two separate ladders. <see cref="Rank"/> below sits at +0x25 and so holds the
/// <i>skill</i>; <see cref="Unk2Uint16"/> sits at +0x29 and holds the rank.</para>
///
/// Ported from org.hercworks.core.data.struct.vshell.sav.PilotEntry.
/// See <c>docs/formats/save-games.md</c> for the record and
/// <c>docs/shell/campaign-loop.md</c> for how the two ladders advance.
/// </summary>
public class PilotEntry {
	public short SquadmateId { get; set; }

	/// <summary>
	/// Index into esnames.bin, 0-35, at <c>+0x02</c>. Stored alongside the name string rather than
	/// instead of it, and every roster draw uses the index space exactly once so no two pilots in a
	/// run share a name.
	/// </summary>
	public short NameIndex { get; set; }

	public string? Name { get; set; }
	public short BayId { get; set; }
	public byte Active { get; set; }

	/// <summary>Skill at <c>+0x25</c>, 0-3.</summary>
	public PilotSkill? Skill { get; set; }

	/// <summary>Squad slot at <c>+0x27</c>; also selects the pilot's promotion divisor pair.</summary>
	public short CrewRowNum { get; set; }

	/// <summary>Rank at <c>+0x29</c>, 0-3 — a separate ladder from <see cref="Skill"/>.</summary>
	public PilotRank? Rank { get; set; }

	public short ProbablyHealth { get; set; }
	public short KillsHercs { get; set; }
	public short KillsFlyers { get; set; }
	public short KillsBuilding { get; set; }
	public short TotalKillHerc { get; set; }
	public short TotalKillFlyer { get; set; }
	public short TotalKillBldng { get; set; }
	public short MissionCount { get; set; }
}
