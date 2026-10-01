namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>
/// A weapon's id, range and name, the id and range held as raw two-byte fields, with the range also
/// as display text. <see cref="Id2"/> is set to the same bytes as <see cref="Id"/>, and
/// <see cref="IdInt"/> is never computed from them.
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
