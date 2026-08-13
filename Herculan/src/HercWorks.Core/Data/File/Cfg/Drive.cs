using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>FILE - [ROOT]/DATA/DRIVE.CFG. Ported from org.hercworks.core.data.file.cfg.Drive.</summary>
public class Drive : DataFile {
	public string[]? DriveLines { get; set; }

	public Drive() : base("DRIVE.CFG", "DATA/") { }
}
