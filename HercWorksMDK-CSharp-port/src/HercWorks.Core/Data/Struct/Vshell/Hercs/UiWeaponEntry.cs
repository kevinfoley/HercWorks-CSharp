using HercWorks.Core.Util;

namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// As observed at various parts of the code, herc hardpoint structs are seen mostly in VSHELL.EXE.
/// WARN: hardpoint IDs themselves are controlled by the parent of this class; this is just the
/// byte data for weapons-in-hardpoint.
///   0 - UINT16 - weapon id
///   2 - UINT16 - health percentage as 0-100, usually 0x64 (100%)
///   4 - UINT16 - missile enum — 'none' = 0x05, then 0x01-0x03 for the actual missile types.
/// Ported from org.hercworks.core.data.struct.vshell.hercs.UiWeaponEntry.
/// </summary>
public class UiWeaponEntry {
	public short ItemId { get; set; }
	public short HealthPercent { get; set; }
	public MissileType? MissileType { get; set; }

	public UiWeaponEntry() { }

	public UiWeaponEntry(short itemId, short healthPercent, MissileType missileType) {
		ItemId = itemId;
		HealthPercent = healthPercent;
		MissileType = missileType;
	}

	public byte[] ToByte() {
		var data = new byte[6];

		ByteOps.ShortLEToByteArr(data, 0, ItemId);
		ByteOps.ShortLEToByteArr(data, 2, HealthPercent);
		ByteOps.ShortLEToByteArr(data, 4, (short)(MissileType?.Id ?? 0));

		return data;
	}
}
