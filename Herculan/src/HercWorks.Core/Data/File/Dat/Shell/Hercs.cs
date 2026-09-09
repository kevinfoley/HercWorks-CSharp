using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/HERCS.DAT — the player's starting herc bay, loaded by VSHELL's
/// <c>LoadHercsDat</c> when a new campaign begins. A collection of ShellHercData.
///   0 - UINT16 - total hercs
///   SEQ0: S0_0 bayId, S0_2 Herc Id, S0_4 build percent, S0_6 build missions remaining,
///   S0_8 hardpoint count, SEQ1 per hardpoint: id, item ID, health percentage, missile enum
///   (05 = none).
///
/// <para>Retail ships four Outlaws in bays 0-3 and, in bay 4, a Razor at 0% with three missions
/// left to build — the state a freshly ordered chassis is left in, not a damaged one. See
/// <c>docs/formats/herc-catalogs.md</c>.</para>
///
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
