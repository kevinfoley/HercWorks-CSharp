using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/RPR_[herc].DAT.
///   RPR_[herc].DBA: 0 UINT16 total images to draw, SEQ_0 UiHardpointGraphic (no outline data):
///   id, X coord, Y coord, DBA frame Id, render flag.
///   [HERC]_INT.DBA: internal components image (UiHardpointGraphic): id (0), X coord, Y coord,
///   DBA frame Id, render flag, then total entries.
///   Layout from there resembles ARM_[herc].DAT: SEQ_1 weapon data (Weapon Id, ...).
/// Ported from org.hercworks.core.data.file.dat.shell.RprHerc.
/// </summary>
public class RprHerc {
	public short BodyImgTotal { get; set; }
	public Dictionary<short, UiImageDBA>? BodyImages { get; set; }

	public UiHardpointGraphic? InternalImage { get; set; }

	public short TotalHardpoints { get; set; }
	public Dictionary<short, UiHardpointGraphic[]>? WeaponHardpoints { get; set; }
}
