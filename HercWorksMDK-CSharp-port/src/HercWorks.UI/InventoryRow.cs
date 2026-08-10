using HercWorks.Core.Data.Struct;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for one Inventory.InventoryItem. Only the buildable flag and quantity
/// are exposed for editing — the per-copy ShellWeaponEntry array (armor/internal health, missile
/// type per individual stocked item) isn't shown here; when Quantity is edited, CampaignResourcesForm
/// resizes that array on save, adding full-health/no-missile copies or truncating as needed. Real
/// data shows individual copies vary in armor health (partial-damage salvage) and missile type
/// (SARH/ARH/etc. for missile weapons, NONE otherwise) — not something a flat quantity edit can
/// meaningfully control, so new copies default to full health / no missile rather than guessing.
/// </summary>
public class InventoryRow {
	public WeaponLUT? WeaponId { get; set; }
	public string WeaponName => WeaponId?.Name ?? "(unknown)";
	public bool Buildable { get; set; }
	public short Quantity { get; set; }
}
