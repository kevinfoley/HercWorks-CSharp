using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/VUE/(herc).VUE — mostly defines 3D viewport sizes and offsets for each player
/// herc. TODO (carried over from Java): finish.
///   0 - UINT32 - total viewport entries
///   4 - SEQ_0 (INT32 each): origin x/y, width, height, then 4 unknown offsets.
/// Ported from org.hercworks.core.data.file.dbsim.Vue.
/// </summary>
public class Vue : DataFile {
	public int TotalViewports { get; set; }
	public Entry[]? Entries { get; set; }

	public Entry NewEntry() => new();

	public class Entry {
		public int OriginX { get; set; }
		public int OriginY { get; set; }
		public int WidthMax { get; set; }
		public int HeightMax { get; set; }

		public int UnkOfsX { get; set; }
		public int UnkOfsY { get; set; }
		public int UnkOfsW { get; set; }
		public int UnkOfsH { get; set; }
	}
}
