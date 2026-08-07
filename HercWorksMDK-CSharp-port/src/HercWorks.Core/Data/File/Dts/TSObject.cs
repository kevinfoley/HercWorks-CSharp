using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>
/// Root ThreeSpace2 3D object — most DTS segments inherit from this base type. DTS files are a
/// tree data structure.
/// Ported from org.hercworks.core.data.file.dts.TSObject.
/// </summary>
public abstract class TSObject {
	public TSObjectHeader? Header { get; }
	public int ByteLen { get; set; }
	public int Index { get; set; }
	public TSObject? Parent { get; set; }
	public byte[]? Data { get; set; }
	public int ListIndex { get; set; }

	protected TSObject() { }

	protected TSObject(TSObjectHeader hdr) {
		Header = hdr;
	}

	public int GetDataIndex() => Index + 8;

	public string MetaInfoString(string chunkName) {
		var str = new StringBuilder();

		str.Append("{ \n\"class\" : \"").Append(chunkName).Append("\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"len\" : ").Append(ByteLen).Append(",\n");

		return str.ToString();
	}

	public abstract StringBuilder JsonString(StringBuilder str);
}
