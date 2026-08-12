using System.Text;

namespace HercWorks.Core.Data.File.Dts.Part;

/// <summary>
/// BmpTag is a plain zero-based frame index into whichever DBA bitmap-array is currently bound
/// to the owning TSShapeInstance at render time (confirmed via Ghidra RE of VSHELL.EXE's
/// TSBitmapPart_Render/TSShapeInstance_Render). This mechanism is specific to TSBitmapPart — the
/// unrelated TSTexture4Poly poly type does NOT work the same way (it has its own, more complex
/// vtable render method feeding a real textured-polygon rasterizer — see TSSurfaceEntry.cs's doc
/// comment). Which DBA file gets bound for a given .DTS model is NOT recorded anywhere in the
/// .DTS/.DBA files themselves — it's an application-level decision made by the calling code, and
/// per user domain knowledge is not one uniform naming rule (mech bodies mostly share a
/// weight-class atlas — LIGHT.DBA/MEDIUM.DBA/HEAVY.DBA/ENEMY.DBA — except a "certain mechs use
/// NEWHERCS.DBA instead" exception and the Apocalypse/Razor each having their own dedicated atlas).
/// See HercWorksMDK-CSharp-port/docs/formats/dts-texture-binding.md for the full picture. OfsX/OfsY
/// place the resulting textured quad. Ported from org.hercworks.core.data.file.dts.part.TSBitmapPart.
/// </summary>
public class TSBitmapPart : TSBasePart {
	public short BmpTag { get; set; }
	public byte OfsX { get; set; }
	public byte OfsY { get; set; }

	public TSBitmapPart() : base(TSObjectHeader.TSBitmapPart) { }

	public TSBitmapPart(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);

		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str = base.JsonString(str);

		str.Append(",\n");
		str.Append("\"bmp_tag\" : ").Append(BmpTag).Append(",\n");
		str.Append("\"ofs_x\" : ").Append(OfsX).Append(",\n");
		str.Append("\"ofs_y\" : ").Append(OfsY);

		return str;
	}
}
