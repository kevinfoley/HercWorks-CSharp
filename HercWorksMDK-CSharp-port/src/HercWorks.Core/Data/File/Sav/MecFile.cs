using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Sav;

/// <summary>
/// ES2\DATA\player.mec
/// Ported from org.hercworks.core.data.file.sav.MecFile. No getter/setter in the original
/// (unused field); exposed as a public property here for the usual reason.
/// </summary>
public class MecFile : DataFile {
	public ShellHercPart? Data { get; set; }
}
