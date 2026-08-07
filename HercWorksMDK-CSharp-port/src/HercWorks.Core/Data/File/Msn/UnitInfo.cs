using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Herc;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// 144-byte segment. Defines Herc unit spawn info.
/// Ported from org.hercworks.core.data.file.msn.UnitInfo.
/// </summary>
public class UnitInfo : MapObject {
	public short MapCoordId { get; set; }
	public short[] HeaderFlags { get; set; } = new short[22];
	public HercLUT? UnitId { get; set; }
	public short[] Weapons { get; set; } = new short[10];
	public short[] UnkFlags { get; set; } = new short[36];
	public short HealthModAdjust { get; set; }

	public override string ToString() {
		var str = new System.Text.StringBuilder();

		str.Append("\n{\n");
		str.Append("\tguid = ").Append(GUID).Append('\n');
		str.Append("\tmap coord = ").Append(MapCoordId).Append('\n');
		str.Append("\tflags = \n");
		str.Append("\t\t[").Append(string.Join(", ", HeaderFlags)).Append("]\n");

		if (UnitId == null) {
			str.Append("\tNo unit id!?\n");
		} else {
			str.Append('\t').Append(UnitId.Name)
				.Append('(').Append(UnitId.Id).Append(')')
				.Append('\n');
		}

		str.Append(" \tweapons = [");
		foreach (var s in Weapons) {
			if (s == -1) {
				str.Append(WeaponLUT.None.Name).Append(", ");
			} else {
				var w = WeaponLUT.GetById(s);
				str.Append(w?.Name).Append('(').Append(w?.Id).Append(')').Append(", ");
			}
		}
		str.Append("]\n\t\t");
		str.Append('[').Append(string.Join(", ", UnkFlags)).Append("]\n");
		str.Append("\thp mod = ").Append(HealthModAdjust).Append('\n');
		str.Append("}\n");

		return str.ToString();
	}
}
