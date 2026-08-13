namespace HercWorks.Core.Util;

/// <summary>
/// Solves 2 issues:
///   1) ES2 executables are coded to know which DPL file is matched to which DBA and DBM files.
///   2) ES2 executables seem aware of whether the corresponding DBM/DBA needs index 0 of the DPL
///      file to be an 'alpha transparent pixel'.
/// Ported from org.hercworks.core.util.PaletteBindingEntry.
/// </summary>
public class PaletteBindingEntry {
	public string? FileName { get; set; }
	public List<string> Files { get; set; } = new();
	public bool Index0Alpha { get; set; }

	public PaletteBindingEntry() { }

	public PaletteBindingEntry(string file, List<string> bindings, bool alphaIndex) {
		FileName = file;
		Files = bindings;
		Index0Alpha = alphaIndex;
	}
}
