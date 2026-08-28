namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/PDG/WEAPONS.PDG — cannot confirm if this is even used.
///   0 - UINT32 - total count
///   SEQ_0: possible x coord, possible y coord.
/// Ported from org.hercworks.core.data.file.dbsim.WeaponPaperDiagram.
/// </summary>
public class WeaponPaperDiagram {
	public Entry[]? Entries { get; set; }

	public Entry NewEntry() => new();

	public class Entry {
		public int X { get; set; }
		public int Y { get; set; }
	}
}
