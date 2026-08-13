using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Sav;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Sav;

/// <summary>
/// FILE - [root]/SAV/&lt;player name&gt;.sav
///   0 - UINT16 unknown flag, 2 - UINT8 spacer/possibly unknown value, 3 - UINT8 begin
///   Inventory segment, 3+X - UINT16 ...
/// Ported from org.hercworks.core.data.file.sav.PlayerSave.
/// </summary>
public class PlayerSave : DataFile {
	public Inventory? Inventory { get; set; }
	public short WorkshopSpace { get; set; }
	public WeaponLUT[] WorkshopSlots { get; set; } = new WeaponLUT[5];

	/// <summary>
	/// 0x00 - UINT16 - ?; 0x02 - UINT16 - mission number (just cosmetic! VSHELL grabs the correct
	/// script.dat for the actual mission); somewhere in this range: .DPL index num for briefing map.
	/// </summary>
	public short[] Unk4_stateFlags { get; set; } = new short[77];

	public PilotEntry[]? Squadmates { get; set; }
	public short[] UnkRange_prePlayer { get; set; } = new short[9];
	public PilotEntry? PlayerPilot { get; set; }
	public Dictionary<short, HercBayEntry> HercBay { get; set; } = new();
	public Dictionary<HercLUT, short> UnlockedHercs { get; set; } = new();
	public int SalvageTotal { get; set; }

	/// <summary>Massive chunk of save values after relevant data.</summary>
	public byte[]? UnknownSaveValues { get; set; }
}
