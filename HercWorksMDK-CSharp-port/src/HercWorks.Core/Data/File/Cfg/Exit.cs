using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// FILE - [ROOT]/DATA/EXIT.CFG (purpose unclear; file appears to be 2 empty-space bytes).
/// Ported from org.hercworks.core.data.file.cfg.Exit.
/// </summary>
public class Exit : DataFile {
	public Exit() : base("EXIT.CFG", "DATA/") { }
}
