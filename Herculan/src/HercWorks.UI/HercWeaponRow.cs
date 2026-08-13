using HercWorks.Core.Data.Struct;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for one HercBayEntry.Weapons entry (keyed by hardpoint socket id) in
/// HercBayEditorForm. Rows can be freely added/removed since the underlying dictionary is sparse
/// (equipped hardpoints only, not every hardpoint slot).
/// </summary>
public class HercWeaponRow {
	public short SocketId { get; set; }
	public WeaponLUT? WeaponId { get; set; }
	public short NameId { get; set; }
	public short HealthArmor { get; set; }
	public short HealthInternal { get; set; }
	public MissileType? MissileType { get; set; }
}
