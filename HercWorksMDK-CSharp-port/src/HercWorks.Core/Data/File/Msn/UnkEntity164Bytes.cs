namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Seen towards end of map script. TRAIN5 has 13 of these.
/// Ported from org.hercworks.core.data.file.msn.UnkEntity164Bytes.
///
/// Flags notes (index : meaning): 0 = -1, 1 = -1, 2 = 1 (seems to toggle base ground texture?).
/// LayoutType: last val of '2' seen for bases but not unit spawns — 2 is the only val for bases,
/// crashes otherwise (2 = bases, 0 = units?).
/// LayoutId notes: 0-4 off-map/unknown config, 5 exists, 6 off-map/unknown config, 7-17 exist,
/// 18-19 crash.
/// </summary>
public class UnkEntity164Bytes : MapObject {
	/// <summary>Length 22.</summary>
	public short[]? Flags { get; set; }

	public short LayoutType { get; set; }
	public short LayoutId { get; set; }

	public MapCoord? MapCoord { get; set; }

	public UnkEntity10Byte? UnkEntity10Byte { get; set; }
	public short Unk10ByteId { get; set; }

	public UnkEntity16Byte? UnkEntity16Byte { get; set; }
	public short Unk16ByteId { get; set; }

	/// <summary>Length 20.</summary>
	public MapObject[]? MapEntities { get; set; }

	/// <summary>The raw array of shorts.</summary>
	public short[]? MapEntIds { get; set; }

	public UnkEntity22Byte? UnkEntity22Byte { get; set; }
	public short Unk22ByteId { get; set; }

	/// <summary>Length 33.</summary>
	public short[]? Values { get; set; }

	public override string ToString() {
		var sb = new System.Text.StringBuilder();

		sb.Append("\n{\n")
			.Append("\tguid = ").Append(GUID).Append('\n')
			.Append(" \tflags = ").Append(Flags == null ? "null" : "[" + string.Join(", ", Flags) + "]").Append('\n')
			.Append("\tlayoutType = ").Append(LayoutType).Append('\n')
			.Append("\tlayoutId = ").Append(LayoutId).Append('\n')
			.Append(" \tmapCoord = ").Append(MapCoord == null ? "-1" : MapCoord.ToString()).Append('\n')
			.Append(" \tunk10? = ").Append(Unk10ByteId).Append('\n')
			.Append(" \tunk16? = ").Append(Unk16ByteId).Append('\n')
			.Append(" \tentities = ").Append(MapEntities == null ? "null" : "[" + string.Join(", ", (object[])MapEntities) + "]").Append('\n')
			.Append(" \tunk22? = ").Append(Unk22ByteId).Append('\n')
			.Append("\tvals = ").Append(Values == null ? "null" : "[" + string.Join(", ", Values) + "]")
			.Append('\n')
			.Append("}\n");

		return sb.ToString();
	}
}
