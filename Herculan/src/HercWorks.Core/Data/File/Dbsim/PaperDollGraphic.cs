using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/PDG/{herc}.PDG — defines HUD wireframe graphics for targets, player herc, and
/// squadmates. Each herc/flyer gets a .PDG defining 3 views, each using a form of
/// drawSubImage(x,y,x1,y1) against the .DBA files in /SIMVOL0/DBA/{herc}.DBA (which house the 3
/// view forms of wireframes). See Java source for the full documented byte layout.
/// Ported from org.hercworks.core.data.file.dbsim.PaperDollGraphic.
/// </summary>
public class PaperDollGraphic {
	public int TotalViews { get; set; }
	public ViewEntry[]? Entries { get; set; }
	public HardpointEntry[]? Hardpoints { get; set; }

	public ViewEntry NewViewEntry() => new();
	public ViewRegion NewViewRegion() => new();
	public HardpointEntry NewHardpointEntry() => new();

	public class ViewEntry {
		public PixelPoint Origin { get; set; }
		public PixelPoint Size { get; set; }
		public ViewRegion[]? Regions { get; set; }
	}

	public class ViewRegion {
		public int Index { get; set; }
		public PixelPoint TopLeft { get; set; }
		public PixelPoint BottomRight { get; set; }
		public int Unk_val { get; set; }
		public int Spacer { get; set; }
	}

	public class HardpointEntry {
		public PixelPoint Origin { get; set; }
		public int Unk1 { get; set; }
		public int Unk2 { get; set; }
		public int Spacer { get; set; }
	}
}
