namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/RPR_HOTS.DAT — configures the 'clickable area' for Herc sections in the
/// REPAIR view.
///   0 - UINT16 - total entries
///   SEQ_0: 0_0 UINT16 Segment ID, 0_2 UINT16 total chunks, SEQ_1: array of UINT32 values.
/// Ported from org.hercworks.core.data.file.dat.shell.HardpointOverlayConfig.
/// </summary>
public class HardpointOverlayConfig {
	public Herc[]? Entries { get; set; }

	public Herc NewEntry() => new();

	public class Herc {
		public short HercId { get; set; }
		public OverlayArea[]? Areas { get; set; }

		public Herc() { }

		public Herc(short uid, int coordSize) {
			HercId = uid;
			Areas = new OverlayArea[coordSize];
		}

		public OverlayArea NewSegment() => new();

		public class OverlayArea {
			public int Id { get; set; }
			public int X { get; set; }
			public int Y { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
		}
	}
}
