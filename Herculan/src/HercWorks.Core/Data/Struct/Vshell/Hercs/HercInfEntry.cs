namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// Utility class, abstracted out from HercInf — a separate class makes working with this data
/// easier. Ported from org.hercworks.core.data.struct.vshell.hercs.HercInfEntry.
/// </summary>
public class HercInfEntry {
	public short HercId { get; set; }

	/// <summary>Mass in tons. Printed by the Herc Construction screen as "%d TONS".</summary>
	public short Weight { get; set; }

	/// <summary>Top speed in kph, printed as "%d KPH".</summary>
	public short Speed { get; set; }

	/// <summary>
	/// Printed bare by the Herc Construction screen — but <b>not</b> what the game equips. Capacity
	/// comes from a nine-entry table in VSHELL's code, and the two disagree for the Raptor II: this
	/// field says 4 where the delivered machine has 5. Treat it as display text, not as the
	/// hardpoint count. See <c>docs/formats/herc-catalogs.md</c>.
	/// </summary>
	public short HardpointTotal { get; set; }

	/// <summary>Price in tons; VSHELL multiplies by 1000 to charge the salvage pool, which is in Kg.</summary>
	public short SalvageReq { get; set; }

	/// <summary>
	/// No reader traced. Runs 40, 50, 60, 95, 110, 100, 125, 30, 70 across the nine chassis, roughly
	/// 1.5x the mass. The other seven columns all have an identified consumer.
	/// </summary>
	public short UnknownFlag { get; set; }

	/// <summary>
	/// Missions a newly bought chassis takes to build. Copied into the bay entry's build-step
	/// counter, which ticks down once per debrief while the build percentage climbs.
	/// </summary>
	public short BuildMissionCount { get; set; }

	/// <summary>
	/// Whether this chassis starts available. Campaign state rather than a catalog constant: the
	/// four that ship 0 — Raptor II, Ogre, Maverick, Razor — are exactly the four VSHELL can unlock
	/// from campaign flags, and this array is what the save persists.
	/// </summary>
	public short FlagCampaignStart { get; set; }
}
