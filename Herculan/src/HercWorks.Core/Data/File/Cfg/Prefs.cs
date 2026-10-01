using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// <c>DATA\PREFS.CFG</c> — the simulator's option array, one byte per option, 54 bytes in retail.
/// No fields are modelled here. See docs/simulation/preferences.md.
/// </summary>
public class Prefs : DataFile {
	public Prefs() : base("PREFS.CFG", "DATA/") { }
}
