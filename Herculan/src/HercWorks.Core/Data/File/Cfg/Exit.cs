using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// <c>DATA\EXIT.CFG</c>. Purpose not established; the retail file is two space bytes.
/// </summary>
public class Exit : DataFile {
	public Exit() : base("EXIT.CFG", "DATA/") { }
}
