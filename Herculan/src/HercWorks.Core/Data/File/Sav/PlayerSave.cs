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
}
