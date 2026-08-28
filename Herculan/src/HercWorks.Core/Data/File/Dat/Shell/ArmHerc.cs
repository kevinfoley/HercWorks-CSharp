using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/ARM_[HERC].dat — herc armory panel image + hardpoint graphic bindings.
/// See the Java source (org.hercworks.core.data.file.dat.shell.ArmHerc) for the full documented
/// byte layout (top/bottom body image blocks, hardpoint "none" graphics, then a per-weapon
/// hardpoint graphics table).
/// Ported from org.hercworks.core.data.file.dat.shell.ArmHerc.
/// </summary>
public class ArmHerc {
	public short TopImgArrId { get; set; }
	public UiImageDBA? HercTopImg { get; set; }

	public short BottomImgArrId { get; set; }
	public UiImageDBA? HercBotImg { get; set; }

	public short TotalWeapons { get; set; }

	public Dictionary<short, UiHardpointGraphic[]>? WeaponHardpoints { get; set; }
}
