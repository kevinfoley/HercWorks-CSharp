using HercWorks.Core.Data.Struct.Herc;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for one entry of PlayerSave.UnlockedHercs. The underlying field is a
/// short, but every real .sav file probed only ever stored 0 or 1, so this exposes it as a plain
/// checkbox rather than a numeric field.
/// </summary>
public class HercUnlockRow {
	public short HercId { get; set; }
	public string HercName { get; set; } = string.Empty;
	public bool Unlocked { get; set; }

	public static HercUnlockRow FromLut(HercLUT herc, short value) => new() {
		HercId = herc.Id,
		HercName = herc.Name,
		Unlocked = value != 0
	};
}
