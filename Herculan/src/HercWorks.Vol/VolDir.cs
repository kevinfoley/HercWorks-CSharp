namespace HercWorks.Vol;

/// <summary>
/// Programmatic representation of a VOL (sub) directory. These are defined in
/// the VOL's header section. Ported from org.hercworks.voln.VolDir.
/// </summary>
public class VolDir {
	public string Label { get; set; } = string.Empty;
	public byte DirIdx { get; set; }

	/// <summary>
	/// The original Java type was a LinkedHashSet&lt;VolEntry&gt; (insertion-ordered,
	/// uniqueness by reference identity since VolEntry never overrode equals/hashCode).
	/// A List gives the same practical behavior here.
	/// </summary>
	public List<VolEntry> Files { get; set; } = new();

	public VolDir() { }

	public VolDir(string label, byte idx) {
		Label = label;
		DirIdx = idx;
	}
}
