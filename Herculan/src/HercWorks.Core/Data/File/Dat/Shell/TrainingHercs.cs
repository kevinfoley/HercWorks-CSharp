using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - SHELL/GAM/TRN_HERCS.DAT — training herc data, an array of ShellHercData entries.
/// VSHELL somehow knows which herc to load, but training skips the normal ARMING/BRIEFING
/// workflow. Ported from org.hercworks.core.data.file.dat.shell.TrainingHercs.
/// </summary>
public sealed class TrainingHercs {
	public List<ShellHercData>? Data { get; set; }
}
