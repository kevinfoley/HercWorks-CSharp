using HercWorks.Core.Data.Struct.Herc;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// 62-byte segment. Seems to include Buildings and Vehicles only, so far.
/// Ported from org.hercworks.core.data.file.msn.MiscEntityInfo.
/// </summary>
public class MiscEntityInfo : MapObject {
	public short[] HeaderFlags { get; set; } = new short[3];
	public MiscEntityLUT? MiscEntityId { get; set; }
	public short[] Spawnflags { get; set; } = new short[25];
	public short HealthModAdjust { get; set; }

	public override string ToString() {
		var sb = new System.Text.StringBuilder();

		sb.Append("\n{\n");
		sb.Append("\tindxId = ").Append(GUID).Append('\n');
		sb.Append("\thdr flags =[").Append(string.Join(", ", HeaderFlags)).Append("]\n");
		sb.Append("\tentity = ").Append(MiscEntityId == null ? "null" : MiscEntityId.ToString()).Append('\n');
		sb.Append("\tflags = [").Append(string.Join(", ", Spawnflags)).Append("]\n");
		sb.Append("\thp mod = ").Append(HealthModAdjust).Append('\n');
		sb.Append("}\n");

		return sb.ToString();
	}
}
