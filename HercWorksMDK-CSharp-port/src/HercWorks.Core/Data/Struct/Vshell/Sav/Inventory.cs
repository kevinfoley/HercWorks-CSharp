namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>
/// Bound to PlayerSave — there's a chunk of save data dealing just with inventory state.
/// Inventory, starting at 0x04:
///   01 - UINT8 - flag - weapon is buildable in armory
///   00 00 - UINT16 - existing quantity of weapon
///   1) total items in the inventory
///   2) an array of ShellWeaponEntry structs
/// Ported from org.hercworks.core.data.struct.vshell.sav.Inventory.
/// </summary>
public class Inventory {
	public InventoryItem[]? Items { get; set; }

	public InventoryItem NewEntry() => new();

	/// <summary>
	/// Nested in the Java original as a non-static inner class, though it never actually used
	/// the enclosing Inventory instance — a plain nested class here is equivalent.
	/// </summary>
	public class InventoryItem {
		public WeaponLUT? Id { get; set; }
		public short UnlockFlag { get; set; }
		public short Quantity { get; set; }
		public ShellWeaponEntry[]? Data { get; set; }

		public InventoryItem() { }

		public InventoryItem(int total) {
			Quantity = (short)total;
			Data = new ShellWeaponEntry[total];
		}
	}
}
