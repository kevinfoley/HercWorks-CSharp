using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /ZONE/DAT/ZONEXXXX.DAT
///   0 - UINT32 - must be either 7 or 8
///   4 - UINT32 - must be either 7 or 8
///   8 - UINT32 - 13, 14, or 15
///   12 - UINT32 - height scalar value, cannot be 0
/// Ported from org.hercworks.core.data.file.dat.sim.ZoneDat. The Java original declared these
/// fields without getters/setters (work in progress); exposed as public properties here since
/// that's the norm for every other class in this codebase.
/// </summary>
public class ZoneDat : DataFile {
	public int UnkInt1_7or8 { get; set; }
	public int UnkInt2_7or8 { get; set; }

	/// <summary>Observed values: 13, 14, 15.</summary>
	public int UnkInt3_varies { get; set; }

	public int HeightScalar { get; set; }
}
