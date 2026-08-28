using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/HERCS.DAT — most likely sets the player's starting herc list when a new
/// campaign is started. A collection of ShellHercData.
///   0 - UINT16 - total hercs
///   SEQ0: S0_0 bayId, S0_2 Herc Id, S0_4 health ratio, S0_6 build completeness, S0_8 hardpoint
///   count, SEQ1 per hardpoint: id, item ID, health percentage, missile enum (05 = none).
/// Ported from org.hercworks.core.data.file.dat.shell.Hercs.
/// </summary>
public class Hercs {
	public Entry[]? Data { get; set; }

	public Hercs() { }

	public Hercs(short total) {
		Data = new Entry[total];
	}

	public class Entry {
		public short BayId { get; set; }
		public ShellHercData? Herc { get; set; }
	}

	public Entry AddEntry() => new();
}
