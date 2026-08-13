using HercWorks.Core.Data.File.Dyn;

namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// Class based on observed data patterns in /SHELL/GAM/ARM_[herc].DAT files.
/// Wraps a UiImageDBA with extra details.
/// Ported from org.hercworks.core.data.struct.vshell.hercs.UiHardpointGraphic.
/// </summary>
public class UiHardpointGraphic : UiImageDBA {
	public short Id { get; set; }

	/// <summary>Linked via shared FrameId.</summary>
	public DynamixBitmapArray? OutlineImg { get; set; }

	public int OutlineX { get; set; }
	public int OutlineY { get; set; }

	public override string ToString() {
		return "UiHardpointGraphic [hardpointId=" + Id
			 + ", originX=" + OriginX
			 + ", originY=" + OriginY
			 + ", outlineX=" + OutlineX
			 + ", outlineY=" + OutlineY
			 + ", frameId=" + FrameId + ", flags=" + Flags + "]";
	}
}
