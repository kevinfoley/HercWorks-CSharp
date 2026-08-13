using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANCyclicSequence.</summary>
public class ANCyclicSequence : ANSequence {
	public ANCyclicSequence() : base(TSObjectHeader.ANCyclicSequence) { }

	public ANCyclicSequence(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);
		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		return base.JsonString(str);
	}
}
