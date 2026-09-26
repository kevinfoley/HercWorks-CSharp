namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for a single WEAPONS.DAT catalog entry (WeaponsDat.Entry). Kept
/// separate from the Core data class so the grid can bind directly, and so the raw
/// length-prefixed name bytes can be edited as a plain string.
/// </summary>
public class WeaponStatRow {
	public short Id { get; set; }
	public string Name { get; set; } = string.Empty;

	/// <summary>Price in tons; VSHELL multiplies it by 1000 at load to charge the pool, which is in kg.</summary>
	public short SalvageCost { get; set; }

	public byte StartUnlock { get; set; }
	public short AutobuildPriority { get; set; }
}
