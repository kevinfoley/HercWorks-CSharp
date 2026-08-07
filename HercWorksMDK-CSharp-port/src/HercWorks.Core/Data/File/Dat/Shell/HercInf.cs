using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/HERC_INF.DAT
///   0 - UINT16 - total hercs (observed value 9)
///   SEQ (hercs in id order): S0 herc id, S2 weight (tons), S4 speed (KPH), S6 hardpoint total,
///   S8 salvage requirement (tons), S10 UINT (possibly unlock flag), S12 mission count to finish
///   build, S14 boolean flag - start campaign available.
/// Ported from org.hercworks.core.data.file.dat.shell.HercInf.
/// </summary>
public class HercInf : DataFile {
	public short TotalHercs { get; set; }
	public HercInfEntry[] Data { get; set; }

	public HercInf(int totalHercs) {
		TotalHercs = (short)totalHercs;
		Data = new HercInfEntry[totalHercs];
	}
}
