using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// FILE - [ROOT]/DATA/LANGUAGE.CFG (only observed values are 'E', 'F', or 'G').
/// Ported from org.hercworks.core.data.file.cfg.Language.
/// </summary>
public class Language : DataFile {
	public Language() : base("LANGUAGE.CFG", "DATA/") { }
}
