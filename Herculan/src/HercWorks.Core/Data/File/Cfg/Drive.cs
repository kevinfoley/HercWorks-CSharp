using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// <c>DATA\DRIVE.CFG</c> — the directory both executables put in front of the movie and voice-clip
/// paths. See docs/shell/screen-layout.md and docs/formats/cockpit-messages.md, "The training port".
/// </summary>
public class Drive : DataFile {
	public string[]? DriveLines { get; set; }

	public Drive() : base("DRIVE.CFG", "DATA/") { }
}
