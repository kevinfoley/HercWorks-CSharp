namespace HercWorks.Vol;

/// <summary>
/// VOL folders are just the file extension used as a folder name.
/// Ported from org.hercworks.voln.FileType.
/// </summary>
public enum FileType {
	Bin, Bnd, Col, Cyc, Dat, Db0, Db1, Db2, Dba, Dbm, Dci, Dem, Dfn, Dgs, Dmg, Dpl, Dts,
	Ed0, Ed1, Ed2, Ed3, Edg, Eng, Fm, Fre, Gam, Gau, Ger, Gl, Hb0, Hb1, Hb2, Hba,
	Hd0, Hd1, Hd2, Hd3, Hfn, Hmi, Hmx, Msn, Nam, Ofs, Pdg, Rmp, Sav, Snc, Sos, Str,
	Ttm, Vue, Vol, Wld, Wav
}

public static class FileTypeExtensions {
	/// <summary>Equivalent of Java's FileType.val() — the lowercase extension string.</summary>
	public static string Val(this FileType type) => type.ToString().ToLowerInvariant();

	/// <summary>Equivalent of Java's FileType.typeFromVal(String).</summary>
	public static FileType? FromExtension(string? ext) {
		if (string.IsNullOrEmpty(ext)) return null;

		foreach (FileType t in Enum.GetValues<FileType>()) {
			if (string.Equals(t.Val(), ext, StringComparison.OrdinalIgnoreCase)) {
				return t;
			}
		}
		return null;
	}
}
