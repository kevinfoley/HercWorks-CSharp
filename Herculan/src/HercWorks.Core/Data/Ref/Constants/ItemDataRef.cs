namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>
/// Reference data for weapons.
/// Ported from org.hercworks.core.data.ref.constants.ItemDataRef.
/// </summary>
public class ItemDataRef {
	public byte[] Id { get; set; } = new byte[2];
	public byte[] Id2 { get; set; } = new byte[2];
	public byte[] RangeHex { get; set; } = new byte[2];

	public string Name { get; set; } = string.Empty;
	public int IdInt { get; set; }
	public string UiRange { get; set; } = string.Empty;

	public ItemDataRef(byte[] id, byte[] range, string name, string uiRange) {
		Id = id;
		Id2 = id;
		RangeHex = range;
		Name = name;
		UiRange = uiRange;
	}
}
