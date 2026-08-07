using System.Drawing;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// FILE - /SIMVOL0/GAU/(herc).GAU — appears to be the GUI config file for HUDs. See the Java
/// source for the extensive documented byte-offset layout (root panel, weapon list, chain/link/
/// autotrack buttons, energy meter, shield labels, MFD panel, throttle sliders, navbar,
/// reticle...).
/// Ported from org.hercworks.core.data.file.gau.GAUFile.
/// </summary>
public class GAUFile {
	public Point HudOrigin { get; set; }
	public Size HudScreenSize { get; set; }

	public HPanel? RootPanel { get; set; }
	public HWeaponPanelItem[]? Weapons { get; set; }
}
