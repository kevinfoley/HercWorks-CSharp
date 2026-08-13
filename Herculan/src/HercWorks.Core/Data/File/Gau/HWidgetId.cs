namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.HWidgetId.</summary>
public sealed class HWidgetId {
	public static readonly HWidgetId RootPanel = new(0, "root", "ROOTPANEL");
	public static readonly HWidgetId WeaponItem1 = new(1, "weapon_1", "WEAPON_ITEM_1");
	public static readonly HWidgetId WeaponItem2 = new(2, "weapon_2", "WEAPON_ITEM_2");
	public static readonly HWidgetId WeaponItem3 = new(3, "weapon_3", "WEAPON_ITEM_3");
	public static readonly HWidgetId WeaponItem4 = new(4, "weapon_4", "WEAPON_ITEM_4");
	public static readonly HWidgetId WeaponItem5 = new(5, "weapon_5", "WEAPON_ITEM_5");
	public static readonly HWidgetId WeaponItem6 = new(6, "weapon_6", "WEAPON_ITEM_6");
	public static readonly HWidgetId WeaponItem7 = new(7, "weapon_7", "WEAPON_ITEM_7");
	public static readonly HWidgetId WeaponItem8 = new(8, "weapon_8", "WEAPON_ITEM_8");
	public static readonly HWidgetId WeaponItem9 = new(9, "weapon_9", "WEAPON_ITEM_9");
	public static readonly HWidgetId WeaponItem10 = new(10, "weapon_10", "WEAPON_ITEM_10");
	public static readonly HWidgetId LinkchainPanel = new(11, "chainLink_panel", "LINKCHAIN_PANEL");
	public static readonly HWidgetId WpnChainBtn = new(12, "wpn_chain_button", "WPN_CHAIN_BTN");
	public static readonly HWidgetId WpnLinkBtn = new(13, "wpn_link_button", "WPN_LINK_BTN");
	public static readonly HWidgetId AutTrackBtn = new(14, "auto_track_button", "AUT_TRACK_BTN");

	private static readonly IReadOnlyList<HWidgetId> All = new[]
	{
		RootPanel, WeaponItem1, WeaponItem2, WeaponItem3, WeaponItem4, WeaponItem5, WeaponItem6,
		WeaponItem7, WeaponItem8, WeaponItem9, WeaponItem10, LinkchainPanel, WpnChainBtn,
		WpnLinkBtn, AutTrackBtn
	};

	private static readonly Dictionary<int, HWidgetId> ById = All.ToDictionary(w => w.Id);

	public int Id { get; set; }
	public string Label { get; set; }

	/// <summary>C# equivalent of Java's enum .name() — the constant's own identifier.</summary>
	public string Name { get; }

	private HWidgetId(int id, string label, string enumName) {
		Id = id;
		Label = label;
		Name = enumName;
	}

	public static HWidgetId? FromId(int id) => ById.GetValueOrDefault(id);

	public static int IdFromLabel(string label) {
		foreach (var hid in All) {
			if (string.Equals(hid.Label, label, StringComparison.OrdinalIgnoreCase)) {
				return hid.Id;
			}
		}
		return -1;
	}
}
