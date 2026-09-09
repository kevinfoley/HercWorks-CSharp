using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/WEAPONS.DAT
///   0 - UINT16 - total weapon list.
///   SEQ 0: id, name len + null-terminated name, salvage cost (stored in tons; VSHELL scales it
///   x1000 at load to give the cost in Kg), start-unlocked byte, armory workshop build priority.
///   Then: UINT16 total campaign-start weapon inventory, SEQ 1 UiWeaponEntry (weapon id, health %,
///   missile enum).
/// Ported from org.hercworks.core.data.file.dat.shell.WeaponsDat.
/// See <c>docs/formats/weapons-dat.md</c> for the 29-byte in-memory record this loads into, which
/// is not the same shape as the on-disk record.
/// </summary>
public class WeaponsDat {
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

		/// <summary>
		/// The name here is right, and VSHELL's auto-stock path is what proves it: that routine walks
		/// priorities 0 upward, resolving each to the weapon holding it, and queues the weapon when it
		/// is unlocked and the player owns fewer than two. Low value means built first, so the whole
		/// ordering runs advanced-to-basic — PLAS is 1 and MSL6 is 28.
		///
		/// <para>99 means never auto-built: the resolver skips those outright, and it is exactly the
		/// value NONE and the three Bull weapons carry. It is stored in a parallel array rather than
		/// inside the 29-byte record.</para>
		/// </summary>
		public short AutobuildPriority { get; set; }
	}
}
