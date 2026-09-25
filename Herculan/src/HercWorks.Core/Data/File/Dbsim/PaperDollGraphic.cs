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

	/// <summary>
	/// Where one weapon icon sits around the doll — entry <c>n</c> places the icon of the hardpoint
	/// whose <c>.GL</c> slot byte (<see cref="GunLayout.HardpointEntry.HardpointId"/>) is <c>n</c>.
	/// See docs/formats/cockpit-hud-widgets.md#weapon-icons.
	/// </summary>
	public class HardpointEntry {
		/// <summary>The anchor point, relative to the view's origin, in the 320-wide space.</summary>
		public PixelPoint Origin { get; set; }

		/// <summary>Added to the weapon's own icon index to pick the <c>WEAPONS</c> frame.</summary>
		public int FrameOffset { get; set; }

		/// <summary>
		/// Which point of the icon lands on <see cref="Origin"/>: the low three bits choose the
		/// horizontal edge (1 left, 2 right, 4 centre) and the rest the vertical (8 top, 0x10 bottom,
		/// 0x20 centre).
		/// </summary>
		public int Alignment { get; set; }

		/// <summary>The icon's blit flags. 0 in every retail file.</summary>
		public int BlitFlags { get; set; }
	}
}
