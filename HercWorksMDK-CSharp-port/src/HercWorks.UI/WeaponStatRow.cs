namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for a single WEAPONS.DAT catalog entry (WeaponsDat.Entry). Kept
/// separate from the Core data class so the grid can bind directly, and so the raw
/// length-prefixed name bytes can be edited as a plain string.
/// </summary>
public class WeaponStatRow {
	public short Id { get; set; }
	public string Name { get; set; } = string.Empty;

	/// <summary>Raw stored value — per the file format doc, multiply by 100 to get Kg.</summary>
	public short SalvageCost { get; set; }

	public byte StartUnlock { get; set; }
	public short AutobuildPriority { get; set; }
}
