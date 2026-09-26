using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Sav;

namespace HercWorks.Core.Data.File.Sav;

/// <summary>
/// FILE - [root]/SAV/GAME_?.SAV — the campaign save. Slots are named by index, not by pilot:
/// <c>GAME_0</c>-<c>GAME_9</c> are the player-named slots, <c>GAME_R</c> the campaign autosave and
/// <c>GAME_T</c> the training autosave. <c>sav\GAMEFILE.STR</c> is the directory that maps a slot to
/// its filename and display label.
///
/// <para>There is no header, no magic and no length field — the file is a bare concatenation of the
/// blocks in the property order below, so it must be parsed by structure. Its writer opens without
/// <c>O_TRUNC</c>, so a shorter save over a longer one leaves a stale tail that is not a parse
/// failure. See <c>docs/formats/save-games.md</c>.</para>
///
/// Ported from org.hercworks.core.data.file.sav.PlayerSave.
/// </summary>
public class PlayerSave {
	public Inventory? Inventory { get; set; }
	public short WorkshopSpace { get; set; }
	public WeaponLUT[] WorkshopSlots { get; set; } = new WeaponLUT[5];

	/// <summary>
	/// The career block, 76 shorts / 152 bytes: campaign stage, mission within the stage, then three
	/// counted arrays of line indices into <c>data\mission.str</c> holding the briefing and debrief
	/// prose, then one trailing short. The mission number here is cosmetic — VSHELL takes the actual
	/// mission from the slot's own <c>script.dat</c>.
	///
	/// <para>Size is load-bearing: 152 bytes is the only length that leaves the 36 pilot records
	/// following it aligned. See <c>docs/formats/save-games.md</c>.</para>
	/// </summary>
	public short[] Unk4_stateFlags { get; set; } = new short[76];

	public PilotEntry[]? Squadmates { get; set; }

	/// <summary>
	/// The six shorts closing the squad block plus the two opening the player block — eight in all,
	/// sitting between the last squadmate record and the player's own pilot record.
	/// </summary>
	public short[] UnkRange_prePlayer { get; set; } = new short[8];
	public PilotEntry? PlayerPilot { get; set; }
	public Dictionary<short, HercBayEntry> HercBay { get; set; } = new();
	public Dictionary<HercLUT, short> UnlockedHercs { get; set; } = new();
	public int SalvageTotal { get; set; }

	/// <summary>
	/// Everything past the salvage pool, carried verbatim: the 2000-byte campaign flag array, the
	/// 2-byte game state, a 20-byte block, and any stale tail the non-truncating writer left behind.
	/// </summary>
	public byte[]? UnknownSaveValues { get; set; }

	// ---- Named views over the raw arrays above -------------------------------------------------
	// The arrays stay the storage, so the round trip is unchanged; these name the fields
	// docs/formats/save-games.md decodes.

	/// <summary>Career block short 0 — the campaign stage, counted from zero in the save.</summary>
	public short CampaignStage { get => Unk4_stateFlags[0]; set => Unk4_stateFlags[0] = value; }

	/// <summary>Career block short 1 — the mission within the stage.</summary>
	public short MissionInStage { get => Unk4_stateFlags[1]; set => Unk4_stateFlags[1] = value; }

	/// <summary>
	/// The career block's three counted arrays of <c>data\mission.str</c> line indices (10, 30 and 30
	/// slots, <c>-1</c> empty) that <c>Career_BuildBriefingText</c> assembles into the mission tab's
	/// objectives, briefing and intelligence report. Returned as <c>(count, lines)</c>; read-only views,
	/// since the lines only mean anything against the slot's own <c>missn%d.str</c>.
	/// </summary>
	public (short Count, short[] Lines) CareerObjectives => CareerArray(2, 10);
	public (short Count, short[] Lines) CareerBriefing => CareerArray(13, 30);
	public (short Count, short[] Lines) CareerIntelligence => CareerArray(44, 30);

	private (short, short[]) CareerArray(int countIndex, int length) =>
		(Unk4_stateFlags[countIndex], Unk4_stateFlags.Skip(countIndex + 1).Take(length).ToArray());

	/// <summary>
	/// The three shorts at <c>00483b48</c>: for each squad, the index of its current member among
	/// that squad's twelve pilot records.
	/// </summary>
	public short SquadMemberIndex(int squad) => UnkRange_prePlayer[squad];

	/// <summary>Squad positions in play, the player's as position 0 (<c>00482a78</c>).</summary>
	public short SquadPositionsInPlay { get => UnkRange_prePlayer[6]; set => UnkRange_prePlayer[6] = value; }

	/// <summary>Machines on strength (<c>00482a7a</c>) — the <c>player.mec</c> export's entry count.</summary>
	public short MachinesOnStrength { get => UnkRange_prePlayer[7]; set => UnkRange_prePlayer[7] = value; }

	/// <summary>The campaign flag array (<c>00482af8</c>): 1000 shorts, the <c>.msn</c> condition store.</summary>
	public const int CampaignFlagCount = 1000;

	/// <summary>
	/// Whether the tail is long enough to hold the flag array and the game state — true for every
	/// parsed retail save.
	/// </summary>
	public bool HasCampaignState => UnknownSaveValues is { Length: >= CampaignFlagCount * 2 + 2 };

	public short GetCampaignFlag(int index) => BitConverter.ToInt16(RequireTail(), index * 2);

	public void SetCampaignFlag(int index, short value) =>
		BitConverter.GetBytes(value).CopyTo(RequireTail(), index * 2);

	/// <summary>The two bytes after the flag array — the game state (<c>0048260e</c>).</summary>
	public short GameState {
		get => BitConverter.ToInt16(RequireTail(), CampaignFlagCount * 2);
		set => BitConverter.GetBytes(value).CopyTo(RequireTail(), CampaignFlagCount * 2);
	}

	private byte[] RequireTail() => HasCampaignState
		? UnknownSaveValues!
		: throw new InvalidOperationException("This save's tail is too short to hold the campaign flags.");
}
