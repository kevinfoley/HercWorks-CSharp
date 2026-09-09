namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// Common struct, observed in 3 places so far: Hercs, InitHerc and TrainingHercs (all in
/// data/file/dat/shell). It is the <c>gam\*.dat</c> form of the same HERC record the save file
/// carries — see <see cref="Sav.HercBayEntry"/>, whose field names these mirror. The save form adds
/// the 66-byte condition block and the hardpoint capacity; the four fields here are all the catalogs
/// store, and everything else is derived at load. Sequence:
///   S0_0 - UINT16 - Herc Id (chassis type, 0-8)
///   S0_2 - UINT16 - build progress, percent complete. 100 in every shipped catalog except the
///          part-built Razor in HERCS.DAT, which is 0.
///   S0_4 - UINT16 - build time remaining, in missions. 00 = delivered.
///   S0_6 - UINT16 - hardpoint count
///     SEQ1 - hardpoint count
///       S1_0 - UINT - hardpoint ID
///       S1_2 - UINT - item ID
///       S1_4 - UINT - health percentage
///       S1_6 - UINT - missile enum, 05 = no missile type.
///
/// <para>The two build fields are construction state, not damage: VSHELL sets them when a chassis
/// is ordered — progress 0, remaining from the chassis's build time in <c>HERC_INF.DAT</c> — and
/// ticks them once per debrief until delivery. See <c>docs/formats/herc-catalogs.md</c>.</para>
///
/// Ported from org.hercworks.core.data.struct.vshell.hercs.ShellHercData.
/// </summary>
public class ShellHercData {
	public short HercId { get; set; }

	/// <summary>Build progress, percent complete. Record <c>+0x4a</c>.</summary>
	public short BuildPercent { get; set; }

	/// <summary>Missions still to run before delivery; 0 once built. Record <c>+0x78</c>.</summary>
	public short BuildStepNum { get; set; }

	public Dictionary<short, UiWeaponEntry>? Hardpoints { get; set; }
}
