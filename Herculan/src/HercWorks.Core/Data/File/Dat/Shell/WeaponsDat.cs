using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/WEAPONS.DAT
///   0 - UINT16 - total weapon list.
///   SEQ 0: id, name len + null-terminated name, salvage cost (x100 tons-to-Kg), start-unlocked
///   byte, armory workshop build priority.
///   Then: UINT16 total campaign-start weapon inventory, SEQ 1 UiWeaponEntry (weapon id, health %,
///   missile enum).
/// Ported from org.hercworks.core.data.file.dat.shell.WeaponsDat.
/// </summary>
public class WeaponsDat : DataFile {
	public short TotalCount { get; set; }
	public Entry[] Data { get; set; }
	public short StartWeaponTotal { get; set; }
	public UiWeaponEntry[]? StartingWeapons { get; set; }

	public WeaponsDat(int total) {
		TotalCount = (short)total;
		Data = new Entry[total];
	}

	public Entry AddEntry(int idx) {
		var item = new Entry();
		Data[idx] = item;
		return item;
	}

	public class Entry {
		public short Id { get; set; }
		public short NameLen { get; set; }
		public byte[]? Name { get; set; }
		public short SalvageCost { get; set; }
		public byte StartUnlock { get; set; }
		public short AutobuildPriority { get; set; }
	}
}
